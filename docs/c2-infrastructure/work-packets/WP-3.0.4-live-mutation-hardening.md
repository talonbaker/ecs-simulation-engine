# WP-3.0.4 — Live-Mutation Hardening (Topology-Dirty Signal + Pathfinding Cache)

**Tier:** Sonnet
**Depends on:** WP-1.1.A (spatial layer), WP-1.3.A (movement quality / pathfinding), WP-1.7.A (world definition)
**Parallel-safe with:** WP-3.0.0 (death foundation — disjoint file surface; only `SimulationBootstrapper.cs` overlap, "keep both" merge; `Tags.cs` exclusive to this packet, neither modifies the other's components)
**Timebox:** 120 minutes
**Budget:** $0.50

---

## Goal

The pathfinding service rebuilds its obstacle and doorway sets on every `ComputePath` call. That is fine when nothing moves — and at v0.1, nothing did, because the world was authored at boot and never restructured. Phase 3.1.D (Unity drag-and-drop building mode) and Phase 3.0.3 (slip-and-fall, where a hazard appears mid-game) both require the engine to handle **structural change at runtime**: a desk moves, a door is added, a stain creates a fall risk on a tile that didn't have one a tick ago.

Two things must hold for that to work without paying the rebuild cost on every path query:

1. The engine must **publish a signal** when topology changes — a single source of truth that says "the obstacle/doorway/walkability set is no longer what it was last tick."
2. Path computation must **cache aggressively** while topology is stable, and **invalidate cleanly** when the signal fires. A 30-NPC scene at 60 FPS will issue many path queries per second; rebuilding obstacle sets on each is a measurable cost (and the foundational reason the kickoff brief calls live mutation a 3.0 prerequisite for 3.1).

This packet ships:

1. A `StructuralChangeKind` enum and `StructuralChangeEvent` record covering: entity added to a tile, entity removed from a tile, entity moved between tiles, wall/door/obstacle component added or removed, room bounds changed.
2. A `StructuralChangeBus` (parallel to the existing `ProximityEventBus` and `NarrativeEventBus`) that producers emit to and consumers subscribe to.
3. A `TopologyVersion` monotonic counter, owned by the bus, incremented on every emission. Read-only externally.
4. A `PathfindingCache` — keyed by `(fromX, fromY, toX, toY, seed, topologyVersion)`, value `IReadOnlyList<(int X, int Y)>`. Bounded LRU; size configurable (default 512). Wired into `PathfindingService.ComputePath` so identical queries against the same topology version hit the cache.
5. A `PathfindingCacheInvalidationSystem` that subscribes to the bus and, on emission, drops the entire cache (or, in the optimisation followup, drops only entries whose tile range overlaps the changed region — see Followups; v0.1 is a clean wipe).
6. Producers wired into the existing systems that legitimately mutate topology: `RoomMembershipSystem` (when an entity's room membership changes), `MovementSystem` (when a `StructuralTag`-marked entity changes position), and a public mutation API surface `IWorldMutationApi` that 3.1.D's Unity glue (and tests, and 3.0.3's stain spawner) can call.

After this packet, the engine has a clean substrate for **runtime topology change**: producers publish, consumers subscribe, the pathfinding service stays fast on stable topology and correct under change. Unity drag-and-drop and emergent mid-game hazards (stains, blocked doors, moved desks) plug into a documented seam instead of poking at internals.

This packet is **engine-internal at v0.1**. The wire format is unchanged. The cost ledger and orchestrator are unaffected.

---

## Reference files

- `docs/PHASE-3-KICKOFF-BRIEF.md` — Phase 3 commitments; especially the "framerate is engineering discipline from packet 1" axiom and the sentence "Required for Unity drag-and-drop."
- `docs/c2-infrastructure/PHASE-2-HANDOFF.md` §6.0 (Phase 3.0.x backlog and ordering).
- `docs/c2-infrastructure/00-SRD.md` §4.1 (fail-closed escalation), §4.2 (determinism), §8.6 (visual target — engine exposes spatial structure for AI tier reasoning), §8.7 (engine host-agnostic).
- `docs/c2-content/aesthetic-bible.md` priority 3 (movement quality) — the path produced after a structural change must still feel "natural" (the tie-break noise pattern must survive cache invalidation).
- `APIFramework/Systems/Movement/PathfindingService.cs` — **the central modification target.** `ComputePath` currently rebuilds obstacle and doorway sets on every call. The packet adds a memoization layer in front of A\* with `(query, topologyVersion)` keys; on cache miss, the existing A\* logic runs unchanged.
- `APIFramework/Systems/Movement/PathfindingTriggerSystem.cs` — read for how paths are requested per-NPC. The cache lives below this, in the service.
- `APIFramework/Systems/Spatial/SpatialIndexSyncSystem.cs` — already maintains a per-position spatial index. Becomes a **producer** of `StructuralChangeEvent` when an entity that carries `StructuralTag` (new) changes its tile position. Most NPCs are *not* tagged structural — only walls, doors, desks, and the like.
- `APIFramework/Systems/Spatial/RoomMembershipSystem.cs` — already fires `RoomMembershipChanged` on `ProximityEventBus`. **Add a parallel emit on `StructuralChangeBus`** when the *room entity itself* changes bounds (rare, but when 3.1.D adds a wall in a room, the room may resize). NPC room transitions do **not** emit on the structural bus — they're proximity-level, not topology-level.
- `APIFramework/Systems/MovementSystem.cs` — read for how `PositionComponent` is updated. NPCs moving through tiles is **not** a structural change; only `StructuralTag`-bearing entities are. Document the distinction in the packet.
- `APIFramework/Systems/Spatial/ProximityEventBus.cs` — pattern template for `StructuralChangeBus`. Mirror the API surface (subscribe, emit, drain).
- `APIFramework/Systems/Narrative/NarrativeEventBus.cs` — the other extant bus. Pattern reference.
- `APIFramework/Components/PositionComponent.cs` — the component whose change is tracked on `StructuralTag` entities.
- `APIFramework/Components/RoomComponent.cs` — `Bounds: BoundsRect`. Bounds change is a structural change.
- `APIFramework/Components/Tags.cs` — **add `StructuralTag` and `MutableTopologyTag`** at end. `StructuralTag` is the "I am a wall, door, desk, or other obstacle whose position affects topology" marker. `MutableTopologyTag` is the "this entity can be picked up and dropped at runtime" marker (the precondition for `IWorldMutationApi.Move`). Both additive, no enum reordering.
- `APIFramework/Components/WorldObjectComponents.cs` — many world objects (desks, walls, doors). Read for what's currently obstacle-bearing. The world bootstrap (WP-1.7.A) attaches these; this packet **does not modify the world definition**, but a follow-up data packet should add `StructuralTag` to the right entries. **For this packet, the cast/world bootstrap is amended to attach `StructuralTag` programmatically** to entities matching the existing obstacle / wall / door predicates — see Design notes.
- `APIFramework/Core/SimulationBootstrapper.cs` — register `StructuralChangeBus` (singleton service), `PathfindingCacheInvalidationSystem` (Spatial phase, immediately after `RoomMembershipSystem` so the bus has the latest emissions before path queries land). **Conflict warning**: WP-3.0.0 also touches this file. Resolution is "keep both"; the two registrations are at different phases.
- `APIFramework/Core/SimulationClock.cs` — used for timestamping `StructuralChangeEvent`.
- `APIFramework/Config/SimConfig.cs` — `MovementConfig.Pathfinding` already exists. **Extend** with cache-size and cache-strategy fields.
- `SimConfig.json` — extend `pathfinding` section with the new settings.
- `APIFramework.Tests/Systems/Movement/PathfindingServiceTests.cs` — pattern reference; extend with cache-hit / cache-miss / invalidation tests.
- `APIFramework.Tests/Determinism/` (whichever Phase 1/2 test exercises movement determinism — likely `MovementDeterminismTests` or similar) — model `LiveMutationDeterminismTests.cs` on it.

---

## Non-goals

- Do **not** implement player-facing drag-and-drop building UI. That is WP-3.1.D. This packet ships only the *engine surface* drag-and-drop will use (`IWorldMutationApi`).
- Do **not** implement stain-as-fall-risk or any 3.0.3 logic. That is WP-3.0.3, which depends on this packet.
- Do **not** implement region-scoped cache eviction. v0.1 invalidates the entire cache on any structural change. The followup is "evict only entries whose tile range overlaps the change box" — that's a real perf win at 30+ NPCs but it's its own packet.
- Do **not** add a bus for non-structural change ("entity got hungry"). The structural bus is *only* for topology mutations that affect pathfinding / room queries.
- Do **not** modify the A\* algorithm or its tie-break noise behavior. Identical inputs (and equal topology version) must still produce identical paths — this packet adds memoization, not a new path computation.
- Do **not** modify `WorldStateDto`, `Warden.Telemetry`, `Warden.Orchestrator`, `Warden.Anthropic`, `ECSCli`. Engine project (`APIFramework`) and its tests only.
- Do **not** modify any system in WP-3.0.0's guard list. The two packets are intentionally disjoint.
- Do **not** introduce a NuGet dependency. The LRU cache is a small dictionary + linked list; ~50 lines of stock C#.
- Do **not** introduce concurrency / locks in the cache. The engine is single-threaded by design (see SRD §4.2 determinism). The cache is not thread-safe; this is correct.
- Do **not** add a runtime LLM call anywhere. (SRD §8.1.)
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing.

---

## Design notes

### `StructuralChangeKind`

```csharp
public enum StructuralChangeKind
{
    EntityMoved        = 0,   // a StructuralTag entity changed PositionComponent
    EntityAdded        = 1,   // a StructuralTag entity was spawned at a tile
    EntityRemoved      = 2,   // a StructuralTag entity was destroyed or had its tag removed
    ObstacleAttached   = 3,   // ObstacleTag added to an existing entity
    ObstacleDetached   = 4,   // ObstacleTag removed
    DoorwayAttached    = 5,   // DoorwayTag added (or whatever doorway marker the engine uses)
    DoorwayDetached    = 6,
    RoomBoundsChanged  = 7,   // RoomComponent.Bounds changed
}
```

### `StructuralChangeEvent`

```csharp
public readonly record struct StructuralChangeEvent(
    StructuralChangeKind Kind,
    Guid                 EntityId,
    int                  PreviousTileX,
    int                  PreviousTileY,
    int                  CurrentTileX,
    int                  CurrentTileY,
    Guid                 RoomId,           // affected room, or Guid.Empty if cross-room or floor-level
    long                 TopologyVersion,  // version stamped at emission
    long                 Tick
);
```

Producers fill what's relevant; consumers ignore irrelevant fields.

### `StructuralChangeBus`

Mirrors `ProximityEventBus`'s shape:

```csharp
public sealed class StructuralChangeBus
{
    private readonly List<Action<StructuralChangeEvent>> _subscribers = new();
    private long _topologyVersion;

    public long TopologyVersion => _topologyVersion;

    public void Subscribe(Action<StructuralChangeEvent> handler) => _subscribers.Add(handler);

    public void Emit(StructuralChangeKind kind, Guid entityId, int prevX, int prevY, int curX, int curY, Guid roomId, long tick)
    {
        Interlocked.Increment(ref _topologyVersion); // single-threaded engine; this is just paranoia
        var ev = new StructuralChangeEvent(kind, entityId, prevX, prevY, curX, curY, roomId, _topologyVersion, tick);
        foreach (var h in _subscribers) h(ev);
    }
}
```

The bus is registered as a singleton in `SimulationBootstrapper`. Producers are injected with it.

### `PathfindingCache`

A bounded LRU keyed by the full query tuple. The `topologyVersion` is part of the key so invalidation can be lazy (we *could* also do eager wipe — see below; choose one).

```csharp
public readonly record struct PathQueryKey(int FromX, int FromY, int ToX, int ToY, int Seed, long TopologyVersion);

public sealed class PathfindingCache
{
    private readonly int _maxEntries;
    private readonly Dictionary<PathQueryKey, LinkedListNode<KeyValuePair<PathQueryKey, IReadOnlyList<(int X, int Y)>>>> _map;
    private readonly LinkedList<KeyValuePair<PathQueryKey, IReadOnlyList<(int X, int Y)>>> _lru;

    public PathfindingCache(int maxEntries) { /* ... */ }

    public bool TryGet(PathQueryKey key, out IReadOnlyList<(int X, int Y)> path) { /* ... */ }
    public void Put(PathQueryKey key, IReadOnlyList<(int X, int Y)> path) { /* ... */ }
    public void Clear();
    public int Count { get; }
}
```

**Eviction policy choice for v0.1: clear on every structural change.** Rationale: at 30 NPCs and `maxEntries = 512`, the cache fills naturally and clears about once per drag-drop or stain-spawn. Region-scoped eviction is a real win at 100+ entities; deferred to followup. The dual benefit of clearing-on-change is that the `topologyVersion` in the key becomes redundant for correctness (cache only ever holds entries from the current version) — but keep it in the key anyway, for debug visibility in the cache miss telemetry.

### `PathfindingService.ComputePath` change

```csharp
public IReadOnlyList<(int X, int Y)> ComputePath(int fromX, int fromY, int toX, int toY, int seed)
{
    if (fromX == toX && fromY == toY) return Array.Empty<(int, int)>();

    var key = new PathQueryKey(fromX, fromY, toX, toY, seed, _bus.TopologyVersion);
    if (_cache.TryGet(key, out var cached)) return cached;

    // existing A* logic, unchanged
    var path = ComputePathUncached(fromX, fromY, toX, toY, seed);
    _cache.Put(key, path);
    return path;
}
```

Determinism contract holds: the same `(from, to, seed)` against the same topology version always produces the same path; cache hit and cache miss return the same value. The existing `PathfindingServiceTests` should pass unchanged.

### `PathfindingCacheInvalidationSystem`

A two-line system that subscribes to the bus and clears the cache on emit:

```csharp
public sealed class PathfindingCacheInvalidationSystem : ISystem
{
    private readonly PathfindingCache _cache;

    public PathfindingCacheInvalidationSystem(StructuralChangeBus bus, PathfindingCache cache)
    {
        _cache = cache;
        bus.Subscribe(_ => _cache.Clear());
    }

    public void Update(EntityManager em, float deltaTime) { /* nothing to do per-tick */ }
}
```

The system is registered for lifecycle reasons (so it survives bootstrap), not because it has tick logic. Subscription happens once in the constructor; clears happen as side-effect of bus emissions, which themselves happen during the producing system's tick.

### Producers — what emits when

**`SpatialIndexSyncSystem` (extended):**
- For each entity tracked by the spatial index that has `StructuralTag`: when its tile position changes, emit `EntityMoved` with previous and current tile.
- When a tagged entity is added to the index (spawned), emit `EntityAdded`.
- When a tagged entity is removed (despawned), emit `EntityRemoved`.

**`RoomMembershipSystem` (extended only for room-bounds change, not for membership change):**
- When a `RoomComponent.Bounds` change is detected (compare current vs cached), emit `RoomBoundsChanged`. NPC membership transitions remain on `ProximityEventBus` only.

**`IWorldMutationApi` (new):**
A small public interface in `APIFramework/Mutation/IWorldMutationApi.cs` with these methods:

```csharp
public interface IWorldMutationApi
{
    void MoveEntity(Guid entityId, int newTileX, int newTileY);
    void SpawnStructural(Guid templateId, int tileX, int tileY);   // returns the new entity id via out or async — pick one
    void DespawnStructural(Guid entityId);
    void AttachObstacle(Guid entityId);
    void DetachObstacle(Guid entityId);
    void ChangeRoomBounds(Guid roomId, BoundsRect newBounds);
}
```

Each method validates inputs, applies the mutation through the entity manager, and then emits the corresponding `StructuralChangeEvent`. **All structural mutations after this packet flow through this interface** — this is the contract. Tests, 3.0.3's stain spawner, and 3.1.D's Unity glue all go through it. Direct `entity.Set(new PositionComponent(...))` on a `StructuralTag` entity outside the API is a code smell flagged in the completion note.

The existing world bootstrap (WP-1.7.A) is **not** rewritten to go through the API — boot-time spawns happen before the bus has subscribers and don't need cache invalidation. Document the boot-vs-runtime distinction in the completion note.

### `StructuralTag` attachment

The packet does not author per-prop JSON for which world objects are structural. Instead, it adds a `StructuralTaggingSystem` (PreUpdate, runs once at boot then idles) that walks the entity manager and attaches `StructuralTag` to every entity matching:

- has `WallComponent` (if present) OR
- has `DoorComponent` (if present) OR
- has `ObstacleTag` OR
- is in the named-anchor set (`NamedAnchorComponent`) AND has a fixed `PositionComponent` AND is not an NPC.

The exact predicate set is read from the existing component vocabulary; **the Sonnet documents which predicates were used in the completion note.** A follow-up content-side packet can switch to per-template authoring if the predicate-based approach over-tags or under-tags.

### `MutableTopologyTag`

Marker for "this entity can be moved at runtime via `IWorldMutationApi`." At v0.1, attached to:
- All `StructuralTag` entities except walls. (Walls move via a different verb — wall-add and wall-remove, not wall-move. Player can't drag a wall sideways.)
- All small movable objects (chairs, props, the named-anchor set excepting fixed-mount items).

`IWorldMutationApi.MoveEntity` rejects (returns / throws — pick one and document) if the entity lacks `MutableTopologyTag`. The rejection is fail-closed — no silent no-op.

### SimConfig additions

```jsonc
{
  "pathfinding": {
    /* existing keys: doorwayDiscount, tieBreakNoiseScale */
    "cacheMaxEntries":          512,
    "cacheEvictionStrategy":    "wipeOnChange",   // future: "regionScoped"
    "logCacheHitRateEveryTick": false,            // dev-only telemetry
    "warnIfCacheHitRateBelow":  0.50              // when telemetry is on, warn if observed hit rate falls below this
  },
  "structuralChange": {
    "emitOnNpcMovement":  false,   // axiomatic; documented for clarity, never set true
    "emitOnRoomBoundsChange": true
  }
}
```

### Determinism

- The cache is order-preserving: identical input order produces identical eviction order.
- The bus is single-threaded; emissions happen in the deterministic order producers call `Emit`.
- `TopologyVersion` is a long incremented in producer-call order — deterministic.
- `IWorldMutationApi` calls in tests happen in test-defined order — deterministic.
- The 5000-tick determinism test (`LiveMutationDeterminismTests`) confirms: a deterministic sequence of mutations produces byte-identical state across two seeds.

### Tests

- `StructuralChangeBusTests.cs` — subscribe/emit/drain semantics; `TopologyVersion` monotonic.
- `PathfindingCacheTests.cs` — bounded LRU eviction; hit/miss correctness; `Clear` empties.
- `PathfindingServiceCacheHitTests.cs` — same `(from, to, seed)` against unchanged topology hits cache (instrument with a hit counter).
- `PathfindingServiceCacheMissOnTopologyChangeTests.cs` — mutation through `IWorldMutationApi.MoveEntity` invalidates cache; subsequent identical query is a miss; result is recomputed against new topology.
- `PathfindingServiceDeterminismHoldsTests.cs` — cache hit and cache miss return identical paths for identical inputs at the same `TopologyVersion` (confirms the existing A\* tie-break noise is preserved).
- `IWorldMutationApiMoveTests.cs` — `MoveEntity` on a `MutableTopologyTag` entity updates `PositionComponent` and emits `EntityMoved`; on a non-tagged entity it rejects.
- `IWorldMutationApiSpawnDespawnTests.cs` — spawn emits `EntityAdded`; despawn emits `EntityRemoved`; both increment `TopologyVersion`.
- `IWorldMutationApiObstacleTests.cs` — attach/detach emit the corresponding events.
- `IWorldMutationApiRoomBoundsTests.cs` — bounds change emits `RoomBoundsChanged`.
- `StructuralTaggingSystemTests.cs` — at boot, `StructuralTag` is attached to walls, doors, obstacle entities, and fixed named anchors; not to NPCs; not to ephemeral spawned items (food, etc.).
- `RoomMembershipNoStructuralEmitOnNpcTransitionTests.cs` — when an NPC walks across a room boundary, `ProximityEventBus` fires `RoomMembershipChanged` but `StructuralChangeBus` is silent. (Critical guardrail — NPC movement is not topology change.)
- `SpatialIndexSyncStructuralEmitTests.cs` — when a `StructuralTag`-bearing desk's `PositionComponent` is changed via the API, `SpatialIndexSyncSystem` emits `EntityMoved` with correct prev/cur tiles.
- `LiveMutationDeterminismTests.cs` — 5000 ticks with a deterministic sequence of `IWorldMutationApi.MoveEntity` calls at scripted ticks: byte-identical state across two seeds.
- `PathfindingCacheLruEvictionTests.cs` — at `cacheMaxEntries = 4`, issue 5 distinct queries; oldest entry evicted; subsequent re-query of evicted is a miss.
- `RegressionMovementDeterminismTests.cs` — the existing Phase 1/2 movement determinism test stays green (cache invisible to the existing test surface).

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Systems/Spatial/StructuralChangeKind.cs` | Enum. |
| code | `APIFramework/Systems/Spatial/StructuralChangeEvent.cs` | Record. |
| code | `APIFramework/Systems/Spatial/StructuralChangeBus.cs` | Bus singleton with `TopologyVersion`. |
| code | `APIFramework/Systems/Movement/PathfindingCache.cs` | Bounded LRU. |
| code | `APIFramework/Systems/Movement/PathQueryKey.cs` | Cache key record. |
| code | `APIFramework/Systems/Movement/PathfindingService.cs` (modified) | Add cache lookup before A\*; cache put on miss. |
| code | `APIFramework/Systems/Movement/PathfindingCacheInvalidationSystem.cs` | Bus subscriber; clears cache on emit. |
| code | `APIFramework/Systems/Spatial/SpatialIndexSyncSystem.cs` (modified) | Emit `EntityMoved`/`EntityAdded`/`EntityRemoved` for `StructuralTag` entities. |
| code | `APIFramework/Systems/Spatial/RoomMembershipSystem.cs` (modified) | Emit `RoomBoundsChanged` when `RoomComponent.Bounds` changes. NPC membership unchanged. |
| code | `APIFramework/Systems/Spatial/StructuralTaggingSystem.cs` | One-shot boot system; attaches `StructuralTag`. |
| code | `APIFramework/Mutation/IWorldMutationApi.cs` | Public mutation contract. |
| code | `APIFramework/Mutation/WorldMutationApi.cs` | Default implementation; emits structural events. |
| code | `APIFramework/Components/Tags.cs` (modified) | Add `StructuralTag`, `MutableTopologyTag`. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register `StructuralChangeBus`, `PathfindingCache`, `PathfindingCacheInvalidationSystem`, `StructuralTaggingSystem`, `WorldMutationApi`. **Conflict warning**: WP-3.0.0 also registers two systems here. "Keep both" merge. |
| code | `APIFramework/Config/SimConfig.cs` (modified) | Extend `PathfindingConfig`; new `StructuralChangeConfig`. |
| code | `SimConfig.json` (modified) | New keys under `pathfinding`; new `structuralChange` section. |
| code | `APIFramework.Tests/Systems/Spatial/StructuralChangeBusTests.cs` | Bus semantics. |
| code | `APIFramework.Tests/Systems/Movement/PathfindingCacheTests.cs` | LRU correctness. |
| code | `APIFramework.Tests/Systems/Movement/PathfindingServiceCacheHitTests.cs` | Cache hits on stable topology. |
| code | `APIFramework.Tests/Systems/Movement/PathfindingServiceCacheMissOnTopologyChangeTests.cs` | Mutation invalidates. |
| code | `APIFramework.Tests/Systems/Movement/PathfindingServiceDeterminismHoldsTests.cs` | Determinism preserved. |
| code | `APIFramework.Tests/Mutation/IWorldMutationApiMoveTests.cs` | Move semantics + tag enforcement. |
| code | `APIFramework.Tests/Mutation/IWorldMutationApiSpawnDespawnTests.cs` | Spawn/despawn semantics. |
| code | `APIFramework.Tests/Mutation/IWorldMutationApiObstacleTests.cs` | Obstacle attach/detach. |
| code | `APIFramework.Tests/Mutation/IWorldMutationApiRoomBoundsTests.cs` | Room bounds change. |
| code | `APIFramework.Tests/Systems/Spatial/StructuralTaggingSystemTests.cs` | Predicate coverage at boot. |
| code | `APIFramework.Tests/Systems/Spatial/RoomMembershipNoStructuralEmitOnNpcTransitionTests.cs` | NPC movement is not structural. |
| code | `APIFramework.Tests/Systems/Spatial/SpatialIndexSyncStructuralEmitTests.cs` | Tagged-entity move emits. |
| code | `APIFramework.Tests/Determinism/LiveMutationDeterminismTests.cs` | 5000-tick byte-identical with scripted mutations. |
| code | `APIFramework.Tests/Systems/Movement/PathfindingCacheLruEvictionTests.cs` | LRU eviction. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.0.4.md` | Completion note. SimConfig defaults; `StructuralTaggingSystem` predicate set used (and any false-positives / false-negatives observed); cache-hit-rate measured during the determinism test (informational); list of any system that emitted on the structural bus that wasn't planned in this packet (escalate); confirmation that NPC movement does not emit on the structural bus. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `StructuralChangeKind`, `StructuralChangeEvent`, `StructuralChangeBus`, `PathfindingCache`, `PathQueryKey`, `IWorldMutationApi`, `WorldMutationApi`, `StructuralTag`, `MutableTopologyTag` compile and instantiate. | unit-test |
| AT-02 | `StructuralChangeBus.Emit` increments `TopologyVersion` exactly once per call; subscribers receive the event with matching version stamp. | unit-test |
| AT-03 | `PathfindingCache(maxEntries=4)` LRU: 5 distinct puts evict the oldest; `TryGet` on evicted returns false; on present returns true with the stored path. | unit-test |
| AT-04 | `PathfindingService.ComputePath` with identical `(from, to, seed)` and unchanged topology: second call hits cache (instrument via a hit counter). | unit-test |
| AT-05 | `PathfindingService.ComputePath` returns identical paths for cache-hit and cache-miss against the same topology version (determinism preserved). | unit-test |
| AT-06 | `IWorldMutationApi.MoveEntity` on a `StructuralTag + MutableTopologyTag` entity: updates `PositionComponent`, emits `EntityMoved`, increments `TopologyVersion`, the cache is empty after the call. | integration-test |
| AT-07 | `IWorldMutationApi.MoveEntity` on an entity without `MutableTopologyTag`: fails closed (returns false / throws — pick one in the spec and document); state unchanged; no event emitted. | integration-test |
| AT-08 | `IWorldMutationApi.SpawnStructural` emits `EntityAdded`; `DespawnStructural` emits `EntityRemoved`; `AttachObstacle` / `DetachObstacle` emit corresponding events; `ChangeRoomBounds` emits `RoomBoundsChanged`. | integration-test |
| AT-09 | NPC movement: an NPC walking across 100 tiles produces zero `StructuralChangeBus` emissions. `ProximityEventBus` continues to fire normally. | integration-test |
| AT-10 | `StructuralTaggingSystem` at boot: walls, doors, obstacle entities, fixed named anchors gain `StructuralTag`; NPCs do not; food / bolus / drinkware do not. | integration-test |
| AT-11 | After `MoveEntity` on a desk: a subsequent `ComputePath` query crossing the desk's old tile vs new tile produces a different path than it would have before the move. (Confirms cache invalidation actually changed routing.) | integration-test |
| AT-12 | After 100 `MoveEntity` calls, the cache size is bounded (≤ `cacheMaxEntries`) and contains only entries from the latest `TopologyVersion`. | integration-test |
| AT-13 | Determinism: 5000-tick run, two seeded worlds with the same scripted sequence of `IWorldMutationApi` calls at fixed ticks: byte-identical state at recorded intervals and at tick 5000. | unit-test |
| AT-14 | All Phase 0, Phase 1, and Phase 2 tests stay green. **Specifically**: `MovementDeterminismTests`, `PathfindingServiceTests` (existing), `RoomMembershipTests`, `SpatialIndexSyncTests`. The cache must be invisible to the existing test surface. | regression |
| AT-15 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-16 | `dotnet test ECSSimulation.sln` — all green, no exclusions. | build + unit-test |
| AT-17 | `ECSCli ai describe --out engine-fact-sheet.md` regenerates with the new systems and the two new tags listed; `FactSheetStalenessTests` (WP-2.9.A) detects the diff and is regenerated. | build + unit-test |

---

## Followups (not in scope)

- **Region-scoped cache eviction.** Drop only entries whose `(min(fromX,toX), min(fromY,toY))..(max(fromX,toX), max(fromY,toY))` range overlaps the changed bounding box. Real perf win at 100+ entities; deferred. Telemetry from this packet's `logCacheHitRateEveryTick` mode informs whether it's worth doing.
- **Producer for stain entities** (3.0.3 territory). Stains spawning at runtime go through `IWorldMutationApi.SpawnStructural` (or a stain-specific verb). 3.0.3 wires it.
- **Producer for live wall/door builds** (3.1.D territory). Unity-glue calls `IWorldMutationApi.SpawnStructural` on a desk-template-id when the player drops a desk; calls `AttachObstacle` when the player adds a wall segment; etc. The contract is set; 3.1.D is the consumer.
- **Per-template `StructuralTag` authoring**. `StructuralTaggingSystem`'s predicate-based approach is heuristic. A follow-up data packet can switch to explicit `"structural": true` flags in the entity-template JSON files.
- **Cache hit-rate as a packet-level metric.** Whether a packet's content additions degrade cache effectiveness (e.g., introduce a system that path-queries with constantly-different seeds) becomes worth knowing. The telemetry hook in this packet (`logCacheHitRateEveryTick`) is the substrate; a downstream packet can wire it to the WARDEN reports.
- **Multi-floor topology**. The current pathfinding service is single-floor; the world bible commits three floors. The cache key already implies single-floor (no `floorId`). When multi-floor lands, extend `PathQueryKey` with `FloorId` and emit `RoomBoundsChanged` cross-floor when a stairwell connects/disconnects.
- **Concurrency / threaded path queries**. The engine is single-threaded; future Unity work may want an off-thread "what's the best path from A to B" query for predictive UI. The cache would need lock-free access. Out of scope until then.
- **Eager rebuild of obstacle/doorway sets**. The current A\* rebuilds these on every cache miss. A second-tier cache (the obstacle set itself, keyed by `TopologyVersion`) is a real perf win at high path-query rates. Deferred.
