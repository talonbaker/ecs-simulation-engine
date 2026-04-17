# Architecture Decision Record — ECS Simulation Engine

> **Version:** v0.7.2  
> **Author:** Talon Baker  
> **Status:** Living document — updated with each architectural milestone

This document records the *why* behind every structural decision in the engine.
It is not a tutorial or API reference; it is a record of trade-offs made and
trade-offs deferred.

---

## 1. Entity Identity and Component Storage

### Decision: `Dictionary<Type, object>` per entity

Every entity is a class instance holding a `Dictionary<Type, object>`.
Components are stored as `object` — which boxes every struct onto the heap.

**Why we accepted boxing:**

The engine started as a learning project for the ECS pattern. Getting the
abstraction correct — stateless systems, data-driven components, query-based
iteration — was the priority. A typed `ComponentStore<T>` would have required
either a complex generic dictionary abstraction or a source-generator, both of
which add significant complexity before the architecture is stable.

**The cost:**

Every `Add<T>` on a struct component allocates a boxed copy. Every `Get<T>`
unboxes it. At 13 systems × 60 fps × ~10 component writes per tick per entity,
a 100-entity simulation generates roughly 780,000 heap allocations per second.
.NET's GC handles this gracefully at this scale; it becomes a problem around
10,000 entities or with sustained per-tick allocation spikes.

**The fix (deferred to v0.8+):**

```
ComponentStore<T> where T : struct
  — typed array per component type
  — zero boxing
  — SIMD-friendly layout for tight loops
```

Entity stays as a class (reference semantics are correct for an entity
identity object). Only the storage of component *values* moves to typed arrays.
The public API (`Add<T>`, `Get<T>`, `Has<T>`, `Remove<T>`) does not change —
systems are unaffected by the storage change.

**Entity ID:**

Currently `Guid` (128 bits). Migration path to `int` or `uint` is deferred
alongside ComponentStore — when storage moves to arrays, an integer ID becomes
the array index, and the Guid becomes an optional external key.

---

## 2. Query Indexing: O(E) → O(1)

### The problem (pre-v0.7.2)

`EntityManager.Query<T>()` was implemented as:

```csharp
_entities.Where(e => e.Has<T>())
```

This scans every entity on every call. At 13 systems × 60 fps, that is 780 full
scans per second. At 100 entities the scan takes nanoseconds; at 10,000 entities
it dominates tick time.

### The fix (v0.7.2)

`EntityManager` now maintains a component index:

```
_componentIndex : Dictionary<Type, HashSet<Entity>>
```

`Entity` fires an `onChange` callback whenever a component type is added or
removed for the first time. `EntityManager` listens and updates the index.

`Query<T>()` now returns the pre-built bucket — O(1), no allocation, no scan.

### Design notes

- **Callback fires once per type, not per write.** Overwriting an existing
  component via `Add<T>()` does not fire the callback — the entity's membership
  in the index bucket doesn't change.
- **Bucket is a `HashSet<Entity>`**, not a `List`, so `DestroyEntity` cleanup
  is O(1) per bucket rather than O(N).
- **Thread safety:** The index is not thread-safe. All entity mutations happen
  on the simulation thread. Parallel system execution (v0.8+) will use a
  command queue flushed at phase boundaries.

---

## 3. System Phase Model

### Decision: Explicit `SystemPhase` enum over flat registration order

Systems are grouped by phase and run in ascending numeric order. Within a phase,
systems run sequentially in registration order.

```
PreUpdate  (0)   — invariant enforcement
Physiology (10)  — biological resource drain/restore
Condition  (20)  — derive sensation tags from physiology
Cognition  (30)  — process conditions into emotions and drives
Behavior   (40)  — act on the dominant drive
Transit    (50)  — move content through digestive pipeline
World      (60)  — environmental systems
PostUpdate (100) — end-of-frame cleanup (reserved)
```

**Why not just a flat ordered list?**

A flat list encodes a total ordering, which is *correct* but not *expressive*.
It conflates two very different constraints:

1. A hard data dependency — System A writes a component that System B reads.
   This must be a phase boundary or an intra-phase ordering constraint.

2. An incidental registration order — System A happens to be added before
   System B because someone wrote it that way.

With a flat list, there is no way to tell the difference. Phases make the
dependency structure explicit. Systems in the same phase declare that they are
logically concurrent — they don't share write targets. Systems in an earlier
phase produce data that later phases consume.

**Current execution is still sequential:**

As of v0.7.2, systems within a phase still run one at a time. The phases exist
for documentation and future parallelism. No performance gain from phases alone.

**The performance win of v0.7.2 is the Query<T>() index** (see §2), which is
orthogonal to phases.

### Path to parallel execution (v0.8+ design sketch)

Each phase becomes a `Task.WhenAll` over its member systems:

```csharp
foreach (var phaseGroup in _orderedCache.GroupBy(r => r.Phase))
{
    var tasks = phaseGroup.Select(r =>
        Task.Run(() => r.System.Update(EntityManager, scaledDelta)));
    await Task.WhenAll(tasks);
    // ← Phase barrier: all writes from this phase are visible before
    //   any system in the next phase begins.
}
```

This requires two preconditions:
1. Systems within a phase must not share write targets (enforced by convention
   and documented per-system).
2. Entity mutations during parallel execution must go through a thread-safe
   command queue flushed at the barrier.

The `SystemRegistration` record and `AddSystem(system, phase)` API are already
compatible with this model. No system code changes when parallelism is added.

---

## 4. ISpatialIndex — Contract-First Design

### Status: Stub only (v0.7.2)

`ISpatialIndex` defines the spatial query contract that v0.9 world systems will
depend on. No production implementation exists yet.

**Why define it now?**

1. v0.8 introduces a 2-D world grid with `PositionComponent`. The systems that
   need proximity queries should declare `ISpatialIndex` in their constructors
   so that the dependency is visible at registration time.

2. An interface defined before any implementation is written tends to be cleaner
   than one extracted from a working concrete class.

3. It keeps v0.8 world component design honest — if a system can't be expressed
   in terms of `QueryRadius` or `QueryNearest`, the right fix is either to
   extend the interface or to reconsider the system's design.

**Proposed implementations:**

| Name         | Best for                        | Complexity |
|:-------------|:--------------------------------|:-----------|
| SimpleGrid   | sparse world, uniform density   | Low        |
| Quadtree     | dense clusters, variable density| Medium     |
| BVH          | large entities, varying sizes   | High       |
| NullIndex    | headless tests, no world needed | Trivial    |

`SimulationBootstrapper` will choose which implementation to instantiate and
inject. Systems never see the concrete type.

---

## 5. Honest Capacity Assessment — "Thousands of Entities"

The engine's current architecture (v0.7.2) can handle several hundred entities
at 60 fps without tuning. The design decisions below affect the ceiling:

| Factor                     | Current                         | Limit at 60 fps     |
|:---------------------------|:--------------------------------|:--------------------|
| Query<T>() cost            | O(1) bucket lookup (v0.7.2)     | Not a bottleneck    |
| Component write cost       | 1 heap allocation per struct Add | ~500 entities       |
| System count               | 13 sequential systems           | Scales with threads |
| Parallel execution         | Not yet implemented             | 1 core              |
| Memory per entity          | ~15 components × ~64 bytes each | ~1 KB per entity    |

**The two bottlenecks at scale:**

1. **Boxing** (§1) — allocation pressure from `Dictionary<Type, object>`. Fix:
   `ComponentStore<T>` typed arrays. Deferred to v0.8+.

2. **Single-threaded systems** (§3) — all 13 systems share one core. Fix: phase
   parallel execution. Prerequisite: command queue for thread-safe entity
   mutation.

With both fixes applied, the engine can comfortably simulate 10,000+ entities.
Without them, expect GC pauses above ~500 entities at sustained 60 fps.

**Practical advice for v0.7.x development:**

- Test with up to 20 entities (the intended simulation scale for now).
- Don't introduce entity pools or object recycling prematurely — the GC handles
  sub-500 entities well and premature pooling adds complexity that obscures bugs.
- Profile before optimizing. The Query<T>() scan was the real O(E) problem;
  it's fixed. Boxing is measurable but not yet painful.

---

## 6. What Was Explicitly Not Done (and Why)

### No source generators

Source generators could eliminate the boxing problem today, but they obscure the
data model, complicate debugging, and add a build-time dependency. The v0.7.x
focus is getting the *model* right.

### No component pooling

Object pools for food/bolus entities would reduce GC pressure but add lifecycle
complexity (reset bugs, double-return bugs). With <100 food entities in flight
at any time, the pool isn't worth its bugs.

### No dependency injection framework

Constructor injection is sufficient and explicit. A DI container would hide the
wiring that `SimulationBootstrapper` currently makes visible.

### No ECS library (Arch, DefaultECS, LeoECS, etc.)

Using an existing ECS library would produce a faster simulation but a shallower
learning project. The engine exists to understand ECS from the inside. When the
engine's architecture stabilizes, migrating the hottest paths to a library is
a legitimate v1.x option.

---

*End of ARCHITECTURE.md — v0.7.2*
