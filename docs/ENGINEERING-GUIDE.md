# Engineering Guide — ECS Simulation Engine

> **Who this is for:** An engineer who wants to understand not just *what* this codebase does,
> but *why* every structural decision was made — and what those decisions teach about
> software architecture in general.
>
> This is the document that bridges "I can write code" and "I can design systems."

---

## Part 1 — The Problem This Engine Solves

Before any architecture makes sense, you have to understand the problem it is solving.

We need to simulate a living biological entity — metabolism, hunger, thirst, sleep, emotions, digestion — running at 60 ticks per second. Dozens of independent subsystems need to read and write state every tick. That state needs to be queryable, extensible, and testable in isolation.

The naive approach is an object like this:

```csharp
class Entity
{
    public float Satiation;
    public float Hydration;
    public float Energy;
    public float Sleepiness;
    public StomachData Stomach;
    public MoodData Mood;
    public DriveData Drives;
    // ...
}
```

Then a god-class `SimulationManager` that does everything in one big `Update()` method.

This approach breaks almost immediately:

- Adding a new subsystem (say, body temperature regulation) means editing the `Entity` class.
- Testing `MetabolismSystem` in isolation is impossible — it's tangled with everything else.
- Querying "which entities are hungry right now?" requires scanning every entity and checking its `Satiation` field.
- Running systems in parallel is impossible because everything shares the same mutable object.

The **Entity-Component-System (ECS) pattern** exists specifically to solve this class of problem.

---

## Part 2 — The ECS Pattern: Separation of Data from Logic

ECS splits three things that object-oriented programming tends to fuse together:

**Entities** are identities. Nothing more. In this engine an entity is just a Guid and a bag of components. It has no behaviour. It does not know what systems exist.

**Components** are data. Pure structs with no methods. `MetabolismComponent` holds `Satiation` and `Hydration`. It doesn't know how to drain them. It just holds the numbers.

**Systems** are logic. Stateless classes. Each system asks one question: "give me every entity with component X" — then reads, computes, and writes back. That's it.

The result is that **systems are functions over data**. A function that takes in a set of entities and returns modified entities is trivially testable, trivially parallelisable, and trivially composable. This is not a coincidence. The architecture was designed to produce these properties.

### Why structs for components?

Components are `struct`, not `class`. This is a deliberate choice. Structs are value types — they live on the stack or inline in collections, not as separate heap allocations. When a system reads a component and writes it back:

```csharp
var meta = entity.Get<MetabolismComponent>(); // copy onto the stack
meta.Satiation -= meta.SatiationDrainRate * deltaTime;
entity.Add(meta); // write back
```

The "read, modify, write back" pattern is explicit. You cannot accidentally mutate a component without writing it back. This prevents a class of bugs where a system modifies data mid-frame and other systems see inconsistent state.

The current implementation boxes these structs into `Dictionary<Type, object>`, which adds heap allocation. This is a known cost accepted in v0.7.x while the architecture stabilises. The fix (typed `ComponentStore<T>` arrays) is documented and deferred — but the public API (`Add<T>`, `Get<T>`) will not change when that fix lands. **This is what "stable abstraction over unstable implementation" looks like in practice.**

---

## Part 3 — The Query Problem: O(E) vs O(1)

This is one of the most important performance decisions in the engine, and it illustrates a principle that matters everywhere in systems programming.

### The original implementation

```csharp
public IEnumerable<Entity> Query<T>() where T : struct
{
    return _entities.Where(e => e.Has<T>());
}
```

This is clean, readable, and correct. It is also **O(E)** — it scans every entity every time. With 13 systems × 60 fps, that is 780 full scans per second. At 100 entities this is nanoseconds. At 10,000 entities it dominates your frame time.

This is the pattern of "code that is slow at the wrong scale." It looks fine until it breaks, and then you have a production performance problem instead of an architecture decision.

### The fix: a component-type index

```csharp
// EntityManager maintains this internally:
Dictionary<Type, HashSet<Entity>> _componentIndex

// Query<T>() now returns the pre-built bucket:
public IEnumerable<Entity> Query<T>() where T : struct
{
    return _componentIndex.TryGetValue(typeof(T), out var bucket)
        ? bucket : [];
}
```

`Query<T>()` is now **O(1)**. It does a single dictionary lookup and returns a pre-built set. No scanning, no LINQ, no allocation.

The index is kept up to date via a callback that `Entity` fires whenever a component type is added or removed:

```csharp
// Entity fires this callback once per new type
_onChange?.Invoke(this, typeof(T), true);  // added
_onChange?.Invoke(this, typeof(T), false); // removed
```

`EntityManager` wires itself into that callback on `CreateEntity` and updates the index accordingly.

### Why `HashSet<Entity>` and not `List<Entity>`?

Two reasons:

1. `DestroyEntity` cleanup is **O(1) per bucket** with a `HashSet`, vs **O(N) per bucket** with a `List`. When an entity dies, you just call `bucket.Remove(entity)` — no searching.

2. The callback only fires on the *first* Add of a given type — overwriting an existing component does not fire it. So there is no risk of duplicate entries. A `HashSet` enforces uniqueness as a safety net anyway.

### The architectural lesson

The lesson here is not "use a dictionary instead of LINQ." The lesson is: **identify your query patterns early and build your data structures to serve them**.

The question `EntityManager` needs to answer millions of times per second is "which entities have component T?" Building a data structure that answers that question in O(1) is the correct architectural response. Any time you have a hot-path query, you should ask: "could I pre-index this answer so the query is free?"

This is how database query optimisation works. This is how game engine archetype systems work. This is how every high-performance system that queries large datasets works. The pattern is universal.

---

## Part 4 — The SystemPhase Model: Encoding Data Dependencies

Before v0.7.2, systems were registered in a flat list:

```csharp
engine.AddSystem(metabolismSystem);
engine.AddSystem(energySystem);
engine.AddSystem(feedingSystem);
// ...
```

This works but it hides something important. Some systems *must* run before others because they produce data that others consume. `MetabolismSystem` must drain `Satiation` before `BiologicalConditionSystem` reads it to set `HungerTag`. `BrainSystem` must score drives before `FeedingSystem` acts on them.

With a flat list, these constraints are invisible. Someone adds a new system, puts it in the wrong position, and a subtle ordering bug appears. The code looks fine. The tests pass individually. But the simulation produces wrong results because the data dependencies aren't respected.

### The fix: explicit phases

```csharp
public enum SystemPhase
{
    PreUpdate  = 0,
    Physiology = 10,
    Condition  = 20,
    Cognition  = 30,
    Behavior   = 40,
    Transit    = 50,
    World      = 60,
    PostUpdate = 100,
}
```

Now every `AddSystem` call declares a phase:

```csharp
engine.AddSystem(new MetabolismSystem(),      SystemPhase.Physiology);
engine.AddSystem(new BiologicalConditionSystem(...), SystemPhase.Condition);
engine.AddSystem(new BrainSystem(...),        SystemPhase.Cognition);
engine.AddSystem(new FeedingSystem(...),      SystemPhase.Behavior);
```

The phase name is a **statement of data dependency**. `Condition` systems read data produced by `Physiology` systems. `Cognition` systems read data produced by `Condition` systems. The ordering constraint is now visible in the code, not hidden in insertion order.

### Systems within a phase are logically independent

Systems registered in the same phase declare that they don't share write targets. `FeedingSystem`, `DrinkingSystem`, and `SleepSystem` are all in `Behavior` because they each act on a different drive — they produce different outputs and don't conflict.

This is the precondition for parallelism. When parallel execution is added in v0.8+:

```csharp
// Each phase becomes a Task.WhenAll over its member systems:
foreach (var phase in _orderedCache.GroupBy(r => r.Phase))
{
    await Task.WhenAll(phase.Select(r =>
        Task.Run(() => r.System.Update(EntityManager, scaledDelta))));
    // Phase barrier: all writes visible before next phase begins
}
```

No system code changes. No `AddSystem` calls change. The parallelism is pure infrastructure — possible because the data dependencies were encoded correctly in the phase model.

### The architectural lesson

**Make implicit constraints explicit in your data model.** A flat list of systems looks simpler than a phased list. But the flat list hides the real structure — it just happens to work because someone put things in the right order. The phased list documents the structure. Future engineers can read it and understand the data flow without reading every system's source code.

This principle shows up everywhere in architecture: foreign keys in databases make implicit relationships explicit. Type systems make implicit data contracts explicit. Interface definitions make implicit behavioural contracts explicit. The pattern is always the same: find the implicit constraint, make it visible, make the compiler or runtime enforce it.

---

## Part 5 — The NutrientProfile Bug: What It Teaches About Data Contracts

In v0.7.2 testing, the simulation ran for a full day without Billy eating once. The stomach filled to 1000ml. Satiation never changed. The bug was one missing option in one JSON deserialiser call:

```csharp
// Before fix:
private static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNameCaseInsensitive = true,
};

// After fix:
private static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNameCaseInsensitive = true,
    IncludeFields = true, // ← this
};
```

`NutrientProfile` uses public **fields**, not **properties**. `System.Text.Json` ignores fields by default. Every nutrient value in every food item silently deserialised to zero. The stomach filled with volume but no nutritional content. The bug produced no exception, no warning, and no immediately obvious symptom — just a simulation that didn't work.

### What this teaches

**The contract between your data model and your serialisation layer is fragile.** In C#, the difference between a field and a property is invisible to callers but significant to serialisers, ORMs, XAML bindings, and reflection-based tools. Every time you use a serialiser, you are trusting that it understands your data model the same way the compiler does.

The correct defensive practices:

1. **Write integration tests that exercise the full data pipeline.** A unit test of `DigestionSystem` with hardcoded values would not have caught this. The smoke test that runs the simulation end-to-end catches the symptom: after a full day, `NutrientStores.Carbohydrates > 0` must be true. That test would have caught this the moment it was written.

2. **Be explicit about serialisation contracts.** Don't rely on defaults. Explicitly specify every option. `IncludeFields = true`, `PropertyNameCaseInsensitive = true` — each of these is a statement of intent. Future engineers reading this code know exactly what the serialiser is doing and why.

3. **Silent failures are worse than loud failures.** This bug failed silently across the entire pipeline. A better design would have validated that nutrient values are non-zero after deserialisation. Making this invariant explicit — either as an assertion or as a test — would have surfaced the bug at startup rather than mid-run.

---

## Part 6 — Unit Testing: Why It Enables Good Architecture

There is a feedback loop between architecture quality and testability that most engineers don't notice until they've experienced it.

When you try to write a unit test for `MetabolismSystem`, you discover immediately what its dependencies are:

```csharp
// MetabolismSystem.Update(EntityManager em, float deltaTime)
//
// Dependencies:
//   - EntityManager (pure in-memory)
//   - float (a number)
//
// No: databases, file I/O, network, clocks, random numbers
```

`MetabolismSystem` is trivially testable because it is trivially isolated. It takes in data and produces data. There is no hidden state, no external dependency, no global variable.

Now imagine trying to test the original god-class `SimulationManager` that did everything in one `Update()` method. To test "does metabolism drain correctly when the entity is sleeping?" you would need to construct the entire simulation — all 13 systems, the full entity state, the clock, the config — just to test one drain calculation. The test becomes a miniature integration test, which is slow, fragile, and hard to read.

**The architectural lesson: if code is hard to test, the architecture is telling you something.** Hard-to-test code almost always has one of these problems: too many dependencies, too much shared mutable state, or too many responsibilities fused into one class.

When you practice writing tests first (or alongside the code), you are forced to confront these problems immediately. The test forces you to ask: "what does this function actually need?" And the answer shapes the design.

In this engine:
- Systems are stateless → they are functions over data → they are trivially testable
- Components are structs → data is explicit → test setup is straightforward
- The phase model encodes dependencies → you know what to set up for each test → tests stay small

The 143 tests in this suite are not just a safety net. They are a **specification** — a machine-executable record of what every system is supposed to do. When you add intestines in v0.8, you run `dotnet test` and find out immediately if you broke something. No manual testing required.

---

## Part 7 — The Architecture of a Framework Engineer

You said you want to become a software architect and framework engineer. Here is the honest map of what that requires.

### The shift from writing features to designing contracts

Junior engineers write code that solves a specific problem. Senior engineers write code that makes it easy to solve a *class* of problems. Framework engineers write the substrate on which others build.

In this engine, the framework is: `Entity`, `EntityManager`, `ISystem`, and `SimulationEngine`. These four things define the contract. Everything else — every system, every component — is built on top of them.

The sign of good framework design is that **adding a new system requires zero changes to the framework**. When we added `DigestionSystem`, we did not touch `Entity` or `EntityManager`. We just wrote a class that implements `ISystem` and registered it. The framework is closed for modification and open for extension. This is the **Open/Closed Principle** — one of the most important ideas in software design.

### The shift from "does it work?" to "what are its failure modes?"

Junior engineers ask "does this work?" Senior engineers ask "under what conditions does this fail, and how does it fail?"

The v0.7.2 performance work is a good example. `Query<T>()` with a linear scan *worked* at 100 entities. The senior question is: "what happens at 10,000? What happens at 1,000,000?" The answer forced an architectural change *before* those scales were hit. That is proactive design rather than reactive firefighting.

### The shift from solving problems to choosing which problems to solve

The decision to defer `ComponentStore<T>` typed arrays is documented in `ARCHITECTURE.md` under "what was explicitly not done." This is as important as what *was* done. Every premature optimisation is complexity you pay for in every future refactor. Deferring boxing fixes until the architecture is stable is a deliberate, reasoned choice — not laziness.

Architectural maturity is largely the ability to say "not yet" with confidence. You know the problem exists. You know the fix. You choose not to apply it because the cost-benefit ratio is wrong at this scale. You document the decision so it can be revisited. This is much harder than just writing the fix.

### What to study next

If you are serious about becoming a framework engineer and software architect, here is the honest reading list:

**Foundational patterns:**
"Design Patterns" (Gang of Four) — learn Observer, Strategy, Command, Composite. They appear in every framework ever written. The onChange callback in this engine is an Observer.

**Data structures and algorithms:**
Not for interviews. For intuition about O(N) vs O(1) vs O(log N). Every architectural decision about indexing, querying, and caching is an algorithms decision. You cannot design query systems without understanding hash tables.

**Domain-Driven Design (Evans):**
For understanding how to model a problem domain correctly before writing a line of code. The `NutrientProfile` type is a value object in DDD terms. Understanding this vocabulary makes communication with other architects much cleaner.

**A Philosophy of Software Design (Ousterhout):**
The best modern book on software design thinking. Specifically the chapter on "deep modules" — good modules hide complexity behind simple interfaces. `Query<T>()` is a deep module: it hides a complex indexing mechanism behind a one-line call.

**The actual code:**
Read framework source code. ASP.NET Core's dependency injection container. EF Core's query provider. Unity's ECS (DOTS). Bevy (Rust ECS). Read them not to understand the details but to see how the framework/application boundary is drawn and why.

---

## Appendix — The Current State of This Engine

| Layer | Status | Notes |
|:---|:---|:---|
| Entity identity | Stable | Guid; migrate to int when ComponentStore<T> lands |
| Component storage | Working, known cost | Dictionary<Type,object>; boxing accepted for v0.7.x |
| Query index | O(1), complete | HashSet<Entity> per type, maintained via onChange |
| System phases | Defined, sequential | Parallel execution ready in v0.8+ |
| Digestive pipeline | Stomach only | SmallIntestine/LargeIntestine deferred to v0.8 |
| Spatial index | Interface stub only | ISpatialIndex defined; no implementation until v0.9 |
| Unit test coverage | 143 tests | All systems covered; smoke test validates full pipeline |
| Parallelism | Not yet | Phase model is the precondition; command queue next |

The engine is at the stage where the architecture is solid and tested, the biology is working, and the foundation is ready for the next layer. The next meaningful additions — intestines, position/world grid, parallel execution — can all be made without changing the core framework. That is the proof that the architecture is doing its job.

---

*End of ENGINEERING-GUIDE.md — v0.7.2*
