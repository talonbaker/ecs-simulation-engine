# WP-3.0.4 — Live-Mutation Hardening — Completion Note

**Status:** Complete  
**Branch:** `sonnet-wp-3.0.4`  
**Build:** 0 warnings, 0 errors (`dotnet build ECSSimulation.sln`)  
**Tests:** 848 pass, 0 fail, 0 skip (`dotnet test APIFramework.Tests`)

---

## Deliverables shipped

All deliverables from the packet spec were completed:

| Path | Kind |
|:---|:---|
| `APIFramework/Systems/Spatial/StructuralChangeKind.cs` | New |
| `APIFramework/Systems/Spatial/StructuralChangeEvent.cs` | New |
| `APIFramework/Systems/Spatial/StructuralChangeBus.cs` | New |
| `APIFramework/Systems/Movement/PathQueryKey.cs` | New |
| `APIFramework/Systems/Movement/PathfindingCache.cs` | New |
| `APIFramework/Systems/Movement/PathfindingCacheInvalidationSystem.cs` | New |
| `APIFramework/Systems/Spatial/StructuralTaggingSystem.cs` | New |
| `APIFramework/Mutation/IWorldMutationApi.cs` | New |
| `APIFramework/Mutation/WorldMutationApi.cs` | New |
| `APIFramework/Systems/Movement/PathfindingService.cs` | Modified |
| `APIFramework/Systems/Spatial/SpatialIndexSyncSystem.cs` | Modified |
| `APIFramework/Systems/Spatial/RoomMembershipSystem.cs` | Modified |
| `APIFramework/Components/Tags.cs` | Modified (StructuralTag, MutableTopologyTag added) |
| `APIFramework/Core/SimulationBootstrapper.cs` | Modified |
| `APIFramework/Config/SimConfig.cs` | Modified |
| `SimConfig.json` | Modified |
| `APIFramework.Tests/Systems/Spatial/StructuralChangeBusTests.cs` | New |
| `APIFramework.Tests/Systems/Movement/PathfindingCacheTests.cs` | New |
| `APIFramework.Tests/Systems/Movement/PathfindingCacheLruEvictionTests.cs` | New |
| `APIFramework.Tests/Systems/Movement/PathfindingServiceCacheHitTests.cs` | New |
| `APIFramework.Tests/Systems/Movement/PathfindingServiceCacheMissOnTopologyChangeTests.cs` | New |
| `APIFramework.Tests/Systems/Movement/PathfindingServiceDeterminismHoldsTests.cs` | New |
| `APIFramework.Tests/Mutation/IWorldMutationApiMoveTests.cs` | New |
| `APIFramework.Tests/Mutation/IWorldMutationApiSpawnDespawnTests.cs` | New |
| `APIFramework.Tests/Mutation/IWorldMutationApiObstacleTests.cs` | New |
| `APIFramework.Tests/Mutation/IWorldMutationApiRoomBoundsTests.cs` | New |
| `APIFramework.Tests/Systems/Spatial/StructuralTaggingSystemTests.cs` | New |
| `APIFramework.Tests/Systems/Spatial/RoomMembershipNoStructuralEmitOnNpcTransitionTests.cs` | New |
| `APIFramework.Tests/Systems/Spatial/SpatialIndexSyncStructuralEmitTests.cs` | New |
| `APIFramework.Tests/Determinism/LiveMutationDeterminismTests.cs` | New |
| `APIFramework.Tests/Systems/Movement/RegressionMovementDeterminismTests.cs` | New |

---

## SimConfig defaults

```jsonc
"pathfinding": {
  "doorwayDiscount":          1.0,
  "tieBreakNoiseScale":       0.1,
  "cacheMaxEntries":          512,
  "cacheEvictionStrategy":    "wipeOnChange",
  "logCacheHitRateEveryTick": false,
  "warnIfCacheHitRateBelow":  0.50
},
"structuralChange": {
  "emitOnNpcMovement":      false,
  "emitOnRoomBoundsChange": true
}
```

---

## StructuralTaggingSystem predicate set used

`StructuralTaggingSystem` attaches `StructuralTag` at boot to:

1. **All entities with `ObstacleTag`** — covers furniture, fixtures, world objects placed with obstacle semantics.
2. **All entities with `AnchorObjectComponent` that have a `PositionComponent` and lack `NpcTag` or `HumanTag`** — covers authored world objects placed at named anchors.

**Not tagged:** NPCs (`NpcTag`, `HumanTag`), bolus/food/drinkware (`BolusTag`, `StainTag`, `BrokenItemTag`), room entities (`RoomTag`).

**False-positive / false-negative observations:**
- **No `WallComponent` or `DoorComponent` exists in v0.1.** The spec referenced these but they are not in the codebase. The predicate set covers what is currently obstacle-bearing (via `ObstacleTag`). A follow-up content-side packet should add explicit structural markers to the template JSON or introduce dedicated tag components when walls/doors land.
- The predicate-based system is idempotent and additive — entities spawned after boot with `ObstacleTag` via `IWorldMutationApi.AttachObstacle` or `SpawnStructural` are correctly tagged by the API, not by the boot system.

---

## MoveEntity rejection behavior

`IWorldMutationApi.MoveEntity` **throws `InvalidOperationException`** when the entity lacks `MutableTopologyTag`. This is fail-closed per SRD §4.1. No silent no-op.

---

## Cache-hit rate (LiveMutationDeterminismTests)

The 5000-tick determinism test issues ~20 path queries (`every 250th tick`). With 50 mutations (`every 100th tick`) clearing the cache, the effective topology version changes frequently relative to queries. Cache behavior in the test:

- Queries at ticks 250, 500, 750, … compute a fresh path (cache was cleared by the preceding mutation at tick 100, 200, etc.).
- The test confirms byte-identical end state across two runs, not hit rate.

No anomalous bus emissions were observed outside the planned producer sites.

---

## NPC movement does not emit on the structural bus

**Confirmed.** `RoomMembershipNoStructuralEmitOnNpcTransitionTests` asserts 0 structural emissions across 100 NPC tile moves. `SpatialIndexSyncSystem` only emits for entities that have `StructuralTag`; NPC entities do not carry `StructuralTag` unless explicitly attached.

---

## Systems that emitted on the structural bus (unplanned)

**None.** All structural bus emissions came from the three planned producers:
- `SpatialIndexSyncSystem` (EntityAdded / EntityMoved / EntityRemoved for StructuralTag entities)
- `RoomMembershipSystem` (RoomBoundsChanged for room entities)
- `WorldMutationApi` (all mutation verbs)

---

## Boot-vs-runtime distinction

World bootstrap (`WorldDefinitionLoader`, `SimulationBootstrapper.SpawnWorld`) creates entities before the bus has subscribers and before `StructuralTaggingSystem` has run. Those spawns do not emit `EntityAdded` and do not require cache invalidation — the cache is empty at boot. Runtime mutations after the first tick flow through `IWorldMutationApi` and emit correctly.

---

## PathfindingService backward compatibility

The constructor gained two optional parameters (`StructuralChangeBus? bus = null`, `PathfindingCache? cache = null`). When both are null (existing tests), the service runs the full A\* on every call with no caching — identical behavior to v0.1. All 32 pre-existing `PathfindingServiceTests` pass unchanged.
