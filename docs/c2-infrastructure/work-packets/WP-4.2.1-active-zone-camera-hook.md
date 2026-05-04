# WP-4.2.1 — Active Zone + Camera Hook

> **Phase 4.2.x wave 1 (post-reorg).** WP-4.2.0 ships the engine substrate (zone tagging, registry, transition triggers); this packet wires the **runtime side** that actually switches the active zone when a transition fires, and the **Unity host glue** that retargets the camera to the new zone's bounds. After this packet, walking through a transition tile (or calling a dev-console / author-mode command) actually changes what the player sees.

> **DO NOT DISPATCH UNTIL WP-4.2.0 IS MERGED** — depends on `ZoneTransitionBus`, `ZoneRegistry`, `ZoneTransitionComponent`.

**Tier:** Sonnet
**Depends on:** WP-4.2.0 (zone substrate — required), WP-3.1.S.0 (camera rig — for retargeting hook).
**Parallel-safe with:** WP-4.2.2 (LoD — disjoint files), WP-4.2.3+ (scenarios — these consume zones but don't modify the camera).
**Timebox:** 120 minutes
**Budget:** $0.55
**Feel-verified-by-playtest:** YES (after Editor wiring; single-press camera switch must feel snappy and the dest tile must be on-screen)
**Surfaces evaluated by next PT-NNN:** Does Ctrl+Shift+Z (or a dev-console `switch-zone <id>` command) snap the camera to the new zone in under 200ms? When an NPC walks through a transition trigger, do they appear at the dest tile in the dest zone correctly? Is there any visual glitch / flicker on switch?

---

## Goal

Today (post-WP-4.2.0), entities are tagged with zone ids and transition triggers fire `ZoneTransitionEvent`s, but nothing consumes them — the camera always shows the same view, and "switching zones" doesn't actually do anything visible.

This packet wires both halves of the actual switch:

1. **Engine side — `ActiveZoneSwitchSystem`.** Subscribes to `ZoneTransitionBus.OnTransition`. When fired, the system: (a) updates the entity's `ZoneIdComponent` to the dest zone, (b) updates its `PositionComponent` to the dest tile, (c) calls `ZoneRegistry.SetActive(destZoneId)` if the transitioning entity is the active-zone marker (currently — for a player-driven game where the player follows one NPC or a ghost camera — the active zone is set explicitly, not by NPC movement). v0.1: only explicit `SwitchActiveZone(zoneId, focusTile?)` calls update the active zone; NPC transitions just teleport the NPC.

2. **Engine side — explicit zone-switch API.** A new public method on `ZoneRegistry`: `SwitchActiveZone(string zoneId, (int X, int Y)? focusTile = null)`. Emits an `ActiveZoneChangedEvent` on a dedicated bus. WARDEN-only dev-console command `switch-zone <id> [x] [y]` calls this directly.

3. **Unity host side — `ZoneAwareCameraController`.** A small MonoBehaviour that subscribes to the `ActiveZoneChangedEvent` and retargets the existing `CameraController` to the new zone's bounds (`ZoneInfo.OriginX`, `OriginY`, `Width`, `Height`). Optionally accepts a focus tile for cinematic snap-to-position; otherwise centers on the zone's bounding rect.

4. **Unity host side — `AuthorModeController` extension.** Adds a `SwitchToZone(string zoneId, int? focusX, int? focusY)` method so author-mode UI buttons can trigger zone switches programmatically.

After this packet:
- A dev-console command (`> switch-zone breakroom`) snaps the camera to the breakroom zone.
- An author-mode button (Editor-wired) calls `AuthorModeController.SwitchToZone(...)`.
- An NPC walking through a transition trigger teleports to the dest zone (their `ZoneIdComponent` + `PositionComponent` update); the camera does NOT auto-follow (single-active-zone simulation; if the NPC was off-camera the player doesn't see the transition until they switch to the new zone).
- The 30-NPC FPS gate holds; per-frame zone-switch overhead is zero (it's an event-driven cost paid only on switch).

---

## Reference files

- `docs/c2-infrastructure/work-packets/WP-4.2.0-zone-substrate.md` — read in full. This packet is the immediate consumer of 4.2.0's substrate.
- `APIFramework/Bootstrap/ZoneRegistry.cs` — extended with `SwitchActiveZone` + `OnActiveZoneChanged` event.
- `APIFramework/Systems/Spatial/ZoneTransitionBus.cs` — the event source `ActiveZoneSwitchSystem` subscribes to.
- `ECSUnity/Assets/Scripts/Camera/CameraController.cs` — read for the camera retargeting API. The new `ZoneAwareCameraController` calls into this rather than reimplementing camera math.
- `ECSUnity/Assets/Scripts/BuildMode/AuthorModeController.cs` — extended with `SwitchToZone` method.
- `Warden.DevConsole/Commands/` (or wherever existing dev console commands live — verify in Wave 4 packet recall) — new `switch-zone` command.

---

## Non-goals

- Do **not** implement simulation LoD. That's WP-4.2.2 — the inactive-zone NPCs in this packet still tick at full fidelity (perf cost of N total NPCs even when only one zone is visible). LoD is the next packet.
- Do **not** implement camera animation between zones. v0.1 ships an instant snap; smooth transitions are a future polish packet (and likely a UX choice — fast snap may be the right answer for an office sim where the player is making frequent zone moves).
- Do **not** implement zone-switch UI (door-click → switch). That's Editor-wired UI work the user does. The dev-console + AuthorModeController API are sufficient as plumbing.
- Do **not** modify the zone substrate from 4.2.0. This packet is purely additive on top.
- Do **not** modify save/load. Active-zone state is recoverable from `ZoneRegistry.ActiveZoneId` which is process-runtime state; if a save is ever needed it'll capture this then.
- Do **not** introduce floor/level concepts. Zones are flat.
- Do **not** modify pathfinding. Per-zone pathfinding stays as-is; an NPC's pathfinder respects the entity's zone implicitly via its room context.

---

## Design notes

### `ZoneRegistry` extension

```csharp
public sealed class ZoneRegistry
{
    // ... existing from 4.2.0 ...

    public event Action<ActiveZoneChangedEvent>? OnActiveZoneChanged;

    /// <summary>
    /// Switch the active zone. Emits ActiveZoneChangedEvent.
    /// Optional focusTile is passed through to consumers (Unity camera retargeter).
    /// Throws if zoneId is unknown.
    /// </summary>
    public void SwitchActiveZone(string zoneId, (int X, int Y)? focusTile = null)
    {
        if (!_byId.ContainsKey(zoneId))
            throw new InvalidOperationException($"Unknown zone: {zoneId}");
        var prev = ActiveZoneId;
        ActiveZoneId = zoneId;
        OnActiveZoneChanged?.Invoke(new ActiveZoneChangedEvent(prev, zoneId, focusTile, _tickCount));
    }
}

public sealed record ActiveZoneChangedEvent(
    string  FromZoneId,
    string  ToZoneId,
    (int X, int Y)? FocusTile,
    long    TickEmitted);
```

### `ActiveZoneSwitchSystem` (engine)

Pure event-driven — runs in response to `ZoneTransitionBus.OnTransition`, not per-tick. Updates the entity's components and (optionally) the active zone if the transitioning entity is flagged as the player's focus. v0.1: NPC transitions teleport the NPC but do NOT shift the active zone (active zone is player-driven, not NPC-driven).

```csharp
public sealed class ActiveZoneSwitchSystem
{
    private readonly EntityManager _em;
    private readonly ZoneRegistry  _registry;

    public ActiveZoneSwitchSystem(EntityManager em, ZoneRegistry registry, ZoneTransitionBus bus)
    {
        _em       = em;
        _registry = registry;
        bus.OnTransition += HandleTransition;
    }

    private void HandleTransition(ZoneTransitionEvent evt)
    {
        var entity = _em.GetAllEntities().FirstOrDefault(e => e.Id == evt.EntityId);
        if (entity is null) return;
        // Update the entity's zone tag and position.
        entity.Add(new ZoneIdComponent { ZoneId = evt.ToZoneId });
        entity.Add(new PositionComponent { X = evt.DestTileX, Y = 0f, Z = evt.DestTileY });
    }
}
```

### Unity-side `ZoneAwareCameraController`

```csharp
#if WARDEN
public sealed class ZoneAwareCameraController : MonoBehaviour
{
    [SerializeField] private EngineHost _host;
    [SerializeField] private CameraController _camera;

    private void OnEnable()
    {
        // Subscribe via the bootstrapper's exposed registry.
        _host.WhenReady(() =>
        {
            var registry = _host.GetService<ZoneRegistry>();
            registry.OnActiveZoneChanged += HandleActiveZoneChanged;
        });
    }

    private void OnDisable()
    {
        var registry = _host.GetService<ZoneRegistry>();
        if (registry != null) registry.OnActiveZoneChanged -= HandleActiveZoneChanged;
    }

    private void HandleActiveZoneChanged(ActiveZoneChangedEvent evt)
    {
        var registry = _host.GetService<ZoneRegistry>();
        var info     = registry.TryGet(evt.ToZoneId);
        if (info is null) return;

        Vector3 focusWorld = evt.FocusTile.HasValue
            ? new Vector3(evt.FocusTile.Value.X, 0f, evt.FocusTile.Value.Y)
            : new Vector3(info.OriginX + info.Width * 0.5f, 0f, info.OriginY + info.Height * 0.5f);

        _camera.SnapTo(focusWorld);
    }
}
#endif
```

(`EngineHost.WhenReady` and `EngineHost.GetService<T>()` need to exist. If they don't, this packet adds them per the existing `EngineHost.Engine` accessor pattern.)

### `AuthorModeController.SwitchToZone`

```csharp
public void SwitchToZone(string zoneId, int? focusX = null, int? focusY = null)
{
    EnsureActive();
    var registry = ((SimulationBootstrapper)_host.GetBootstrapper()).GetService<ZoneRegistry>();
    var focus = (focusX.HasValue && focusY.HasValue)
        ? ((int X, int Y)?)(focusX.Value, focusY.Value)
        : null;
    registry.SwitchActiveZone(zoneId, focus);
}
```

### Dev-console command

```
> switch-zone <zoneId> [focusX] [focusY]
```

WARDEN-only. Calls `ZoneRegistry.SwitchActiveZone(...)` directly. Useful for testing without UI wiring.

### Performance

Per-frame cost: 0 when no zone switches happen (the event handlers do nothing). Per-switch cost: O(1) component update + one camera snap. Negligible.

The 30-NPC FPS gate is preserved with verification: `PerformanceGate30NpcWithActiveZoneSwitchTests` confirms FPS holds across rapid switches (10 switches/second sustained).

### Backwards compatibility

Single-zone boots have `ZoneRegistry.ActiveZoneId == "default-zone"` and never receive `OnActiveZoneChanged` (no-one calls `SwitchActiveZone`). The whole chain is no-op for single-zone setups.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Bootstrap/ZoneRegistry.cs` (modification) | Add `SwitchActiveZone` + `OnActiveZoneChanged` event. |
| code | `APIFramework/Bootstrap/ActiveZoneChangedEvent.cs` (new) | Event record. |
| code | `APIFramework/Systems/Spatial/ActiveZoneSwitchSystem.cs` (new) | Engine-side: handles `ZoneTransitionEvent` → entity update. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modification) | Construct + register `ActiveZoneSwitchSystem`. |
| code | `ECSUnity/Assets/Scripts/Engine/EngineHost.cs` (modification) | Add `GetService<T>()` accessor + `WhenReady(Action)` callback. (If they already exist, no-op.) |
| code | `ECSUnity/Assets/Scripts/Camera/ZoneAwareCameraController.cs` (new, WARDEN-gated) | Subscribes to `OnActiveZoneChanged`; calls `CameraController.SnapTo(...)`. |
| code | `ECSUnity/Assets/Scripts/Camera/CameraController.cs` (modification) | Add `SnapTo(Vector3 worldPos)` method if it doesn't already exist. |
| code | `ECSUnity/Assets/Scripts/BuildMode/AuthorModeController.cs` (modification) | Add `SwitchToZone(zoneId, focusX, focusY)` method. |
| code | `Warden.DevConsole/Commands/SwitchZoneCommand.cs` (new, WARDEN-gated) — OR equivalent location | `switch-zone <id> [x] [y]` dev-console command. |
| test | `APIFramework.Tests/Bootstrap/ZoneRegistrySwitchActiveTests.cs` (new) | `SwitchActiveZone` updates state + emits event; unknown id throws. |
| test | `APIFramework.Tests/Systems/Spatial/ActiveZoneSwitchSystemTests.cs` (new) | Transition event → entity zone tag + position update. |
| test | `APIFramework.Tests/Performance/PerformanceGate30NpcWithActiveZoneSwitchTests.cs` (new) | FPS gate holds with rapid zone switches. |
| test | `APIFramework.Tests/Bootstrap/ZoneTransitionRoundTripTests.cs` (new) | Transition → switch → save world → load → entity in correct zone with correct position. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `ZoneRegistry.SwitchActiveZone(zoneId)` updates `ActiveZoneId` and emits `OnActiveZoneChanged`. | unit-test |
| AT-02 | `SwitchActiveZone` with unknown zoneId throws `InvalidOperationException`. | unit-test |
| AT-03 | `OnActiveZoneChanged` event carries `FromZoneId`, `ToZoneId`, optional `FocusTile`, and tick. | unit-test |
| AT-04 | `ActiveZoneSwitchSystem` updates the transitioning entity's `ZoneIdComponent` and `PositionComponent` per the event. | unit-test |
| AT-05 | NPC transition does NOT auto-switch the active zone (player-driven only in v0.1). | unit-test |
| AT-06 | `AuthorModeController.SwitchToZone` (with author mode active) triggers the registry switch. | integration-test |
| AT-07 | `> switch-zone <id>` dev-console command switches the active zone. | integration-test (WARDEN) |
| AT-08 | Round-trip: NPC walks transition → world saved → loaded → NPC at dest tile in dest zone. | round-trip-test |
| AT-09 | `ZoneAwareCameraController` calls `CameraController.SnapTo` when `OnActiveZoneChanged` fires. | manual visual / integration |
| AT-10 | 30 NPCs, 10 zone switches/second sustained → FPS gate holds. | perf-test |
| AT-11 | All Phase 0–3 + Phase 4.0.A–L + WP-4.2.0 tests stay green. | regression |
| AT-12 | `dotnet build` warning count = 0; all tests green. | build + test |

---

## Mod API surface

This packet **extends MAC-018** (zone substrate) — adds `ZoneRegistry.SwitchActiveZone` + `OnActiveZoneChanged` event as the modder-relevant runtime API. No new MAC entry; bumps MAC-018's stability one notch closer to *stabilizing* (now has 2 consumers — the system + the camera controller).

---

## Followups (not in scope)

- **WP-4.2.2 — Simulation LoD.** Inactive-zone NPCs tick at coarser fidelity. The single biggest perf win the zone system enables.
- Smooth camera transitions between zones (animation rather than snap). Polish packet; design TBD (snap may be the right answer for the office sim's tempo).
- Auto-switch on player-controlled NPC transition (for player-embodiment modes that follow a specific NPC). Couples to FF-016 / Q5.
- Zone-switch UI affordances (clickable doors). Editor-wired; user's hands.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED (Editor wiring + manual switch)

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm worktree at `.claude/worktrees/sonnet-wp-4.2.1/` on branch `sonnet-wp-4.2.1` based on recent `origin/staging` (which now includes WP-4.2.0).
1. Implement the spec.
2. Run `dotnet test`. All must stay green.
3. Stage all changes.
4. Commit + push.
5. Notify Talon: `READY FOR VISUAL VERIFICATION — wire ZoneAwareCameraController into PlaytestScene; create a 2-zone test world (modify or duplicate playtest-office.json) with a transition trigger; verify dev-console "switch-zone" command snaps the camera correctly.`

### Cost envelope

Target: **$0.55**. Engine system + Unity controller + dev-console command + tests. If cost approaches $0.90, escalate via `WP-4.2.1-blocker.md`.

### Self-cleanup on merge

Standard. Check for `WP-4.2.2`, `WP-4.2.3+` as dependents.
