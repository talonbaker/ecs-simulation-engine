# WP-3.0.4 Completion Note

**Outcome:** COMPLETED

**Branch:** `sonnet-wp-3.0.4`  
**Commit:** e43323a  
**Test Result:** 795/795 APIFramework tests pass; all regression tests green

---

## Summary

Implemented the structural change event system and pathfinding cache required for runtime topology mutations. The engine can now handle live world changes (entities moving, being added/removed, obstacles appearing) without paying the cost of a full pathfinding rebuild on every mutation.

### Core Components Delivered

1. **StructuralChangeBus** (`APIFramework/Systems/Spatial/StructuralChangeBus.cs`)
   - Singleton pub-sub event bus for topology mutations
   - `TopologyVersion` monotonically incremented on each emission
   - Subscribers (cache invalidator, future systems) notified synchronously

2. **StructuralChangeEvent & StructuralChangeKind** 
   - Typed event record carrying mutation details (entity, prev/cur position, room, tick)
   - 8 change kinds: EntityMoved, EntityAdded, EntityRemoved, ObstacleAttached/Detached, DoorwayAttached/Detached, RoomBoundsChanged

3. **PathfindingCache** (`APIFramework/Systems/Movement/PathfindingCache.cs`)
   - LRU cache keyed by `(fromX, fromY, toX, toY, seed, topologyVersion)`
   - Bounded at 512 entries (configurable via SimConfig)
   - Transparent to existing test surface; cache hit and miss return identical paths
   - Hit/miss counters for telemetry (LogCacheHitRateEveryTick config flag)

4. **PathfindingCacheInvalidationSystem**
   - One-line system subscribing to StructuralChangeBus
   - Clears entire cache on any emission (v0.1 strategy; region-scoped eviction is a followup)

5. **IWorldMutationApi & WorldMutationApi**
   - Public contract for runtime mutations: MoveEntity, SpawnStructural, DespawnStructural, AttachObstacle, DetachObstacle, ChangeRoomBounds
   - All mutations emit corresponding StructuralChangeEvent
   - Fail-closed: rejected mutations (e.g., moving entity without MutableTopologyTag) return false and change nothing
   - Boot-time spawns do NOT use this API (they predate bus initialization)

6. **StructuralTag & MutableTopologyTag**
   - Added to Tags.cs
   - StructuralTag: marks walls, doors, desks, obstacles — entities whose position affects pathfinding
   - MutableTopologyTag: subset of StructuralTag that can be moved at runtime (excludes walls)

7. **StructuralTaggingSystem**
   - One-shot PreUpdate system (runs once at boot, then idles)
   - Attaches StructuralTag to: obstacles, named anchors, world objects
   - Attaches MutableTopologyTag to: all non-NPC structural entities

### Modifications to Existing Systems

- **SpatialIndexSyncSystem**: Emits EntityMoved/EntityAdded/EntityRemoved on StructuralChangeBus when tagged entities change position or are registered/destroyed
- **RoomMembershipSystem**: Detects room bounds changes (via cached BoundsRect comparison) and emits RoomBoundsChanged
- **PathfindingService**: Added cache lookup before A\*; all cache operations transparent to callers
  - New constructor signature: `PathfindingService(em, width, height, config, cache, bus)`
  - All existing tests updated to pass cache and bus

### Configuration Updates

**SimConfig.cs additions:**
- `PathfindingConfig.CacheMaxEntries` (default 512)
- `PathfindingConfig.CacheEvictionStrategy` (default "wipeOnChange")
- `PathfindingConfig.LogCacheHitRateEveryTick` (default false)
- `PathfindingConfig.WarnIfCacheHitRateBelow` (default 0.50)
- New `StructuralChangeConfig` section with `EmitOnNpcMovement` (false) and `EmitOnRoomBoundsChange` (true)

**SimConfig.json updated** with new pathfinding and structuralChange sections.

### SimulationBootstrapper Changes

- Instantiates StructuralChangeBus singleton
- Instantiates PathfindingCache with configurable max entries
- Passes both to PathfindingService constructor
- Registers PathfindingCacheInvalidationSystem in Spatial phase
- Registers StructuralTaggingSystem in PreUpdate phase (after Invariants)
- Updates SpatialIndexSyncSystem and RoomMembershipSystem constructors

### Test Results

- **APIFramework.Tests:** 795/795 pass (all regression tests green)
- **Movement determinism:** Preserved — cache hit and miss produce identical paths
- **Spatial determinism:** Unaffected — cache invisible to RNG/determinism contracts
- **FactSheetStalenessTests:** Auto-regenerated `docs/engine-fact-sheet.md` to include new systems and tags

### Key Design Decisions

1. **v0.1 eviction strategy: Wipe entire cache on any structural change.** Region-scoped eviction (evict only entries whose bounding box overlaps the mutation) is documented as a followup; needs telemetry to justify the complexity.

2. **TopologyVersion in cache key:** Retained even though v0.1 clears the cache on change (makes the version redundant for correctness). Kept for debug visibility in telemetry and to support future region-scoped eviction without changing the key structure.

3. **Local tick counters:** SpatialIndexSyncSystem, RoomMembershipSystem, and WorldMutationApi use independent tick counters for emitting events. These are not coordinated with SimulationClock (which is a game-time clock, not a frame counter). Emissions are deterministic within each system and across seeded runs.

4. **Boot-time spawns bypass IWorldMutationApi:** The initial world spawn happens before StructuralChangeBus has subscribers, so it doesn't use the mutation API. The API is only for runtime mutations (player drag-drop in 3.1.D, stain spawns in 3.0.3, etc.).

5. **NPC movement is not structural:** NPCs moving across tiles does NOT emit on StructuralChangeBus. Only entities with StructuralTag emit when they move. This is enforced in SpatialIndexSyncSystem's Update method.

### Acceptance Criteria Met

- ✅ AT-01: All new types instantiate and compile.
- ✅ AT-02: StructuralChangeBus.Emit increments TopologyVersion; subscribers receive events with matching stamps.
- ✅ AT-03: PathfindingCache LRU eviction works (4-entry test holds 5 items, evicts oldest).
- ✅ AT-04: Identical queries hit cache on stable topology.
- ✅ AT-05: Cache hit and miss return identical paths (determinism preserved).
- ✅ AT-06: IWorldMutationApi.MoveEntity on StructuralTag+MutableTopologyTag updates position, emits, increments version, clears cache.
- ✅ AT-07: MoveEntity on non-MutableTopologyTag entity fails closed (returns false).
- ✅ AT-08: Spawn/despawn/obstacle/bounds mutations emit corresponding events.
- ✅ AT-09: NPC movement produces zero structural emissions; ProximityBus unaffected.
- ✅ AT-10: StructuralTaggingSystem at boot attaches tags correctly.
- ✅ AT-11: MoveEntity changes routing behavior (confirmed via test setup).
- ✅ AT-12: Cache size stays bounded after 100 mutations.
- ✅ AT-13: 5000-tick determinism test (multi-seed, scripted mutations) produces identical state.
- ✅ AT-14: All Phase 0/1/2 tests remain green; cache invisible to existing test surface.
- ✅ AT-15: Zero build warnings.
- ✅ AT-16: All tests pass.
- ✅ AT-17: Fact sheet regenerated with new systems and tags.

### Non-Goals Honored

- No player-facing drag-and-drop UI (that's WP-3.1.D).
- No stain-as-fall-risk logic (that's WP-3.0.3).
- No region-scoped cache eviction (deferred as documented).
- No new bus for non-structural changes.
- No A\* algorithm changes.
- No WorldStateDto or orchestrator modifications.
- No WP-3.0.0 system interference (confirmed disjoint file surface).
- No NuGet dependencies.
- No concurrency/locks (engine is single-threaded by design).
- No runtime LLM calls.
- No test failures or recursion on failure.
- No DateTime.Now or wall-clock timing in tests.

### Known Limitations & Future Work

1. **Region-scoped eviction** (documented followup): Current impl clears entire cache; a region-overlap check would preserve entries unaffected by the mutation. Telemetry from LogCacheHitRateEveryTick will inform cost/benefit.

2. **Per-template structural tagging** (documented followup): StructuralTaggingSystem uses predicate-based heuristics (has ObstacleTag, has AnchorObjectTag). A future content packet can add explicit `"structural": true` flags to entity templates if heuristics over/under-tag.

3. **Stain spawning and wall building** (separate packets): IWorldMutationApi contract is set; producer code lives in WP-3.0.3 and WP-3.1.D respectively.

4. **Multi-floor pathfinding** (future phase): Cache key doesn't include FloorId; when multi-floor lands, extend PathQueryKey and emit cross-floor room-bounds changes.

---

## Files Changed

**New files:**
- `APIFramework/Systems/Spatial/StructuralChangeKind.cs`
- `APIFramework/Systems/Spatial/StructuralChangeEvent.cs`
- `APIFramework/Systems/Spatial/StructuralChangeBus.cs`
- `APIFramework/Systems/Spatial/StructuralTaggingSystem.cs`
- `APIFramework/Systems/Movement/PathQueryKey.cs`
- `APIFramework/Systems/Movement/PathfindingCache.cs`
- `APIFramework/Systems/Movement/PathfindingCacheInvalidationSystem.cs`
- `APIFramework/Mutation/IWorldMutationApi.cs`
- `APIFramework/Mutation/WorldMutationApi.cs`

**Modified files:**
- `APIFramework/Components/Tags.cs` (added StructuralTag, MutableTopologyTag)
- `APIFramework/Config/SimConfig.cs` (extended MovementPathfindingConfig, added StructuralChangeConfig)
- `APIFramework/Core/SimulationBootstrapper.cs` (instantiate bus/cache, register systems)
- `APIFramework/Systems/Movement/PathfindingService.cs` (cache lookup + uncached path computation)
- `APIFramework/Systems/Spatial/SpatialIndexSyncSystem.cs` (emit structural events)
- `APIFramework/Systems/Spatial/RoomMembershipSystem.cs` (detect bounds changes)
- `SimConfig.json` (new configuration sections)
- `docs/engine-fact-sheet.md` (regenerated)
- 7 test harnesses (updated to pass new parameters)

**Total lines added:** ~830  
**Total warnings:** 0  
**Total errors after fix:** 0
