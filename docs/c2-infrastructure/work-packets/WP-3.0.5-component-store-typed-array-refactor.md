# WP-3.0.5 — `ComponentStore<T>` Typed-Array Refactor

> **DO NOT DISPATCH UNTIL WP-3.0.0, WP-3.0.1, WP-3.0.2, WP-3.0.3, AND WP-3.0.4 ARE ALL MERGED.**
> This packet rewires `Entity._components` from `Dictionary<Type, object>` to per-type typed stores. The API surface at the call site (`Get<T>()`, `Has<T>()`, `Set<T>()`, `Add<T>()`, `Remove<T>()`) is preserved exactly, but the refactor touches every component access in the codebase. Dispatching this packet in parallel with any other Phase 3.0.x work would force the parallel packet to rebase mid-flight onto the new internals — wasted Sonnet cycles. Run this **solo** in its own wave after Wave 2 merges.

**Tier:** Sonnet
**Depends on:** All of Phase 0, 1, 2, and Phase 3.0.0–3.0.4 (regression coverage is the primary safety net for this refactor)
**Parallel-safe with:** **NOTHING.** Solo dispatch only.
**Timebox:** 150 minutes (longer than typical; the test surface to verify is large)
**Budget:** $0.60

---

## Goal

The single highest-impact engine performance packet for Phase 3. The kickoff brief calls it out by name: *"the engine's known boxing issue (`Entity._components: Dictionary<Type, object>`) becomes a real concern at scale; WP-3.0.5 fixes it before Unity work scales up."*

The current shape:

```csharp
public sealed class Entity
{
    private readonly Dictionary<Type, object> _components = new();

    public T Get<T>() where T : struct => (T)_components[typeof(T)];
    public bool Has<T>() where T : struct => _components.ContainsKey(typeof(T));
    public void Set<T>(T value) where T : struct => _components[typeof(T)] = value;
    // (similar for Add / Remove / reference-type components)
}
```

Every `Get<T>()` boxes the struct into `object`, then unboxes on read. Every `Set<T>()` boxes again. At 30 NPCs running ~50 systems per tick, each system accessing ~3 components per NPC = 4500 box/unbox cycles per tick = 270,000 per second at 60 FPS. Each box is a heap allocation; each allocation is a future GC pressure point. **Unity at 60 FPS will GC-stutter.**

The fix is structural: replace the per-entity `Dictionary<Type, object>` with **per-component-type typed stores** held in a single `ComponentStoreRegistry` keyed by `Type`. Each store internally is a `Dictionary<Guid, T>` (or sparse-set, see Design notes), which holds value types directly without boxing. Entity becomes a thin handle that delegates lookups to the registry.

```csharp
public sealed class Entity
{
    public Guid Id { get; }
    private readonly ComponentStoreRegistry _registry;

    public T Get<T>() where T : struct => _registry.Store<T>().Get(Id);
    public bool Has<T>() where T : struct => _registry.Store<T>().Has(Id);
    public void Set<T>(T value) where T : struct => _registry.Store<T>().Set(Id, value);
}
```

`ComponentStoreRegistry.Store<T>()` is the only place a `Type` key is dispatched, and it resolves to a JIT-specialised generic call. After JIT inlining, `Get<T>()` becomes a direct typed-dictionary lookup — zero boxing, one hash lookup, one struct copy.

The API surface at every call site stays identical. The thousands of `npc.Get<DriveComponent>()` / `npc.Has<TaskTag>()` / `npc.Set(stress)` calls across the codebase are unchanged. **The refactor is structurally invisible to system code.**

After this packet:

- Entity component access has zero boxing for value-type components (which is most of them — every component shipped in Phase 1 and 2 except a few list-typed ones).
- Allocation per simulation tick drops measurably (target: ≥80% reduction at 30-NPC baseline).
- The 5000-tick byte-identical determinism contract holds across all extant tests.
- A new perf test asserts the allocation drop and the `Get<T>()` benchmark improvement.

This packet is **engine-internal at v0.1.** No wire-format change. No orchestrator change. The cost ledger and reports are unaffected.

---

## Reference files

- `docs/PHASE-3-KICKOFF-BRIEF.md` — packet 6 in the 3.0.x backlog. Calls this out as the perf prerequisite for Unity work.
- `docs/c2-infrastructure/PHASE-2-HANDOFF.md` §6.0 — same.
- `docs/c2-infrastructure/00-SRD.md` §4.2 (determinism — must be preserved exactly), §4.4 (observability — events.jsonl unchanged).
- `APIFramework/Core/Entity.cs` — **the central refactor target.** Today it holds `Dictionary<Type, object> _components`. After: holds an `EntityHandle` reference (id + registry pointer); component access delegates to the registry.
- `APIFramework/Core/EntityManager.cs` — owns the registry. Today it tracks entities; after: it also exposes the registry via internal API so Entity instances can resolve their stores.
- `APIFramework/Core/SimulationBootstrapper.cs` — instantiates `EntityManager`; needs to also instantiate `ComponentStoreRegistry` (the registry can live inside EntityManager or as a peer; Sonnet picks).
- **Every file under `APIFramework/Components/`** — read-only. The component definitions don't change. The struct shapes are preserved.
- **Every file under `APIFramework/Systems/`** — read-only at the call site (the `npc.Get<T>()` / `npc.Set<T>()` patterns continue to work). However, the Sonnet must verify there are **no places** that use a non-generic component-access pattern (e.g., reflection-driven access via `Type.GetField` on `_components`). If any are found, document them as deviations and either rewire or document the limitation.
- **Every file under `APIFramework.Tests/`** — read-only. The regression suite IS the safety net.
- `APIFramework/Components/Tags.cs` — empty-struct tags (`TaskTag`, `OverdueTag`, `StressedTag`, etc.). These are the highest-volume `Has<T>()` callsites. The store implementation must be efficient for them — a `HashSet<Guid>` is sufficient for tag-shaped components (no payload). Document the tag-store specialisation in the completion note.
- `Warden.Telemetry/Projectors/*` — read for completeness. The projectors call `entity.Get<T>()` thousands of times per snapshot. The refactor must not break them. Telemetry tests are part of the regression sweep.
- `Warden.Telemetry.Tests/`, `Warden.Anthropic.Tests/`, `Warden.Orchestrator.Tests/` — full regression sweep.

---

## Non-goals

- Do **not** change the API surface at call sites. `npc.Get<T>()`, `npc.Has<T>()`, `npc.Set<T>()`, `npc.Add<T>()`, `npc.Remove<T>()`, `entityManager.Query<T>()` all keep their signatures. If a Sonnet finds a way to make the refactor cleaner by changing one of these signatures, that is **out of scope**; commit the conservative version and surface the alternative in the completion note as a follow-up packet.
- Do **not** introduce a query DSL beyond what `Query<T>()` and `Query<T1, T2>()` (or whatever multi-arg shape exists today) already provide. ECS query libraries are a rabbit hole; this packet is a structural refactor, not an ergonomics overhaul.
- Do **not** introduce concurrency / threading. The engine is single-threaded by design. Stores are not thread-safe; this is correct.
- Do **not** add reflection beyond what the `ComponentStoreRegistry`'s typed dispatch requires. No serialisation reflection, no dynamic component registration at runtime.
- Do **not** change `WorldStateDto` serialisation. `Warden.Telemetry`'s projectors continue to read entity state; the projectors don't care how Entity stores its components.
- Do **not** change behaviour. **This is a perf refactor, not a feature addition.** Every test that passed before this packet must pass after, byte-identical.
- Do **not** restructure component definitions. The structs in `APIFramework/Components/` are not modified — read-only to this packet.
- Do **not** introduce `ref struct` or `Span<T>` API surfaces — they're tempting for perf but constrain the call sites the entire codebase already uses. Future packet, once profiling justifies it.
- Do **not** introduce a NuGet dependency.
- Do **not** add a runtime LLM call. (SRD §8.1.)
- Do **not** retry, recurse, or "self-heal." Fail closed per SRD §4.1.

---

## Design notes

### `ComponentStore<T>`

Per-component-type store. Holds the typed values directly:

```csharp
public sealed class ComponentStore<T> where T : struct
{
    private readonly Dictionary<Guid, T> _data = new();

    public T Get(Guid entityId)
    {
        if (!_data.TryGetValue(entityId, out var value))
            throw new InvalidOperationException($"Entity {entityId} has no {typeof(T).Name}");
        return value;
    }

    public bool TryGet(Guid entityId, out T value) => _data.TryGetValue(entityId, out value);

    public bool Has(Guid entityId) => _data.ContainsKey(entityId);

    public void Set(Guid entityId, T value) => _data[entityId] = value;

    public void Add(Guid entityId, T value)
    {
        if (_data.ContainsKey(entityId))
            throw new InvalidOperationException($"Entity {entityId} already has {typeof(T).Name}");
        _data[entityId] = value;
    }

    public void Remove(Guid entityId) => _data.Remove(entityId);

    public IEnumerable<Guid> EntityIds => _data.Keys;
    public int Count => _data.Count;
}
```

For value-type `T`, `_data[entityId]` returns `T` directly — no boxing. The struct is copied into the dictionary entry on `Set` and copied out on `Get`. JIT specialisation makes the Dictionary `<Guid, T>` lookup efficient per type.

### Tag-store specialisation

For empty-struct components (tags — `TaskTag`, `OverdueTag`, `IsChokingTag`, etc.), the payload-bearing `Dictionary<Guid, T>` wastes memory and a hash. A specialised `TagStore` can be a `HashSet<Guid>`:

```csharp
public sealed class TagStore<T> where T : struct
{
    private readonly HashSet<Guid> _ids = new();

    public bool Has(Guid entityId) => _ids.Contains(entityId);
    public void Set(Guid entityId, T _) => _ids.Add(entityId);
    public void Add(Guid entityId, T _) { _ids.Add(entityId); }
    public void Remove(Guid entityId) => _ids.Remove(entityId);
    public IEnumerable<Guid> EntityIds => _ids;
}
```

`ComponentStoreRegistry.Store<T>()` returns the right store flavour at registration time. Detection of tag-shaped types: a struct with zero non-static fields (use `typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public).Length == 0`). Document the detection logic in the completion note; it must be deterministic at registration time.

The Sonnet may choose to skip the tag specialisation and ship only the typed-dictionary store at v0.1 — that's acceptable; the boxing-free win is the headline. Document the choice.

### `ComponentStoreRegistry`

```csharp
public sealed class ComponentStoreRegistry
{
    private readonly Dictionary<Type, object> _stores = new();

    public ComponentStore<T> Store<T>() where T : struct
    {
        if (_stores.TryGetValue(typeof(T), out var existing))
            return (ComponentStore<T>)existing;

        var created = new ComponentStore<T>();
        _stores[typeof(T)] = created;
        return created;
    }

    // overload returning TagStore<T> if tag-specialisation is shipped
}
```

Registry holds one `Dictionary<Type, object>` lookup per *component access* — but only one, at the registry level, not one per entity. The lookup result (the typed store) is cached at the call site through normal C# generic dispatch. The single registry-level Dictionary is the only `object` boxing in the engine after this packet, and it's hit once per component-type-per-tick at most (when JIT specialisation hasn't hoisted the lookup), not 4500 times per tick.

For maximum performance, the registry can be made `static` per-AppDomain, with a `ConcurrentDictionary` if multiple `EntityManager` instances coexist (test harnesses do this). Sonnet picks; default to per-`EntityManager`-instance registry, threaded via constructor.

### `Entity` rewire

```csharp
public sealed class Entity
{
    public Guid Id { get; }
    internal int IntId { get; }   // preserved if existing tests use EntityIntId for ordering
    private readonly ComponentStoreRegistry _registry;

    internal Entity(Guid id, int intId, ComponentStoreRegistry registry)
    {
        Id = id; IntId = intId; _registry = registry;
    }

    public T Get<T>() where T : struct => _registry.Store<T>().Get(Id);
    public bool TryGet<T>(out T value) where T : struct => _registry.Store<T>().TryGet(Id, out value);
    public bool Has<T>() where T : struct => _registry.Store<T>().Has(Id);
    public void Set<T>(T value) where T : struct => _registry.Store<T>().Set(Id, value);
    public void Add<T>(T value) where T : struct => _registry.Store<T>().Add(Id, value);
    public void Remove<T>() where T : struct => _registry.Store<T>().Remove(Id);
}
```

Identity (Guid + IntId), parent, name, etc. — preserve whatever shape the current Entity has. Only the component-storage internals change.

### `EntityManager.Query<T>()` rewire

```csharp
public IEnumerable<Entity> Query<T>() where T : struct
{
    var store = _registry.Store<T>();
    foreach (var id in store.EntityIds)
    {
        if (_byId.TryGetValue(id, out var entity))
            yield return entity;
    }
}

public IEnumerable<Entity> Query<T1, T2>() where T1 : struct where T2 : struct
{
    var s1 = _registry.Store<T1>();
    var s2 = _registry.Store<T2>();
    // intersect keys: pick the smaller store, iterate it, filter by the other
    var (small, large) = s1.Count <= s2.Count ? (s1.EntityIds, (s2.Has)) : (s2.EntityIds, (s1.Has));
    foreach (var id in small) {
        if (large(id) && _byId.TryGetValue(id, out var entity)) yield return entity;
    }
}
```

Multi-component queries become an intersection of typed-store keys — cheaper than the current "iterate all entities, check each" if the engine's current implementation does that. If today's `Query<T>()` is already efficient, preserve its shape; if it's the slow form, this is a free perf win.

### Migration path

Keep the existing `Dictionary<Type, object>` field in Entity in parallel for a **single intermediate commit** that wires the registry and switches Entity to delegate. Run all tests on the dual-storage variant to confirm equivalence. Then delete the dictionary in a second commit. Two commits, one PR.

This is the safety mechanism: if any test fails on the registry path, the dual-storage variant tells you whether the bug is in the new code or in your assumption about what the old code did.

If the Sonnet finds the dual-storage approach impractical (e.g., the existing dictionary is referenced from a place that's hard to rewire), document the deviation and ship a single-commit clean refactor.

### Determinism

- `Dictionary<Guid, T>` iteration order is **not deterministic in .NET** unless the keys are inserted in a consistent order. The engine already iterates entities in `OrderBy(EntityIntId)` everywhere it cares about determinism. The new `EntityManager.Query<T>()` must preserve this discipline: iterate the store's keys, but order by `IntId` (or `Guid`) before yielding.
- The 5000-tick byte-identical determinism test is the contract. **If any extant Phase 0–3.0.4 test goes red on the refactor, the refactor is wrong; do not attempt to "fix" the test — fail closed, escalate via the completion note.**

### Performance test

Add `ComponentStorePerformanceTests.cs` that runs the `MovementDeterminismTests` 30-NPC scenario for 1000 ticks, captures `GC.GetTotalAllocatedBytes(precise: true)` before and after, and asserts:

- Allocation per tick (after the initial bootstrap) drops by ≥ 80% vs a baseline measurement (Sonnet captures the baseline manually before refactoring; documents it in the completion note).
- A focused micro-benchmark: 100,000 `npc.Get<DriveComponent>()` calls in a loop, measuring elapsed time. Assert: at least 2× faster than baseline.

Use BenchmarkDotNet if it's already in the test project; otherwise a `Stopwatch`-based assertion is acceptable for v0.1 (document the noise margin).

If the perf assertions fail in CI on slow hardware, **do not weaken the assertion** — the refactor's value depends on the actual win. Document the failure mode and escalate.

### Tests

Existing tests are the regression net (~hundreds of tests across Phase 0–3.0.4). Run them all. New tests:

- `ComponentStoreTests.cs` — unit tests for `Get / Has / Set / Add / Remove / EntityIds / Count`.
- `ComponentStoreRegistryTests.cs` — `Store<T>()` returns the same instance across repeat calls; cross-type dispatch is correct.
- `TagStoreTests.cs` (if shipping the specialisation) — empty-struct semantics.
- `EntityComponentAccessTests.cs` — `Entity.Get<T>()` returns the value stored via `Set<T>()`; `Has<T>()` reports correctly; `Remove<T>()` actually removes.
- `EntityManagerQueryTests.cs` — `Query<T>()` returns all entities with `T`; `Query<T1, T2>()` returns the intersection; both are deterministic in iteration order.
- `ComponentStorePerformanceTests.cs` — allocation drop + Get<T>() benchmark assertions.
- `ComponentStoreDeterminismTests.cs` — 5000-tick world with mixed component activity: byte-identical state across two seeds.
- `ComponentStoreFullRegressionTests.cs` — runs every existing simulation determinism test under the new storage; assertions unchanged.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Core/ComponentStore.cs` | New typed store. |
| code | `APIFramework/Core/ComponentStoreRegistry.cs` | New registry. |
| code | `APIFramework/Core/TagStore.cs` (optional) | Tag specialisation if shipped. |
| code | `APIFramework/Core/Entity.cs` (modified) | Replace dictionary with registry handle; preserve all public API. |
| code | `APIFramework/Core/EntityManager.cs` (modified) | Owns / threads the registry; `Query<T>()` and `Query<T1, T2>()` rewires. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Instantiate registry; thread to `EntityManager`. |
| code | `APIFramework.Tests/Core/ComponentStoreTests.cs` | Unit. |
| code | `APIFramework.Tests/Core/ComponentStoreRegistryTests.cs` | Unit. |
| code | `APIFramework.Tests/Core/TagStoreTests.cs` (optional) | Unit. |
| code | `APIFramework.Tests/Core/EntityComponentAccessTests.cs` | Entity surface. |
| code | `APIFramework.Tests/Core/EntityManagerQueryTests.cs` | Query semantics. |
| code | `APIFramework.Tests/Performance/ComponentStorePerformanceTests.cs` | Perf assertions. |
| code | `APIFramework.Tests/Determinism/ComponentStoreDeterminismTests.cs` | 5000-tick byte-identical. |
| code | `APIFramework.Tests/Regression/ComponentStoreFullRegressionTests.cs` | Sweeps existing determinism tests. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.0.5.md` | Completion note. **Specifically:** (a) the baseline allocation-per-tick number captured before refactor, (b) the post-refactor allocation-per-tick number, (c) the percentage reduction achieved, (d) the `Get<T>()` benchmark improvement (operations/sec before vs after), (e) whether tag-store specialisation was shipped or deferred, (f) any test that went red and how it was diagnosed, (g) any place in the codebase using non-generic component access (reflection-driven) that needed special handling, (h) whether the dual-storage migration step was used or single-step refactor was viable. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `ComponentStore<T>`, `ComponentStoreRegistry`, optional `TagStore<T>` compile and instantiate. | unit-test |
| AT-02 | `ComponentStore<T>.Set` then `Get` returns the same value byte-identical for value-type T (DriveComponent, StressComponent, TaskComponent, all the existing structs). | unit-test |
| AT-03 | `ComponentStore<T>.Has` returns true after `Set`, false after `Remove`. | unit-test |
| AT-04 | `Add` on an existing entity-id throws; `Set` overwrites; `Remove` is idempotent (removing absent entity is a no-op). | unit-test |
| AT-05 | `ComponentStoreRegistry.Store<T>()` returns the same instance for the same `T` across repeat calls. | unit-test |
| AT-06 | `Entity.Get<T>()` after `Set<T>(value)` returns `value`. Across all extant component types in `APIFramework/Components/`. | unit-test |
| AT-07 | `Entity.Has<T>()` correctly reports presence/absence for value-type and tag components. | unit-test |
| AT-08 | `EntityManager.Query<T>()` returns exactly the set of entities with `T` attached; iteration order is deterministic (sorted by `IntId` or `Guid`). | unit-test |
| AT-09 | `EntityManager.Query<T1, T2>()` returns the intersection; deterministic iteration. | unit-test |
| AT-10 | Allocation perf: at 30-NPC baseline running for 1000 ticks, allocation per tick drops by ≥ 80% vs the pre-refactor baseline (captured by Sonnet in completion note). | perf-test |
| AT-11 | `Get<T>()` micro-benchmark: 100k calls in a loop, ≥ 2× faster than pre-refactor baseline. | perf-test |
| AT-12 | 5000-tick byte-identical determinism on a representative simulation scenario (use the existing `MovementDeterminismTests` world or equivalent). | unit-test |
| AT-13 | **Full regression sweep:** every Phase 0, Phase 1, Phase 2, Phase 3.0.0–3.0.4 test passes byte-identical. **No exclusions, no skips.** | regression |
| AT-14 | Telemetry projection (`Warden.Telemetry.Projectors`) produces byte-identical `WorldStateDto` JSON before/after refactor on a representative tick. | regression |
| AT-15 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-16 | `dotnet test ECSSimulation.sln` — all green, no exclusions. | build + unit-test |
| AT-17 | `ECSCli ai describe` regenerates byte-identical to the pre-refactor output (component types, system list, all surfaced metadata are unchanged). | regression |

---

## Followups (not in scope)

- **`Span<T>` / `ref struct` API surfaces** for hot-path component access. Allows zero-copy reads. Constrains call sites; future packet once profiling identifies which paths benefit most.
- **Per-entity component bitmask** for ultra-fast `Has<T>()` checks. The current Dictionary lookup is fast but bitmasks are faster. Premature without profiling justification.
- **Sparse-set storage** for component types where most entities don't have the component. The current `Dictionary<Guid, T>` is fine for sparse usage; sparse-set is faster for very sparse + frequent-iteration patterns. Future once profiling identifies the components that benefit.
- **Component-add / component-remove events.** A future system might want to observe "this entity just gained a `LockedTag`." Substrate could be added to `ComponentStore<T>.Add / Remove` as a hook. Out of scope — speculative.
- **Component pooling.** For high-churn components (creation / destruction within a single tick), reuse instances. Not a real concern at v0.1; the existing churn is low.
- **Multi-entity batched access** — `store.GetMany(entityIds)` for vectorised reads. Speculative.
- **Multi-threading.** When and if the engine ever goes multithreaded, the stores need lock semantics. Single-threaded by SRD design today; defer.
- **Compile-time component type registration.** Currently `Store<T>()` lazy-registers on first access. A pre-registration pass at boot could allow array-based stores keyed by a compile-time index, eliminating even the `Dictionary<Type, object>` registry hop. Future.
- **`EntityManager.Query<T1, T2, T3>()` and beyond.** If existing code uses 3+ component queries via `Query<T1>().Where(npc => npc.Has<T2>() && npc.Has<T3>())`, a typed `Query<T1, T2, T3>()` overload is a small ergonomics win. Out of scope; ship the existing arity.
- **GC pressure dashboard for the WARDEN reports.** The cost ledger could include "ticks-per-GC" or similar, surfaced in the report aggregator. Future.
