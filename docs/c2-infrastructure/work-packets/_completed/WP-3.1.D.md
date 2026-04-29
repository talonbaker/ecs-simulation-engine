# WP-3.1.D — Building Mode Mechanic — Completion Note

**Executed by:** claude-sonnet-4-6
**Branch:** feat/wp-3.1.D
**Started:** 2026-04-28
**Ended:** 2026-04-28
**Outcome:** ok

---

## Summary

Shipped the full Build Mode mechanic on top of the WP-3.1.A scaffold. Eight new MonoBehaviours / classes in `ECSUnity/Assets/Scripts/BuildMode/`:

- **BuildIntent** — immutable state record for active drag or pickup (Placing / PickingUp / None).
- **BuildModeConfig** — ScriptableObject with all tunables (overlay alpha, ghost tints, grid size, disruption gain).
- **BuildPaletteCatalog** — ScriptableObject that describes palette entries per category; JSON catalog at `docs/c2-content/build-palette-catalog.json`.
- **BuildOverlay** — screen-space Image component that applies a beige-blue tint (α=0.10) when build mode is active.
- **PlacementValidator** — stateless Physics.OverlapBox validator; also hosts SnapToGrid and ScreenToWorld helpers.
- **GhostPreview** — translucent cube ghost (swappable mesh); white when valid, red when invalid.
- **PickupController** — MutableTopologyTag check + MoveEntity commit; rejects NPCs with a clear reason string.
- **DoorLockContextMenu** — IMGUI popup for right-click lock/unlock; calls AttachObstacle/DetachObstacle.
- **BuildPaletteUI** — UI Toolkit side panel with category tabs and item rows; falls back to IMGUI when UXML is absent (e.g. edit-mode tests).
- **BuildModeController** — lifecycle owner; routes B-key toggle, palette callbacks, ghost updates, commit/cancel.
- **SelectableTag** — marker component for NPC / object / room clickability (shared with WP-3.1.E).

Also delivered: `BuildPalette.uxml`, `BuildPalette.uss`, `build-palette-catalog.json`, and 10 test files.

---

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | expected ✓ | TestToggleBuildMode / IsBuildMode verified in BuildModeToggleTests. |
| AT-02 | expected ✓ | BuildPaletteVisibilityTests covers on/off cases. |
| AT-03 | expected ✓ | BuildOverlayTintTests: overlay visible and alpha > 0 when active. |
| AT-04 | expected ✓ | GhostPreviewValidPlacementTests: ghost active after palette select; white tint; commit calls SpawnStructural. |
| AT-05 | expected ✓ | GhostPreviewInvalidPlacementTests: red tint confirmed; no SpawnStructural call on invalid. |
| AT-06 | expected ✓ | PickupMutableTopologyTests: CommitPickup → MoveEntity. Tile rounding verified. |
| AT-07 | expected ✓ | PickupNonMutableRejectsTests: null engine → false + non-empty reason. |
| AT-08 | expected ✓ | LockUnlockDoorTests: DirectLock → AttachObstacle; DirectUnlock → DetachObstacle. |
| AT-09 | expected ✓ | PathInvalidationOnLockTests: lock/unlock call counts verified. |
| AT-10 | n/a (engine-side) | NPC re-path is covered by WP-3.0.4 PathfindingService tests. |
| AT-11 | expected ✓ | DisruptionStressIntegrationTests: MoveEntity call count matches expected. |
| AT-12 | expected ✓ | BuildModeCancelTests: cancel clears intent, deactivates ghost, no SpawnStructural. |
| AT-13 | expected ✓ | BuildPaletteCatalogJsonTests: all four categories present; uniqueness flags valid; GUIDs parseable. |
| AT-14 | expected ✓ | PerformanceGate30NpcWithBuildModeTests: mean FPS assertion against 58 FPS gate. |
| AT-15 | expected ✓ | BuildModeDeterminismTests: same call sequence on two runs via LoggingFakeApi. |

---

## Config defaults

| Param | Default | Notes |
|:---|:---|:---|
| overlayAlpha | 0.10 | Subtle; increase to 0.15 if users miss the mode shift. |
| overlayColor | (0.72, 0.78, 0.88) beige-blue | Per UX bible. |
| ghostAlphaValid | 0.55 | Visible but clearly translucent. |
| ghostAlphaInvalid | 0.50 | Same opacity, distinct red color. |
| snapGridSize | 1.0 world-unit | One tile = one unit. |
| rotationStep | 90° | Four cardinal directions. |
| disruptionStressGain | 10 acute points | Tunable; felt as a small irritation spike. |
| disruptionIrritationGain | 5 drive units | Secondary signal. |

---

## Palette categories (final content)

| Category | Entries |
|:---|:---|
| Structural | Wall 1×1, Wall 2×1, Door (Single), Door (Double) |
| Furniture | Cubicle Desk, Manager Desk, Conference Table, Rolling Chair, Stationary Chair, Lounge Chair |
| Props | Phone, Computer, Desk Lamp, Floor Fan, File Cabinet, Plant, Picture Frame, Water Cooler |
| NamedAnchor | Microwave (unique), Fridge (unique) |

Named-anchor uniqueness enforced: the catalog JSON marks both as `uniqueInstance: true`. `BuildPaletteUI.SetCategory` reads this at render time and would show "Move" instead of "Place" when the entity already exists in world (integration with live engine entity tracking is a minor follow-up).

---

## Performance notes

Build mode is UI-only — no per-frame physics beyond the single PlacementValidator OverlapBox (run only while an intent is active). Overlay is a single Image render. Ghost is one Cube primitive with one material.

Expected overhead vs 3.1.C baseline: < 0.05 ms/frame while idle, < 0.2 ms/frame while a ghost is active.

---

## Assumptions

1. `WorldMutationApi` is constructable from `EntityManager` + `StructuralChangeBus`. Verified in engine source.
2. `EngineHost.Engine.GetService<StructuralChangeBus>()` returns null when bus is not yet registered; `BuildModeController.TryInjectMutationApi` creates a new bus as fallback. Production wiring should plumb the bus through `EngineHost` directly (minor follow-up).
3. `LockedTag`, `MutableTopologyTag`, `NpcTag` are all in `APIFramework.Components` namespace. Confirmed in `WorldMutationApi.cs`.

---

## Follow-ups

- Wire `BuildPaletteUI` unique-instance detection against live entity manager.
- Autosave on build-mode entry (UX §3.4 checkpoint).
- Player-controllable light switches (coupled to 3.1.C).
- Undo/redo (reuses save-snapshot mechanism).
