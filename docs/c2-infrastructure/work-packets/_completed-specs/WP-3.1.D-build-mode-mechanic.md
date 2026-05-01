# WP-3.1.D — Building Mode Mechanic

> **DO NOT DISPATCH UNTIL WP-3.1.A AND WP-3.0.4 ARE MERGED.**
> Build mode is the player-facing surface that calls `IWorldMutationApi` (from WP-3.0.4) to drive structural changes. The Unity scaffold (3.1.A) provides the camera and input system this packet extends. Both must be on `main` before dispatch. The 30-NPCs-at-60-FPS gate from 3.1.A also remains binding (build mode adds UI overhead but should not violate the gate).

**Tier:** Sonnet
**Depends on:** WP-3.1.A (Unity scaffold), WP-3.0.4 (`IWorldMutationApi`, `StructuralChangeBus`, `MutableTopologyTag`, `LockedTag`)
**Parallel-safe with:** WP-3.1.B (silhouettes), WP-3.1.C (lighting), WP-3.1.F (JSONL stream)
**Timebox:** 150 minutes
**Budget:** $0.60

---

## Goal

Build mode is the player's primary tool for shaping the office. After this packet:

- Pressing `B` (or clicking an on-screen build-mode button) toggles into build mode. The world tints slightly (a soft beige-blue overlay) signaling the mode shift.
- A build palette appears on one side of the screen, organised by category (walls, doors, desks, chairs, props, named anchors).
- The player drags an item from the palette into the world; a translucent ghost preview shows where it will land. Red tint = invalid placement.
- Click commits; `Esc` or right-click cancels.
- The player can pick up `MutableTopologyTag`-bearing entities and reposition them.
- Door entities can be locked (right-click → "Lock") which attaches `LockedTag` and invalidates the path cache.
- All mutations flow through `IWorldMutationApi` from WP-3.0.4. The structural change bus emits; the path cache invalidates; downstream systems pick up the new topology naturally on the next tick.

This packet does **not** ship the pickup-and-throw-with-momentum verb (UX bible §2.5 — deferred to physics packet ~3.2.x). It ships discrete drag-drop placement only. Throwing a stapler at the wall is its own packet, later.

The off-hours rhythm (UX §4.3 — work hours = NPCs disrupted by rearrangement; night = player's natural restructure window) is a *design* commitment, not a UI restriction. Build mode is freely available 24h. NPCs react with stress / irritation / schedule disruption when the player rearranges during work hours; the engine handles the consequence; the UI does not warn the player.

---

## Reference files

- `docs/c2-infrastructure/work-packets/WP-3.1.A-unity-scaffold-and-baseline-render.md` — Unity scaffold, camera, input system.
- `docs/c2-infrastructure/work-packets/WP-3.0.4-live-mutation-hardening.md` — **read fully.** `IWorldMutationApi.MoveEntity`, `SpawnStructural`, `DespawnStructural`, `AttachObstacle` (the lock verb), `DetachObstacle` (unlock), `ChangeRoomBounds`. `MutableTopologyTag` is the precondition for `MoveEntity`. The path cache invalidates automatically on emit.
- `docs/c2-content/ux-ui-bible.md` — **read §2.3 and §3.5.** Build mode toggle (B key + button); palette structure; ghost preview; off-hours rhythm; build-mode tinted overlay.
- `docs/c2-content/world-bible.md` — named anchors. The Microwave, the Fridge, the Window, the Supply Closet, the IT Closet — these are spawn-able from the palette? Or only existing-and-movable? Verify in design notes.
- `APIFramework/Components/Tags.cs` — `MutableTopologyTag`, `StructuralTag`, `LockedTag`, `CorpseTag`. The build palette respects these — only `StructuralTag` items appear in the palette; `CorpseTag` items have a special "drag corpse" affordance only available if the player has the right tool / permission.
- `APIFramework/Mutation/IWorldMutationApi.cs` — public contract.
- `ECSUnity/Assets/Scripts/Camera/CameraController.cs` (from 3.1.A) — input system extended for build-mode-only verbs.

---

## Non-goals

- Do **not** implement pickup-and-throw-with-camera-momentum. UX §2.5; future packet.
- Do **not** ship the resource-management / quartermaster supply-room model. UX §3.5 v0.1 leans unlimited palette; deferred toggle.
- Do **not** ship player-driven NPC commands ("walk Donna to the bathroom"). UX §3.4 / Q6 — environmental-only at v0.1.
- Do **not** ship multi-floor build placement. Single floor v0.1.
- Do **not** ship NPC-autonomous building / rearranging.
- Do **not** ship procedural building generation tools. Player-driven only.
- Do **not** modify `IWorldMutationApi` itself or any engine surface. The packet is Unity-side only; it consumes the API.
- Do **not** modify the 30-NPCs-at-60-FPS gate.
- Do **not** retry, recurse, or "self-heal."

---

## Design notes

### `BuildModeController`

Owns the build-mode lifecycle. MonoBehaviour on a canvas-level GameObject.

```csharp
public sealed class BuildModeController : MonoBehaviour
{
    [SerializeField] EngineHost _host;
    [SerializeField] BuildPaletteUI _palette;
    [SerializeField] BuildOverlay _overlay;
    [SerializeField] CameraController _camera;
    [SerializeField] InputAction _toggleAction;          // 'B' key + UI button

    bool _isBuildMode;
    BuildIntent _currentIntent;                          // null when no item being placed

    void Update()
    {
        if (_toggleAction.WasPressedThisFrame())
        {
            _isBuildMode = !_isBuildMode;
            _palette.SetVisible(_isBuildMode);
            _overlay.SetTinted(_isBuildMode);
        }

        if (_isBuildMode)
        {
            HandlePlacement();
            HandlePickup();
            HandleLockUnlock();
            HandleCancel();   // Esc + right-click
        }
    }
}
```

### `BuildPaletteUI`

A side-panel UI built with Unity UI Toolkit (UXML + USS). Categories from the palette catalog (`build-palette-catalog.json`):

- **Structural** — walls (1×1 tile, 2×1 tile), doors (single, double).
- **Furniture** — desks (cubicle desk, manager desk, conference table), chairs (rolling, stationary, lounge).
- **Props** — phone, computer, lamp, fan, file cabinet, plant, picture frame, water cooler.
- **Named anchors** — Microwave, Fridge (each with a unique-instance flag — only one Microwave per office; the existing one can be moved but a second cannot be spawned from the palette).

Each palette entry has an icon (CRT-style per UX §1.6), name, and drag-source behavior. Drag from the palette → world → drop.

### Ghost preview

While dragging from palette OR while a placement is staged:

- A translucent (alpha 0.5) ghost mesh appears at the cursor's world-projected position.
- Snap-to-grid: tile-grid (1×1 world-units), with optional sub-tile rotation (0°, 90°, 180°, 270°).
- Validity check: does the placement collide with existing geometry? Does it block all paths in a room? Does it overlap an NPC?
- Valid → ghost tints white; invalid → ghost tints red.
- Click → commit (calls `IWorldMutationApi.SpawnStructural`); Esc / right-click → cancel.

### Pickup mode

Click on a `MutableTopologyTag`-bearing entity in build mode → enters pickup mode for that entity. Cursor follows; ghost preview shows new position. Drop (click again) commits via `IWorldMutationApi.MoveEntity`.

NPCs are **not** pickup-able (no `MutableTopologyTag`). Corpses are pickup-able if `CorpseTag` AND a future `CorpseDragInProgressTag` mechanism (per WP-3.0.2) — at v0.1, treat corpses as pickup-able with a minor visual cue (cursor changes to a "drag corpse" icon).

### Lock / unlock doors

Right-click on a door entity in build mode → context menu: "Lock" / "Unlock" (depending on current state). Selection calls `IWorldMutationApi.AttachObstacle(doorId)` (lock) or `DetachObstacle(doorId)` (unlock). The path cache from 3.0.4 invalidates automatically.

### Build-mode visual overlay

When build mode is on:

- The world tint shifts subtly (beige-blue overlay, 0.1 alpha) signaling the mode change.
- Structural items get visible outlines (a thin line around each `StructuralTag` entity).
- The NPCs continue to do their thing — they don't pause.

### Disruption feedback

Build mode is freely usable, but disruption costs are real:

- An NPC at their desk when their desk is moved → +10 stress, +5 irritation, schedule disruption event.
- An NPC walking when their path is blocked by a newly-placed wall → re-paths via 3.0.4's cache invalidation; if no path available, lockout-detection (3.0.3) fires.
- Doors locked during work hours → NPCs unable to reach their schedule anchor → schedule miss, possible stress.

This packet does **not** add new event types; the engine systems (stress, schedule, lockout) already react. The UI does not warn the player; consequence rolls through naturally.

### Tests

- `BuildModeToggleTests.cs` — press `B`, build mode on; press again, off.
- `BuildPaletteVisibilityTests.cs` — palette shown only when build mode active.
- `BuildOverlayTintTests.cs` — world tint applied only when build mode active.
- `GhostPreviewValidPlacementTests.cs` — valid placement → ghost white; click commits via `IWorldMutationApi.SpawnStructural`.
- `GhostPreviewInvalidPlacementTests.cs` — placement on existing wall → ghost red; click does nothing.
- `PickupMutableTopologyTests.cs` — click `MutableTopologyTag` entity → pickup mode; drop commits `IWorldMutationApi.MoveEntity`.
- `PickupNonMutableRejectsTests.cs` — click NPC (no `MutableTopologyTag`) → no pickup.
- `LockUnlockDoorTests.cs` — right-click door → context menu; select Lock → `LockedTag` attached; select Unlock → removed.
- `PathInvalidationOnLockTests.cs` — lock a door → path cache invalidates (verify via cache version increment); next path query through that door fails.
- `BuildPaletteCatalogJsonTests.cs` — `build-palette-catalog.json` loads; all categories present; named-anchor uniqueness flags valid.
- `DisruptionStressIntegrationTests.cs` — NPC at desk + player moves desk → NPC `StressComponent.AcuteLevel += disruptionStressGain`.
- `BuildModeCancelTests.cs` — Esc / right-click during placement → ghost cleared; no mutation.
- `PerformanceGate30NpcWithBuildModeTests.cs` — 30 NPCs + build mode active for 60s: FPS gate preserved.
- `BuildModeDeterminismTests.cs` — scripted build sequence (place wall, lock door, move desk) on two seeds: byte-identical world state.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `ECSUnity/Assets/Scripts/BuildMode/BuildModeController.cs` | Lifecycle. |
| code | `ECSUnity/Assets/Scripts/BuildMode/BuildPaletteUI.cs` | Side-panel UI. |
| code | `ECSUnity/Assets/Scripts/BuildMode/BuildOverlay.cs` | Tinted overlay during build mode. |
| code | `ECSUnity/Assets/Scripts/BuildMode/GhostPreview.cs` | Translucent placement preview. |
| code | `ECSUnity/Assets/Scripts/BuildMode/PlacementValidator.cs` | Collision / topology check. |
| code | `ECSUnity/Assets/Scripts/BuildMode/PickupController.cs` | Click-to-pickup, click-to-drop. |
| code | `ECSUnity/Assets/Scripts/BuildMode/DoorLockContextMenu.cs` | Right-click lock/unlock. |
| code | `ECSUnity/Assets/Scripts/BuildMode/BuildIntent.cs` | Active drag/pickup state. |
| asset | `ECSUnity/Assets/UI/BuildPalette.uxml` | Palette layout. |
| asset | `ECSUnity/Assets/UI/BuildPalette.uss` | Palette styling. |
| asset | `ECSUnity/Assets/Sprites/PaletteIcons/*.png` | CRT-style icons per category. |
| code | `ECSUnity/Assets/Scripts/BuildMode/BuildPaletteCatalog.cs` | ScriptableObject. |
| asset | `ECSUnity/Assets/Settings/DefaultBuildPaletteCatalog.asset` | Defaults. |
| data | `docs/c2-content/build-palette-catalog.json` | Categories + entries. |
| code | `ECSUnity/Assets/Scripts/BuildMode/BuildModeConfig.cs` | Tunables. |
| asset | `ECSUnity/Assets/Settings/DefaultBuildModeConfig.asset` | Defaults. |
| test | `ECSUnity/Assets/Tests/Play/BuildModeToggleTests.cs` | Toggle. |
| test | `ECSUnity/Assets/Tests/Play/BuildPaletteVisibilityTests.cs` | Palette visibility. |
| test | `ECSUnity/Assets/Tests/Play/BuildOverlayTintTests.cs` | Tint. |
| test | `ECSUnity/Assets/Tests/Play/GhostPreviewValidPlacementTests.cs` | Valid placement. |
| test | `ECSUnity/Assets/Tests/Play/GhostPreviewInvalidPlacementTests.cs` | Invalid. |
| test | `ECSUnity/Assets/Tests/Play/PickupMutableTopologyTests.cs` | Pickup. |
| test | `ECSUnity/Assets/Tests/Play/PickupNonMutableRejectsTests.cs` | Pickup rejects NPC. |
| test | `ECSUnity/Assets/Tests/Play/LockUnlockDoorTests.cs` | Lock/unlock. |
| test | `ECSUnity/Assets/Tests/Play/PathInvalidationOnLockTests.cs` | Cache invalidation. |
| test | `ECSUnity/Assets/Tests/Edit/BuildPaletteCatalogJsonTests.cs` | Catalog validation. |
| test | `ECSUnity/Assets/Tests/Play/DisruptionStressIntegrationTests.cs` | Stress on disruption. |
| test | `ECSUnity/Assets/Tests/Play/BuildModeCancelTests.cs` | Cancel. |
| test | `ECSUnity/Assets/Tests/Play/PerformanceGate30NpcWithBuildModeTests.cs` | **FPS preserved.** |
| test | `ECSUnity/Assets/Tests/Play/BuildModeDeterminismTests.cs` | Determinism preserved. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.1.D.md` | Completion note. SimConfig defaults. Per-category palette content. Whether named-anchor uniqueness was enforced. Performance measurements with build mode active. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `B` key toggles build mode on/off. On-screen button does the same. | play-mode test |
| AT-02 | Build palette visible only when build mode is on. | play-mode test |
| AT-03 | World tint applied only when build mode is on. | play-mode test |
| AT-04 | Drag wall from palette → ghost appears at cursor; valid placement → white tint. | play-mode test |
| AT-05 | Drag wall onto existing wall → ghost red tint; click does not commit. | play-mode test |
| AT-06 | Click on `MutableTopologyTag` desk → pickup mode; drop calls `IWorldMutationApi.MoveEntity`; entity position updates. | play-mode test |
| AT-07 | Click on NPC (no `MutableTopologyTag`) → no pickup; cursor shows "can't" indicator. | play-mode test |
| AT-08 | Right-click on door → context menu shows Lock; click Lock → `LockedTag` attached. Right-click again → Unlock; removes tag. | play-mode test |
| AT-09 | Locking a door increments `StructuralChangeBus.TopologyVersion`; pathfinding cache empties. | play-mode test |
| AT-10 | NPC walking on a path that becomes blocked by newly placed wall → re-paths next tick (or fails-closed for lockout if no path). | play-mode test |
| AT-11 | NPC at desk when desk is moved → `StressComponent.AcuteLevel` increases by `disruptionStressGain`. | play-mode test |
| AT-12 | Esc / right-click during ghost preview → preview cleared, no mutation, build mode remains on. | play-mode test |
| AT-13 | `build-palette-catalog.json` loads; all categories present; named-anchor uniqueness valid. | edit-mode test |
| AT-14 | **Performance gate.** 30 NPCs + build mode + 5 placement operations: min ≥ 55, mean ≥ 58, p99 ≥ 50. | play-mode test |
| AT-15 | Determinism: scripted build sequence (place wall + lock door + move desk) on two seeds → byte-identical state. | play-mode test |
| AT-16 | All Phase 0/1/2/3.0.x and 3.1.A tests stay green. | regression |
| AT-17 | `dotnet build` warning count = 0; `dotnet test` all green. | build + test |
| AT-18 | Unity Test Runner: all tests pass. | unity test runner |

---

## Followups (not in scope)

- **Pickup-and-throw with camera momentum.** UX §2.5; couples to physics packet ~3.2.x.
- **Resource-management palette.** Quartermaster / supply-room variant. Future toggle.
- **NPC-autonomous building.** An NPC notices the broken chair and replaces it. Future.
- **Multi-floor build.** Stairwell-aware placement. Future.
- **Player-controllable light switches.** Click a fixture in build mode → toggle state. Future.
- **Save snapshots before build commits.** Per UX §3.4 — autosave on entering build mode. Lives in the save/load packet.
- **Build-mode tutorial overlay.** First-launch only. Out of scope per UX §6.
- **Per-archetype reaction to disruption.** The Vent loudly objects; the Hermit silently fumes. Tunable.
- **Sandbox / creative-mode build features.** Free placement (no validity check), spawn anything, no resource limits. Wired via UX §5.2 creative-mode toggle.
- **Undo/redo.** Future polish; reuses save-snapshot mechanism.
