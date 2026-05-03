# WP-4.0.C — Build Prop Footprint Foundation

> **STATUS:** SHIPPED to staging 2026-05-02. Retained because pending packets depend on this spec: WP-4.0.G.

> **Wave 1 of the Phase 4.0.x foundational polish wave.** Per the 2026-05-02 brief restructure: BUG-001 (prop-on-prop displacement) was deferred to "build mode v2" because the underlying engine has no concept of per-prop occupancy. This packet ships the foundation — `BuildFootprintComponent` as a first-class engine concept — without yet fixing BUG-001. WP-4.0.G (the actual BUG-001 fix) will consume this substrate. Track 1.5 packet (engine + minor Unity reads).

**Tier:** Sonnet
**Depends on:** Existing build mode (`DraggableProp.cs`, `DragHandler.cs`), `IWorldMutationApi`. No render changes.
**Parallel-safe with:** WP-4.0.A (Unity render-only, disjoint surface), WP-4.0.B (engine spatial, disjoint from build mode), WP-PT.* (does not modify scenes).
**Timebox:** 100 minutes
**Budget:** $0.50

---

## Goal

Engine-side props (chairs, desks, monitors, fridges, plants — anything placeable via build mode) currently have no first-class concept of *what tile area they occupy*. The build mode's `DragHandler.GetSurfaceYAtXZ` works around this by querying ray-cast geometry, which is what produced BUG-001 (a small prop's top surface gets read as the floor for a larger prop being placed on top).

This packet introduces **`BuildFootprintComponent`** as the engine's canonical "this prop occupies these tiles, has these surface-heights, and has these stack-on-top affordances" data. Build mode reads it (instead of geometry-querying); the runtime engine can use it for pathfinding awareness; future packets can use it for footprint-aware drop logic.

This packet does **not** fix BUG-001. WP-4.0.G is the BUG-001 fix; it dispatches after this packet merges and consumes `BuildFootprintComponent` to do prop-on-prop displacement properly.

After this packet:
- Every placeable prop type has a `BuildFootprintComponent` declaring its tile-occupancy, surface-heights, and stack-on-top compatibility.
- A footprint catalog JSON declares the per-prop-type defaults (Sonnet authors initial entries for cube wall, door, desk, chair, monitor, computer, printer per UX bible §3.5 starting palette).
- Build mode's `DragHandler` reads `BuildFootprintComponent` for placement validity (overlap detection works against actual footprints, not just bounding boxes).
- xUnit tests verify: footprint round-trip; footprint-vs-footprint overlap detection; per-prop-type catalog loads.
- BUG-001's symptom may or may not be incidentally fixed by this packet; **do not optimize for fixing BUG-001 here**. WP-4.0.G owns the fix.

---

## Reference files

- `docs/known-bugs.md` — read BUG-001 in full. Note the deferred-because rationale: footprint tracking + multi-prop stacking + undo/redo. This packet is the *footprint tracking* slice.
- `docs/c2-infrastructure/MOD-API-CANDIDATES.md` — read MAC-007 (IWorldMutationApi), MAC-011 (BuildFootprintComponent — this packet introduces).
- `docs/c2-content/ux-ui-bible.md` §3.5 — the v0.2 starting palette (cube walls, doors, desks, chairs, monitors / desktop computers, printers). Each gets a footprint entry.
- `docs/PHASE-4-KICKOFF-BRIEF.md` — read the 2026-05-02 restructure section.
- `ECSUnity/Assets/Scripts/Build/DraggableProp.cs` — read for the existing drag/drop wiring.
- `ECSUnity/Assets/Scripts/Build/DragHandler.cs` — read in full. **Especially `GetSurfaceYAtXZ`** — the BUG-001 root-cause site. This packet's read-side enables the future fix; do not change `GetSurfaceYAtXZ` here unless the change is purely additive (read footprint when available, fall back to geometry).
- `APIFramework/World/IWorldMutationApi.cs` — the structural mutation contract. Footprint-aware adds/moves use this.
- `docs/c2-infrastructure/work-packets/_completed-specs/WP-3.0.4-live-mutation-hardening.md` — read for context on `StructuralChangeBus` and how mutations propagate.

---

## Non-goals

- Do **not** fix BUG-001 in this packet. WP-4.0.G owns it.
- Do **not** add multi-prop stacking rules ("a vase can sit on a desk; a desk cannot sit on a vase"). WP-4.0.G or a successor handles this.
- Do **not** add undo/redo. Future packet.
- Do **not** modify render-side geometry or visual representation. Footprint is purely a data concept.
- Do **not** add user-facing UI for footprint visualization in build mode. Future polish.
- Do **not** ship player-facing build palette changes. UX bible §3.5 commits to the starting palette; this packet ships *data* declaring the footprint of each item, not the palette UI.
- Do **not** modify pathfinding to consume footprints in this packet. Future packet, after footprints are stable.
- Do **not** support non-rectangular footprints (e.g., L-shaped desks). v0.2 ships rectangular only; document the constraint.
- Do **not** add per-instance footprint overrides. Per-prop-type is the v0.2 granularity.

---

## Design notes

### `BuildFootprintComponent`

```csharp
public class BuildFootprintComponent : IComponent {
    // Tile-based footprint relative to the prop's anchor tile.
    // (0,0) is the anchor. Width/depth in tiles.
    public int WidthTiles { get; set; }
    public int DepthTiles { get; set; }

    // Heights at the bottom and top surfaces, in world units (typically meters).
    // BottomHeight = how far the prop's bottom sits above ground (0 for floor-resting).
    // TopHeight = the top surface other props could rest on, measured from BottomHeight base.
    // For a 0.75m-tall desk sitting on the floor: BottomHeight=0, TopHeight=0.75.
    // For a 0.30m monitor sitting on top of that desk: it would have BottomHeight=0,
    //   TopHeight=0.30, and be placed at the desk's TopHeight.
    public float BottomHeight { get; set; }
    public float TopHeight { get; set; }

    // Stack-on-top affordance. If true, other props can rest on this prop's top surface.
    // Floor: not a prop. Desk: true. Chair: usually false. Monitor: false (unless cat).
    public bool CanStackOnTop { get; set; }

    // Optional category tag for richer matching in future stacking-rule packets.
    // Examples: "Furniture", "DeskAccessory", "WallMounted". Defaults to empty.
    public string FootprintCategory { get; set; }
}
```

### Footprint catalog JSON

`docs/c2-content/build/prop-footprints.json`:

```jsonc
{
  "schemaVersion": "0.1.0",
  "propFootprints": [
    {
      "propTypeId": "cube-wall",
      "widthTiles": 1, "depthTiles": 1,
      "bottomHeight": 0.0, "topHeight": 2.5,
      "canStackOnTop": false,
      "footprintCategory": "Wall"
    },
    {
      "propTypeId": "door",
      "widthTiles": 1, "depthTiles": 1,
      "bottomHeight": 0.0, "topHeight": 2.2,
      "canStackOnTop": false,
      "footprintCategory": "Wall"
    },
    {
      "propTypeId": "desk",
      "widthTiles": 2, "depthTiles": 1,
      "bottomHeight": 0.0, "topHeight": 0.75,
      "canStackOnTop": true,
      "footprintCategory": "Furniture"
    },
    {
      "propTypeId": "chair",
      "widthTiles": 1, "depthTiles": 1,
      "bottomHeight": 0.0, "topHeight": 0.45,
      "canStackOnTop": false,
      "footprintCategory": "Furniture"
    },
    {
      "propTypeId": "monitor",
      "widthTiles": 1, "depthTiles": 1,
      "bottomHeight": 0.0, "topHeight": 0.40,
      "canStackOnTop": false,
      "footprintCategory": "DeskAccessory"
    },
    {
      "propTypeId": "computer",
      "widthTiles": 1, "depthTiles": 1,
      "bottomHeight": 0.0, "topHeight": 0.45,
      "canStackOnTop": false,
      "footprintCategory": "DeskAccessory"
    },
    {
      "propTypeId": "printer",
      "widthTiles": 1, "depthTiles": 1,
      "bottomHeight": 0.0, "topHeight": 0.35,
      "canStackOnTop": true,
      "footprintCategory": "DeskAccessory"
    }
  ]
}
```

The values above are reasonable starting estimates; Sonnet does not need to spend time fine-tuning. They will be iterated when WP-4.0.G uses them.

### `BuildFootprintCatalog` service

```csharp
public class BuildFootprintCatalog {
    public BuildFootprintCatalog Load(string jsonPath); // factory
    public BuildFootprintComponent? GetByPropType(string propTypeId);
    public IReadOnlyList<string> AllPropTypeIds { get; }
}
```

Loaded at boot. Spawn-phase initializer attaches `BuildFootprintComponent` instances to placeable props by their `PropTypeId` (which presumably exists today on the prop entity; if not, add a `PropTypeIdComponent` as part of this packet — small additive change).

### Footprint-vs-footprint overlap detection

Pure utility function (not a system):

```csharp
public static class FootprintGeometry {
    public static bool Overlaps(
        Vector2Int anchorA, BuildFootprintComponent footprintA,
        Vector2Int anchorB, BuildFootprintComponent footprintB);
    // Considers tile-level XZ overlap only. Heights are not used here.

    public static bool CanStackOn(
        BuildFootprintComponent topProp,
        BuildFootprintComponent bottomProp);
    // True iff bottomProp.CanStackOnTop AND topProp's footprint
    // fits within bottomProp's footprint at the XZ level.
}
```

WP-4.0.G will use these for prop-on-prop displacement; this packet just ships them.

### Build mode read-side wiring (minimal)

`DragHandler.cs` in Unity gets a thin extension: when reading a target tile's surface height, it first queries the engine for any prop whose footprint covers that tile and whose `TopHeight + BottomHeight` is the highest at that location; falls back to existing geometry-cast logic if no footprint data is available. This is a **purely additive** change — no behavior change unless a prop with footprint data is at the tile.

This is enough to *not regress* anything. BUG-001 is not fixed because the displacement logic still does the wrong thing; but the surface-height query is now correct, which is the precondition.

### Schema bump

`world-state.schema.json` v0.5.x → v0.5.x+1. Additive: `entities[].buildFootprint?` optional object. Per the additive-minor rule.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/BuildFootprintComponent.cs` | Component definition. |
| code | `APIFramework/Build/BuildFootprintCatalog.cs` | Catalog loader / lookup. |
| code | `APIFramework/Build/FootprintGeometry.cs` | Overlap + can-stack-on utilities. |
| code | `APIFramework/Systems/Build/BuildFootprintInitializerSystem.cs` | Spawn-phase initializer attaching footprints to props. |
| code | `APIFramework/Systems/SimulationBootstrapper.cs` (modification) | Register the initializer. |
| code | `APIFramework/Components/PropTypeIdComponent.cs` (NEW IF MISSING) | Small additive: a string id on props. Verify whether already exists; do not duplicate. |
| code | `ECSUnity/Assets/Scripts/Build/DragHandler.cs` (additive modification) | Surface-height query consults footprint data first; falls back to geometry. **No other behavior change.** |
| data | `docs/c2-content/build/prop-footprints.json` | Catalog of v0.2 starting-palette footprints. |
| code | `APIFramework/Projection/WorldStateDto.cs` (modification) | Round-trip `BuildFootprintComponent`. |
| schema | `Warden.Contracts/SchemaValidation/world-state.schema.json` (modification) | Additive minor bump. |
| test | `APIFramework.Tests/Build/BuildFootprintCatalogTests.cs` | JSON load + lookup. |
| test | `APIFramework.Tests/Build/FootprintGeometryTests.cs` | Overlap, can-stack-on, edge cases (1x1 inside 2x1, exact match, no overlap). |
| test | `APIFramework.Tests/Systems/Build/BuildFootprintInitializerTests.cs` | Initializer attaches correct footprint by prop type. |
| test | `APIFramework.Tests/Build/BuildFootprintRoundtripTests.cs` | Component round-trips via WorldStateDto. |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` | Confirm MAC-011 entry post-implementation. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | All seven props in v0.2 starting palette have catalog entries. | unit-test |
| AT-02 | All catalog values in valid range (widthTiles ≥ 1, depthTiles ≥ 1, heights ≥ 0). | unit-test |
| AT-03 | `FootprintGeometry.Overlaps` returns correct result across an exhaustive small-case matrix (same tile, adjacent tile, overlap on one axis, full overlap, partial overlap, contained). | unit-test |
| AT-04 | `FootprintGeometry.CanStackOn` returns correct result for: monitor on desk (true), desk on chair (false — chair canStackOnTop = false), printer on desk (true). | unit-test |
| AT-05 | Initializer attaches `BuildFootprintComponent` to a spawned prop by its `propTypeId`. | unit-test |
| AT-06 | Spawned prop without a catalog match logs a warning but does not crash; component is left unattached. | unit-test |
| AT-07 | `BuildFootprintComponent` round-trips through `WorldStateDto` JSON. | unit-test |
| AT-08 | `DragHandler.GetSurfaceYAtXZ` returns footprint-aware height when a prop with footprint data is present at the queried tile, and falls back to geometry otherwise. | unit-test (mocked engine) |
| AT-09 | BUG-001's symptom is **not necessarily fixed** by this packet (the broken displacement logic is unchanged); existing build-mode tests stay green; no regression. | regression |
| AT-10 | All Phase 0/1/2/3 tests stay green. | regression |
| AT-11 | `dotnet build` warning count = 0; `dotnet test` all green. | build + test |

---

## Mod API surface

This packet introduces **MAC-011: BuildFootprintComponent + footprint-aware drop**. See `docs/c2-infrastructure/MOD-API-CANDIDATES.md`.

The catalog JSON shape and the component shape are intended to be modder-extensible: a modder adding a new prop type (a couch, a vending machine, a cat tower) appends an entry to the catalog and the initializer wires it up automatically. The JSON-driven pattern is consistent with MAC-001 (per-archetype tuning) and MAC-005 (SimConfig sections), reinforcing the "data over code for content" pattern.

`FootprintGeometry` utilities are pure functions; modders can call them freely from custom build-tool code via the future Mod API.

The component intentionally does NOT include rotation. Rotated props are a future concern; v0.2 ships axis-aligned footprints only. When rotation lands, it becomes a separate component (`PropRotationComponent`) that the geometry utilities consume — not a property of footprint itself.

---

## Followups (not in scope)

- **WP-4.0.G — Build mode v2 / BUG-001 fix.** Consumes this packet's substrate. Implements proper prop-on-prop displacement, multi-prop stacking rules, and undo/redo.
- Non-rectangular footprints (L-shaped desks, corner units). Future depth.
- Rotated footprints. Future depth.
- Pathfinding awareness of footprints (NPCs path around occupied tiles, not just walls). Future polish.
- Per-instance footprint overrides (a chair with its arms folded down has a smaller footprint). Future depth.
- Visual footprint preview in build mode (a tile-grid overlay showing what will be occupied). Future build-mode UX.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: NOT NEEDED

This is a Track 1.5 (engine + minor additive Unity) packet. The Unity-side change is purely additive to `DragHandler.GetSurfaceYAtXZ` (footprint-data path; falls back to existing geometry path); no behavior change is observable without props that carry footprint data. All verification is handled by the xUnit test suite.

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm you are in a dedicated worktree at `.claude/worktrees/sonnet-wp-4.0.c/` on branch `sonnet-wp-4.0.c` based on recent `origin/staging`. If anything is wrong, stop and notify Talon.
1. Implement the spec.
2. Add or update xUnit tests to cover all acceptance criteria.
3. Run `dotnet test` from the repo root. Must be green.
4. Run `dotnet build` to confirm no warnings introduced.
5. Stage all changes including the self-cleanup deletion (see below).
6. Commit on the worktree's feature branch.
7. Push the branch and open a PR against `staging`.
8. Stop. Do **not** merge. Talon merges after review.

### Cost envelope (1-5-25 Claude army)

Target: **$0.50–$1.20** per packet. If costs approach the upper bound without acceptance criteria nearing completion, **escalate to Talon** by stopping work and committing a `WP-4.0.C-blocker.md` note.

Cost-discipline:
- Read reference files at most once per session.
- Don't try to fix BUG-001 in this packet. The footprint substrate is enough; the fix is WP-4.0.G's job.
- The catalog values are reasonable estimates, not finely tuned. Don't iterate them; WP-4.0.G will iterate.

### Self-cleanup on merge

The active `docs/c2-infrastructure/work-packets/` directory should contain only **pending** packets.

Before opening the PR:

1. **Check downstream dependents:**
   ```bash
   git grep -l "WP-4.0.C" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```

2. **If the grep returns the pending `WP-4.0.G` packet (likely — that's the BUG-001 fix that consumes this substrate)**: leave the spec file in place. Add:
   ```markdown
   > **STATUS:** SHIPPED to staging YYYY-MM-DD. Retained because pending packets depend on this spec: WP-4.0.G.
   ```
   Add `Self-cleanup: spec retained, dependents: WP-4.0.G.` to the commit message.

3. **If the grep returns no results**: include `git rm docs/c2-infrastructure/work-packets/WP-4.0.C-build-footprint-foundation.md` in the staging set. Add `Self-cleanup: spec file deleted, no pending dependents.` to the commit message.

4. **Do not touch** files under `_completed/` or `_completed-specs/`.

5. The git history (commit message + PR body) is the historical record.
