# WP-4.2.0 — Zone Substrate

> **Phase 4.2.x kickoff packet, post-reorganization.** Per `docs/PHASE-4.2-REORGANIZATION-2026-05-03.md`: replaces the original "multi-floor topology" with a zone system — separate physical areas (parking lot, breakroom, warehouse, upstairs office) modeled as peer scenes the camera switches between, NPCs in inactive zones running at reduced simulation fidelity. This packet ships the **engine substrate**: zone identity on entities, multi-world-definition loader, runtime zone registry, transition trigger primitives. Active-zone tracking + camera glue land in 4.2.1; LoD lands in 4.2.2.

**Tier:** Sonnet
**Depends on:** WP-4.0.I (world-definition writer — round-trip discipline), WP-4.0.K (NameHint round-trip + WorldDefinitionDto v0.1.0 stability). Phase 1 substrate (WorldDefinitionLoader, RoomComponent).
**Parallel-safe with:** WP-4.0.M.1 (badge generation, disjoint), WP-4.0.N (hire economy, disjoint), Wave 4 INT work (Editor-side, disjoint).
**Timebox:** 150 minutes
**Budget:** $0.65
**Feel-verified-by-playtest:** NO (engine substrate; no user-visible surface in this packet)

---

## Goal

Today, the engine loads exactly **one** `world-definition.json` at boot. All entities live in a single implicit world; there is no concept of "this NPC is in the breakroom and that one is in the parking lot — they cannot see each other."

This packet introduces the **zone substrate** — a pure-engine layer that:

1. **Tags entities with a zone id.** `ZoneIdComponent` (string id, modder-extensible — consistent with the project's data-driven extension pattern). Existing entities get their zone id implicitly from the world-definition they were spawned from.
2. **Loads multiple world-definitions at boot.** `MultiWorldDefinitionLoader` accepts an array of file paths; spawns entities into the same `EntityManager` but each carries its zone id.
3. **Tracks loaded zones in a runtime registry.** `ZoneRegistry` exposes the list of loaded zone ids, lookup by id, and per-zone bounds metadata (the ASCII bounding rect of the zone's tile space — used by render hooks downstream).
4. **Defines transition triggers.** `ZoneTransitionComponent` on a tile (typically a doorway) — when an NPC's path crosses it, the engine emits a `ZoneTransitionEvent` carrying the dest zone id and the dest tile within that zone. Active-zone switching + camera glue (4.2.1) consumes this event.

After this packet:
- An entity has a zone id (defaults to a synthesised `default-zone` for single-world-definition boots — backwards compat).
- Multiple zones can be loaded at boot via the bootstrap config.
- Zones are queryable via `ZoneRegistry`.
- A transition trigger placed at a tile fires `ZoneTransitionEvent` when crossed (no consumer yet — that's 4.2.1's job).

The 30-NPC FPS gate is preserved. Per-tick cost added by zone tagging: O(1) per entity (single component lookup); negligible.

---

## Reference files

- `docs/PHASE-4.2-REORGANIZATION-2026-05-03.md` — design rationale.
- `docs/c2-infrastructure/PHASE-4-KICKOFF-BRIEF.md` — Phase 4.2.x revised section (added in this packet's docs deliverable).
- `docs/c2-infrastructure/MOD-API-CANDIDATES.md` — adds MAC-018 (zone substrate as Mod API surface).
- `docs/c2-content/world-definitions/playtest-office.json` — the canonical single-zone scene; remains valid as a single-zone setup.
- `docs/c2-content/world-definitions/office-starter.json` — second canonical scene.
- `APIFramework/Bootstrap/WorldDefinitionLoader.cs` — read in full. The new `MultiWorldDefinitionLoader` calls this in a loop, tagging entities with the zone id derived from the world-definition's `worldId` field.
- `APIFramework/Bootstrap/WorldDefinitionDto.cs` — `WorldId` field is the zone id. No schema change needed.
- `APIFramework/Components/RoomComponent.cs` — rooms get a zone id via the world-def they belong to.
- `APIFramework/Core/SimulationBootstrapper.cs` — read for boot-time service registration; new `ZoneRegistry` registers here.
- `APIFramework/Systems/Movement/PathfindingService.cs` — read in full. Pathfinder stays per-zone (an entity's pathfinding is bounded by its zone). 4.2.0 doesn't modify pathfinding behavior; 4.2.1 wires zone-aware filtering.

---

## Non-goals

- Do **not** implement camera switching or any visual zone-switching UX. That's WP-4.2.1's job.
- Do **not** implement simulation LoD (per-zone tick fidelity). That's WP-4.2.2's job.
- Do **not** modify the `world-definition.json` schema. Zones are runtime concepts; each world-def file is one zone.
- Do **not** implement cross-zone interactions (phone calls, intercom). Out of scope; future packet.
- Do **not** modify the existing single-world-definition boot path semantics. Single-world-def boot continues to work; the new multi-world-def boot is opt-in via the bootstrap config.
- Do **not** modify `WorldDefinitionWriter`. Each saved JSON file is one zone; the writer doesn't need zone awareness.
- Do **not** modify `IWorldMutationApi`. Author-mode mutations operate within the active zone (mutations apply to the entity's current zone implicitly; 4.2.1 wires this).
- Do **not** modify `PathfindingService`'s search logic. 2D per-zone pathfinding stays as-is; cross-zone "pathing" is handled by transition triggers, not by the pathfinder.
- Do **not** ship a save-game format that captures the multi-zone runtime state. Save/load of multi-zone state is a future packet (couples to the unsolved question of save-game vs. world-definition separation).
- Do **not** introduce a new RNG. Existing `SeededRandom` suffices.

---

## Design notes

### `ZoneIdComponent`

```csharp
namespace APIFramework.Components;

/// <summary>
/// Identifies which zone an entity belongs to.
/// Zone id matches the source world-definition's WorldId field.
/// Single-world-def boots tag everything with the synthesised id "default-zone".
/// </summary>
public struct ZoneIdComponent
{
    public string ZoneId { get; init; }
}
```

Added to every entity spawned via `WorldDefinitionLoader.LoadFromFile` (the loader is updated to attach this tag). Existing entities created outside the loader (test fixtures, runtime spawns from `IWorldMutationApi.CreateNpc` etc.) inherit the active zone via `ZoneRegistry.ActiveZoneId` — see below.

### `ZoneRegistry`

Boot-time service registered by `SimulationBootstrapper`:

```csharp
namespace APIFramework.Bootstrap;

public sealed class ZoneRegistry
{
    private readonly Dictionary<string, ZoneInfo> _byId = new(StringComparer.Ordinal);
    public string ActiveZoneId { get; private set; }    // Defaults to first loaded zone.
    public IReadOnlyCollection<ZoneInfo> AllZones => _byId.Values;

    public ZoneInfo? TryGet(string zoneId);
    public bool Contains(string zoneId);

    /// <summary>Internal — called by the loader during boot.</summary>
    internal void Register(ZoneInfo info);

    /// <summary>Sets the active zone. Throws if zoneId is unknown.</summary>
    public void SetActive(string zoneId);
}

public sealed record ZoneInfo(
    string  ZoneId,
    string  DisplayName,
    int     OriginX,
    int     OriginY,
    int     Width,
    int     Height);
```

`ZoneInfo` carries the bounding box of the zone in tile space — used by 4.2.1's camera retargeting. Width/Height come from the union of all room bounds in the zone.

### `MultiWorldDefinitionLoader`

```csharp
public static class MultiWorldDefinitionLoader
{
    /// <summary>
    /// Loads N world-definition files into the same EntityManager. Each entity from
    /// world-def k is tagged with ZoneIdComponent { ZoneId = worldDefK.WorldId }.
    /// Registers each zone in the supplied ZoneRegistry.
    /// First-loaded zone becomes the default active zone.
    /// </summary>
    public static IReadOnlyList<LoadResult> LoadAll(
        IEnumerable<string> paths,
        EntityManager       em,
        ZoneRegistry        registry,
        SeededRandom        rng);
}
```

Single-world-def boot continues to use `WorldDefinitionLoader.LoadFromFile` directly; that path is unchanged. The new multi-loader is opt-in.

### Zone tagging in the existing loader

`WorldDefinitionLoader.LoadFromFile` is extended to **optionally** accept a `zoneId` parameter:

```csharp
public static LoadResult LoadFromFile(
    string        path,
    EntityManager entityManager,
    SeededRandom  rng,
    string?       zoneIdOverride = null);  // null = use WorldDefinitionDto.WorldId
```

Every entity created during the load gets `ZoneIdComponent { ZoneId = zoneIdOverride ?? dto.WorldId }`. When no override and no `worldId` in the file, defaults to `"default-zone"`.

### `ZoneTransitionComponent`

```csharp
namespace APIFramework.Components;

/// <summary>
/// A tile that, when an entity's path crosses it, triggers a zone transition.
/// Typically attached to doorway entities. Spawned by world-definition (via a
/// new optional zoneTransitions block — additive content, no schema bump
/// since the loader is liberal in what it accepts).
/// </summary>
public struct ZoneTransitionComponent
{
    public string DestZoneId        { get; init; }
    public int    DestTileX         { get; init; }
    public int    DestTileY         { get; init; }
    public string TransitionLabel   { get; init; }   // e.g., "to-breakroom"; for UI / dev console
}
```

### `ZoneTransitionEvent`

```csharp
namespace APIFramework.Systems.Spatial;

public sealed record ZoneTransitionEvent(
    Guid    EntityId,
    string  FromZoneId,
    string  ToZoneId,
    int     DestTileX,
    int     DestTileY,
    long    TickEmitted);

public sealed class ZoneTransitionBus
{
    public event Action<ZoneTransitionEvent>? OnTransition;
    public void Emit(ZoneTransitionEvent evt);
}
```

A new `ZoneTransitionDetectionSystem` walks entities each tick, checks whether any have moved onto a `ZoneTransitionComponent` tile, and emits the event. **This packet ships the system stub that detects + emits; it does NOT actually move entities across zones.** That's 4.2.1's job — the consumer that listens to the bus and updates `ZoneIdComponent` + `PositionComponent` accordingly.

### Authoring transition triggers

Transition triggers are spawned via the existing `IWorldMutationApi` — extended in this packet with one new operation:

```csharp
/// <summary>
/// Spawns a zone transition trigger entity at the given tile in the active zone.
/// Crossing this tile fires a ZoneTransitionEvent.
/// </summary>
Guid CreateZoneTransition(int tileX, int tileY, string destZoneId, int destTileX, int destTileY, string label);
```

Boot-time transitions can also be authored via an additive `zoneTransitions` block in world-definition.json (no schema bump — the existing loader's `JsonSerializer` skips unknown fields gracefully; this packet adds the field handler).

### Backwards compatibility

Single-zone boots (today's playtest) continue to work without changes:
- `WorldDefinitionLoader.LoadFromFile` (without a zone id override) tags everything with `WorldDefinitionDto.WorldId` (existing field).
- `ZoneRegistry` registers one zone.
- No transition triggers exist; no transitions fire.
- Behavior is identical to today.

### Performance

Per-tick added cost:
- `ZoneTransitionDetectionSystem`: O(N) where N is the number of entities with `PositionComponent` AND a non-default `ZoneIdComponent`. With 30-100 NPCs this is sub-millisecond.
- `ZoneIdComponent` lookups are O(1) typed-array reads (post WP-3.0.5 `ComponentStore<T>` refactor).
- No new allocations per frame; the bus is fire-and-forget.

The 30-NPC FPS gate is verified explicitly via a new test variant: `PerformanceGate30NpcWithZoneSubstrateTests` confirms FPS holds when zone tagging is present.

### Save / load

`WorldStateDto` (the runtime snapshot, MAC-006) is **not modified** in this packet. The zone id of an entity is recoverable from the entity's `ZoneIdComponent` which serializes through the existing per-component snapshot path. No schema bump.

When the multi-zone save-game story matures (future packet, out of scope here), it'll need: a per-zone partition of `WorldStateDto`, plus the active-zone pointer. For now, single-zone save/load works as today; multi-zone save/load is a known gap.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/ZoneIdComponent.cs` (new) | Per-entity zone tag. |
| code | `APIFramework/Components/ZoneTransitionComponent.cs` (new) | Per-tile transition trigger. |
| code | `APIFramework/Bootstrap/ZoneRegistry.cs` (new) | Runtime registry of loaded zones; active-zone pointer. |
| code | `APIFramework/Bootstrap/ZoneInfo.cs` (new) | Zone metadata record (id, name, bounds). |
| code | `APIFramework/Bootstrap/MultiWorldDefinitionLoader.cs` (new) | Loads N world-defs, tags entities, registers zones. |
| code | `APIFramework/Bootstrap/WorldDefinitionLoader.cs` (modification) | Add optional `zoneIdOverride` param; tag every spawned entity with `ZoneIdComponent`; populate `ZoneInfo` bounds. |
| code | `APIFramework/Bootstrap/WorldDefinitionDto.cs` (modification) | Add optional `ZoneTransitionDefDto[] ZoneTransitions` block (additive — older files load with empty transitions). |
| code | `APIFramework/Systems/Spatial/ZoneTransitionBus.cs` (new) | Event bus for transition events. |
| code | `APIFramework/Systems/Spatial/ZoneTransitionEvent.cs` (new) | Event record. |
| code | `APIFramework/Systems/Spatial/ZoneTransitionDetectionSystem.cs` (new) | Per-tick check: entity moved onto transition tile? emit event. |
| code | `APIFramework/Mutation/IWorldMutationApi.cs` (modification) | Add `CreateZoneTransition` operation. |
| code | `APIFramework/Mutation/WorldMutationApi.cs` (modification) | Implement `CreateZoneTransition`. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modification) | Register `ZoneRegistry` and `ZoneTransitionBus` services. |
| test | `APIFramework.Tests/Bootstrap/ZoneRegistryTests.cs` (new) | Registry behavior + active-zone semantics. |
| test | `APIFramework.Tests/Bootstrap/MultiWorldDefinitionLoaderTests.cs` (new) | Load 2 zones; entities tagged correctly; zones registered. |
| test | `APIFramework.Tests/Bootstrap/ZoneIdRoundTripTests.cs` (new) | Single-zone boot tags everything with the world-def's worldId. |
| test | `APIFramework.Tests/Systems/Spatial/ZoneTransitionDetectionSystemTests.cs` (new) | NPC walking onto transition tile fires event with correct dest. |
| test | `APIFramework.Tests/Mutation/CreateZoneTransitionTests.cs` (new) | API operation creates entity with correct component. |
| test | `APIFramework.Tests/Performance/PerformanceGate30NpcWithZoneSubstrateTests.cs` (new) | FPS gate holds with zone tagging present. |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` | Add MAC-018 (zone substrate as Mod API surface). |
| doc | `docs/PHASE-4-KICKOFF-BRIEF.md` (modification) | Update Phase 4.2.x section per reorganization memo. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | Single-world-def boot tags every entity with `ZoneIdComponent` matching the file's `worldId`. | unit-test |
| AT-02 | When `worldId` is absent from the file, entities are tagged with `"default-zone"`. | unit-test |
| AT-03 | `MultiWorldDefinitionLoader.LoadAll` with 2 paths spawns entities tagged with the correct per-file zone id. | integration-test |
| AT-04 | `ZoneRegistry.AllZones` contains both zones after a 2-zone load; lookup by id returns the right `ZoneInfo`. | unit-test |
| AT-05 | First-loaded zone becomes the default `ActiveZoneId`. | unit-test |
| AT-06 | `ZoneRegistry.SetActive` with an unknown id throws. | unit-test |
| AT-07 | `ZoneInfo.Width` and `Height` reflect the union of all room bounds in that zone. | unit-test |
| AT-08 | An NPC walking onto a `ZoneTransitionComponent` tile fires `ZoneTransitionEvent` with correct `EntityId`, `ToZoneId`, `DestTileX`, `DestTileY`. | unit-test |
| AT-09 | `IWorldMutationApi.CreateZoneTransition` spawns an entity with `ZoneTransitionComponent` + `PositionComponent` at the requested tile. | unit-test |
| AT-10 | Existing `office-starter.json` and `playtest-office.json` continue to load via `WorldDefinitionLoader.LoadFromFile`; entity counts unchanged from pre-packet baseline. | regression |
| AT-11 | Existing `WorldDefinitionWriter` output round-trips through the existing loader unchanged (zone tagging is additive, doesn't alter JSON output). | round-trip-test |
| AT-12 | 30 NPCs in a single zone hold ≥ 60 FPS with zone substrate active. | perf-test |
| AT-13 | All Phase 0–3 + Phase 4.0.A–L tests stay green. | regression |
| AT-14 | `dotnet build` warning count = 0; all tests green. | build + test |
| AT-15 | MAC-018 added to `MOD-API-CANDIDATES.md`. | review |

---

## Mod API surface

This packet introduces **MAC-018: Zone substrate**. Append to `MOD-API-CANDIDATES.md`:

> **MAC-018: Zone substrate (multi-world-def loader + ZoneIdComponent + transition triggers)**
> - **What:** Engine substrate for multi-zone scenes. `ZoneIdComponent` tags every entity with its zone of origin (a string id matching a `world-definition.json#worldId`). `MultiWorldDefinitionLoader` loads N zones into the same EntityManager. `ZoneRegistry` exposes the runtime registry + active-zone pointer. `ZoneTransitionComponent` + `ZoneTransitionBus` describe and emit cross-zone movement events. `IWorldMutationApi.CreateZoneTransition` lets author-mode tools place transitions live.
> - **Where:** `APIFramework/Components/ZoneIdComponent.cs`, `ZoneTransitionComponent.cs`; `APIFramework/Bootstrap/ZoneRegistry.cs`, `MultiWorldDefinitionLoader.cs`, `ZoneInfo.cs`; `APIFramework/Systems/Spatial/ZoneTransitionBus.cs`, `ZoneTransitionEvent.cs`, `ZoneTransitionDetectionSystem.cs`; `APIFramework/Mutation/IWorldMutationApi.cs` (extension).
> - **Why a candidate:** Modders shipping "scene packs" (custom zones — a different floor plan, an outdoor area, a fantasy office wing) need a uniform way to declare their zone and connect it to the player's existing world. The substrate is data-driven (zones are JSON files) and additive (no breaking change to the single-zone boot path).
> - **Stability:** fresh (lands with WP-4.2.0).
> - **Source packet:** WP-4.2.0.

---

## Followups (not in scope)

- **WP-4.2.1 — Active zone + camera hook.** Consumes `ZoneTransitionEvent`; updates active zone; calls Unity-side camera retargeting.
- **WP-4.2.2 — Simulation LoD.** Per-zone tick fidelity for inactive zones.
- **WP-4.2.3 through 4.2.6** — emergent gameplay scenarios that consume the zone substrate.
- **Multi-zone save/load.** Future packet — needs the save-game story matured.
- **Cross-zone interactions.** Phone calls, intercom announcements. Future content packet; couples to FF-005 (notification carriers).
- **Zone-aware pathfinding cache.** Currently the pathfinder operates per-zone but cache key doesn't include zone id; if cross-zone same-coordinate paths happen, key collisions could surface. v0.1 ships single-zone pathfinding; cache audit deferred to when it matters.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: NOT required

Engine substrate. `dotnet test` green is the gate.

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm worktree at `.claude/worktrees/sonnet-wp-4.2.0/` on branch `sonnet-wp-4.2.0` based on recent `origin/staging`.
1. Implement the spec.
2. Run `dotnet test`. All must stay green (1555+ tests after the wave-4 baseline).
3. Stage all changes including self-cleanup.
4. Commit on the worktree's feature branch.
5. Push the branch.
6. Stop. Notify Talon: `READY FOR REVIEW — engine substrate, no visual verification needed.`

### Cost envelope

Target: **$0.65**. Substrate + bus + detection system + mutation API extension + tests. If cost approaches $1.10, escalate via `WP-4.2.0-blocker.md`.

Cost-discipline:
- Don't modify `PathfindingService` behavior (just acknowledge in code comments that pathfinding stays per-zone).
- Don't touch `WorldDefinitionWriter` (zones are runtime concept; writer is per-zone already).
- Don't modify the JSON schema files (the loader's tolerance for unknown fields is sufficient).

### Self-cleanup on merge

Standard. Check for `WP-4.2.1`, `WP-4.2.2` as direct dependents.
