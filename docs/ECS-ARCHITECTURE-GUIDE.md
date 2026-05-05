# ECS Simulation Engine — Architecture & Developer Guide

> **Who this is for:** A developer working toward senior/architect level who wants to
> understand not just *what* this engine does, but *how every pattern fits together*,
> *why* each structural decision was made, and *how this relates to Unity DOTS*.
>
> Read `ENGINEERING-GUIDE.md` first — it covers the foundational reasoning.
> This document builds on that foundation with the full current picture.

---

## Table of Contents

1. [What We Built — The Full System](#1-what-we-built)
2. [The ECS Trinity in This Engine](#2-the-ecs-trinity)
3. [Entity — Identity and Component Storage](#3-entity)
4. [Components — The Full Catalog](#4-components)
5. [Systems — The Full Catalog and Phase Model](#5-systems-and-phases)
6. [EntityManager — O(1) Querying](#6-entitymanager)
7. [The onChange Subscription Pattern](#7-onchange-subscription-pattern)
8. [SimulationClock — Scaled Time](#8-simulationclock)
9. [SimulationBootstrapper — The Composition Root](#9-simulationbootstrapper)
10. [SimulationSnapshot — The Decoupled Data Contract](#10-simulationsnapshot)
11. [Multi-Frontend Architecture](#11-multi-frontend-architecture)
12. [The Unity Visualizer Layer](#12-the-unity-visualizer-layer)
13. [This Engine vs Unity DOTS](#13-this-engine-vs-unity-dots)
14. [What to Study Next](#14-what-to-study-next)

---

## 1. What We Built

This is a **headless ECS simulation engine** that models the full biology of a living entity
(metabolism, hunger, thirst, sleep, mood, digestion, elimination, spatial movement) running
at configurable time-scale, with three completely independent frontends reading from it:

```
+-------------------------------------------------------------+
|                     APIFramework.dll                        |
|                  (netstandard2.1 class library)             |
|                                                             |
|   EntityManager  →  SimulationEngine  →  SimulationClock   |
|        ↑                   ↑                                |
|   20 Systems across 8 Phases                               |
|        ↑                                                    |
|   SimulationBootstrapper  (composition root)               |
|        ↓                                                    |
|   SimulationSnapshot  (immutable per-frame data contract)  |
+-------------------------------------------------------------+
         ↓                   ↓                    ↓
   CLI Renderer        Avalonia GUI         Unity 6 Visualizer
   (terminal)        (real-time charts)   (3D + HUD overlay)
```

Key numbers today:
- **1 simulation entity** (Billy, human)  
- **4 world objects** (Fridge, Sink, Bed, Toilet)  
- **20+ component types**  
- **20 systems** across **8 execution phases**  
- **3 frontends** sharing one engine, zero coupling between them  
- **Time scale 120×** — one real second = two in-game minutes  

---

## 2. The ECS Trinity

ECS separates three things that OOP fuses together:

| Concept | What it is | What it is NOT |
|---------|-----------|----------------|
| **Entity** | A unique identity (Guid + component bag) | Has no behaviour, no methods |
| **Component** | Pure data (a struct) | Has no logic, knows no systems |
| **System** | Logic that operates on a filtered set of entities | Holds no per-entity state |

The result: systems are **pure functions over data**.

```
Entity (Guid: abc123)
  +-- MetabolismComponent  { Satiation: 72.4, Hydration: 81.0 }
  +-- EnergyComponent      { Energy: 88.0, IsSleeping: false }
  +-- StomachComponent     { CurrentVolumeMl: 150, DigestionRate: 0.017 }
  +-- DriveComponent       { Dominant: Eat, EatUrgency: 65.3 }
  +-- PositionComponent    { X: 4.2, Y: 0, Z: 6.1 }
  +-- MoodComponent        { Joy: 12.0, Anticipation: 38.0, ... }
```

`MetabolismSystem` asks EntityManager: *"give me every entity with a MetabolismComponent"*.
It reads `Satiation` and `Hydration`, applies drain rates scaled by deltaTime, and writes
back. It does not know about EnergyComponent, DriveComponent, or anything else.

---

## 3. Entity — Identity and Component Storage

**File:** `APIFramework/Core/Entity.cs`

```csharp
public sealed class Entity
{
    public Guid   Id      { get; }
    public string ShortId { get; }   // first 8 chars — for logging

    private readonly Dictionary<Type, object> _components = new();
    private readonly Action<Entity, Type, bool>? _onChange;

    public void Add<T>(T component)    where T : struct { ... }
    public T    Get<T>()               where T : struct { ... }
    public bool Has<T>()               where T : struct { ... }
    public void Remove<T>()            where T : struct { ... }
}
```

### Why `Dictionary<Type, object>` for storage?

Each entity holds components in a `Dictionary<Type, object>`. The key is the *type* of the
component, which means:
- `entity.Has<MetabolismComponent>()` is an O(1) dictionary lookup.
- An entity can hold **at most one** of any component type. This is intentional — it maps
  directly to the biological reality (one stomach, one bladder, etc.).
- Adding a component that already exists **overwrites it** (the dict assignment replaces the
  value). This is how movement updates work: `entity.Add(new PositionComponent { X=... })`
  is a safe in-place update.

### Why `struct` components?

Components are structs, not classes. When you call `entity.Get<MetabolismComponent>()` you
get a **copy** of the struct. When you write back, you call `entity.Add(updatedCopy)`. This
means:
1. No accidental mutation by reference — a system that forgets to write back simply has no
   effect. The original is unchanged.
2. No heap allocation per access — struct copies live on the stack.
3. Clear data ownership — each `Add()` call is an explicit write.

> **Common mistake:** Writing `var meta = entity.Get<MetabolismComponent>(); meta.Satiation -= 5;`
> and then doing nothing. Because `meta` is a copy, the entity is unchanged. You must
> call `entity.Add(meta)` to commit the change.

---

## 4. Components — The Full Catalog

Components are grouped here by the domain they belong to. Each is a `struct` in
`APIFramework/Components/`.

### 4.1 Identity

| Component | Key Fields | Who Uses It |
|-----------|-----------|-------------|
| `IdentityComponent` | `Name`, `Role` | All systems that need a display name |

### 4.2 Physiology (Biological State)

| Component | Key Fields | Who Writes | Who Reads |
|-----------|-----------|-----------|-----------|
| `MetabolismComponent` | `Satiation`, `Hydration`, `BodyTemp`, drain rates | `MetabolismSystem` | `BiologicalConditionSystem`, `BrainSystem`, Snapshot |
| `EnergyComponent` | `Energy`, `Sleepiness`, `IsSleeping`, rates | `EnergySystem`, `SleepSystem` | `BrainSystem`, Snapshot |
| `MoodComponent` | `Joy`, `Trust`, `Fear`, `Surprise`, `Sadness`, `Disgust`, `Anger`, `Anticipation` | `MoodSystem` | `BrainSystem`, Snapshot |
| `StomachComponent` | `CurrentVolumeMl`, `DigestionRate`, `NutrientsQueued` | `DigestionSystem`, `FeedingSystem` | `FeedingSystem`, `DrinkingSystem`, Snapshot |

### 4.3 Drive & Cognition

| Component | Key Fields | Who Writes | Who Reads |
|-----------|-----------|-----------|-----------|
| `DriveComponent` | `Dominant` (DesireType), urgency scores for each drive | `BrainSystem` | All behavior systems, Snapshot |

`DesireType` is an enum: `None`, `Eat`, `Drink`, `Sleep`, `Defecate`, `Pee`.

`BrainSystem` scores each drive each tick (circadian weight, urgency, sleepiness) and
writes the winner to `DriveComponent.Dominant`. Behavior systems only act if their drive
is dominant. This creates a **clean separation between cognition and action**.

### 4.4 Digestive Pipeline (GI Tract)

These components form a physical pipeline — content flows forward, each system passing
residue to the next:

```
Esophagus (transit) → Stomach → SmallIntestine → LargeIntestine → Colon → expelled
                                                                 ↓
                                                              Bladder (via fill rate)
```

| Component | Key Fields | Description |
|-----------|-----------|-------------|
| `EsophagusTransitComponent` | `Progress` (0→1), `Speed`, `TargetEntityId` | On **food/water entities**, not on the eater. Marks content in transit. |
| `BolusComponent` | `Volume`, `FoodType`, `Toughness`, `NutrientsQueued` | Marks a food entity. Created by FeedingSystem. |
| `LiquidComponent` | `LiquidType`, `VolumeMl` | Marks a water entity. Created by DrinkingSystem. |
| `SmallIntestineComponent` | `ChymeVolumeMl`, `AbsorptionRate`, `Chyme` (NutrientProfile), `Fill` | Absorbs nutrients from chyme |
| `LargeIntestineComponent` | `ContentVolumeMl`, `WaterReabsorptionRate`, `MobilityRate`, `Fill` | Reabsorbs water, forms stool |
| `ColonComponent` | `StoolVolumeMl`, `UrgeThresholdMl`, `CapacityMl`, `Fill`, `HasUrge`, `IsCritical` | Accumulates stool until DefecationUrge |
| `BladderComponent` | `VolumeML`, `FillRate`, `UrgeThresholdMl`, `CapacityMl`, `Fill`, `HasUrge`, `IsCritical` | Fills continuously at constant rate |

> **Key insight:** `EsophagusTransitComponent` lives on the **food entity**, not on
> the eater. This is a deliberate modelling choice — the food is what is "in transit",
> not the person. It means you can query all items currently in someone's esophagus with
> `em.Query<EsophagusTransitComponent>().Where(e => e.Get<EsophagusTransitComponent>().TargetEntityId == billyId)`.

### 4.5 Condition Tags (Read-Only Signals)

These are **tag components** — empty or near-empty structs added/removed to signal a
condition. They have no data fields; their **presence is the message**.

| Tag | Meaning | Added by | Read by |
|-----|---------|---------|---------|
| `HungryTag` | Satiation below threshold | `BiologicalConditionSystem` | `BrainSystem` |
| `ThirstyTag` | Hydration below threshold | `BiologicalConditionSystem` | `BrainSystem` |
| `StarvedTag` | Satiation critically low | `BiologicalConditionSystem` | `BrainSystem`, `MoodSystem` |
| `DehydratedTag` | Hydration critically low | `BiologicalConditionSystem` | `BrainSystem`, `DrinkingSystem` |
| `IrritableTag` | Hunger + thirst simultaneously | `BiologicalConditionSystem` | `MoodSystem` |
| `ExhaustedTag` | Energy below threshold | `BiologicalConditionSystem` | `BrainSystem` |
| `DefecationUrgeTag` | Colon at urge threshold | `ColonSystem` | `BrainSystem` |
| `BowelCriticalTag` | Colon near capacity | `ColonSystem` | `BrainSystem` |
| `UrinationUrgeTag` | Bladder at urge threshold | `BladderSystem` | `BrainSystem` |
| `BladderCriticalTag` | Bladder near capacity | `BladderSystem` | `BrainSystem` |
| `ConsumedRottenFoodTag` | Eater ate rotten food | `FeedingSystem` | `MoodSystem` |
| `RotTag` | Food has reached rot threshold | `RotSystem` | `FeedingSystem` |

Tag components are a **publish/subscribe mechanism without an event bus**. Systems write
tags; other systems poll for them. Because every system runs every tick, the latency
between "tag added" and "tag read" is at most one frame.

### 4.6 Food Lifecycle

| Component | Key Fields | Description |
|-----------|-----------|-------------|
| `RotComponent` | `AgeSeconds`, `RotLevel`, `RotStartAge`, `RotRate` | Ages food; RotSystem applies `RotTag` when threshold reached |
| `StoredTag` | — | Marks food inside a container (fridge); prevents FeedingSystem from treating it as floor food |

### 4.7 Spatial Layer

| Component | Key Fields | Description |
|-----------|-----------|-------------|
| `PositionComponent` | `X`, `Y`, `Z` | World position. Added to living entities and world objects. |
| `MovementComponent` | `Speed` (units/game-s), `ArrivalDistance` | Movement capability |
| `MovementTargetComponent` | `TargetEntityId`, `Label` | Present while navigating to a world object. Removed on arrival by `MovementSystem`. |

### 4.8 World Objects

| Component | Key Fields | Description |
|-----------|-----------|-------------|
| `FridgeComponent` | `FoodCount` | Marks the fridge entity. FoodCount decrements each time Billy eats. |
| `SinkComponent` | — | Tag. Marks the sink (water source). |
| `BedComponent` | — | Tag. Marks the bed (sleep location). |
| `ToiletComponent` | — | Tag. Marks the toilet (defecation/urination target). |

---

## 5. Systems and Phases

Systems are the **only place logic lives**. They are stateless (or nearly so) classes
implementing `ISystem`:

```csharp
public interface ISystem
{
    void Update(EntityManager em, float deltaTime);
}
```

`deltaTime` is **game-time seconds** (already scaled by TimeScale). At TimeScale=120 and
60fps, `deltaTime ≈ 1.92` game-seconds per frame.

### 5.1 The Phase Model

Phases encode **data dependency ordering**. Systems within the same phase are logically
independent (they don't share write targets). Phases run in ascending numeric order.

```
Phase 0   PreUpdate     InvariantSystem
             |  (clamp impossible values — safety net for all downstream)
             ▼
Phase 10  Physiology    MetabolismSystem   EnergySystem   BladderFillSystem
             |  (raw biological drain/restore — produces the numbers)
             ▼
Phase 20  Condition     BiologicalConditionSystem
             |  (reads numbers, writes condition TAGS — observation only)
             ▼
Phase 30  Cognition     MoodSystem   BrainSystem
             |  (reads tags+numbers, writes emotion state + dominant drive)
             ▼
Phase 40  Behavior      FeedingSystem  DrinkingSystem  SleepSystem
             |          DefecationSystem  UrinationSystem
             |  (reads dominant drive, triggers actions)
             ▼
Phase 50  Transit       InteractionSystem  EsophagusSystem  DigestionSystem
             |  (moves content through upper GI: esophagus → stomach)
             ▼
Phase 55  Elimination   SmallIntestineSystem  LargeIntestineSystem
             |          ColonSystem  BladderSystem
             |  (lower GI pipeline: intestines → colon/bladder → tags)
             ▼
Phase 60  World         RotSystem   MovementSystem
             (environmental: age food, move entities)
```

### 5.2 The Full System Reference

#### `InvariantSystem` (PreUpdate)
- **Reads:** All physiology components  
- **Writes:** Clamps Satiation, Hydration, Energy, Sleepiness to [0, 100]; logs violations  
- **Why first:** If any value drifts out of range (floating-point accumulation, config bug),
  downstream systems must not see the invalid state. This is the safety net.

#### `MetabolismSystem` (Physiology)
- **Reads:** `MetabolismComponent`, `EnergyComponent.IsSleeping`  
- **Writes:** Decrements `Satiation` and `Hydration` each tick. During sleep, applies
  `SleepMetabolismMultiplier` (typically 0.10) so overnight drain is realistic.

#### `EnergySystem` (Physiology)
- **Reads:** `EnergyComponent`  
- **Writes:** Drains `Energy` and builds `Sleepiness` while awake. Restores `Energy` and
  drains `Sleepiness` while sleeping.

#### `BladderFillSystem` (Physiology)
- **Reads:** `BladderComponent`  
- **Writes:** Increments `BladderComponent.VolumeML` at a constant rate every tick. The
  bladder fills continuously regardless of hydration level (simplified physiology).

#### `BiologicalConditionSystem` (Condition)
- **Reads:** `MetabolismComponent`, `EnergyComponent`, configurable thresholds  
- **Writes:** Adds/removes `HungryTag`, `ThirstyTag`, `StarvedTag`, `DehydratedTag`,
  `IrritableTag`, `ExhaustedTag`  
- **Pattern:** This system *observes* — it never modifies physiology values, only produces
  signal tags that the cognition layer can read.

#### `MoodSystem` (Cognition)
- **Reads:** Condition tags, `MoodComponent`, config  
- **Writes:** Decays all emotions each tick; gains `Joy` when all needs are met; gains
  `Anger` / `Sadness` when needs are unmet; spikes `Disgust` on rotten food tag; writes
  Plutchik intensity tags (low/mid/high thresholds).

#### `BrainSystem` (Cognition)
- **Reads:** All condition tags, `DriveComponent`, `EnergyComponent`, circadian clock  
- **Writes:** Scores every drive (EatUrgency, DrinkUrgency, SleepUrgency, etc.) and writes
  the highest-scoring drive as `Dominant` in `DriveComponent`.  
- **Key mechanics:** Circadian factor boosts `SleepUrgency` at night; colon/bladder
  critical tags override other drives when sufficiently urgent.

#### `FeedingSystem` (Behavior)
- **Reads:** `DriveComponent` (Dominant == Eat), `MetabolismComponent`, `StomachComponent`,
  fridge `FridgeComponent.FoodCount`, entity `PositionComponent`  
- **Writes:** If Billy is hungry, not at fridge → sets `MovementTargetComponent` to fridge.
  If at fridge and food remains → creates `BolusComponent` + `EsophagusTransitComponent`,
  decrements `FridgeComponent.FoodCount`. If fridge empty → nothing (starvation).

#### `DrinkingSystem` (Behavior)
- **Reads:** `DriveComponent` (Dominant == Drink), `StomachComponent`  
- **Writes:** Creates a `LiquidComponent` entity + `EsophagusTransitComponent` (a gulp of
  water sent to the esophagus immediately, no proximity check yet).

#### `SleepSystem` (Behavior)
- **Reads:** `DriveComponent`, `EnergyComponent`  
- **Writes:** Toggles `EnergyComponent.IsSleeping`. The brain sets Dominant; this system
  acts on it. Enforces a `WakeThreshold` — entity stays asleep until Sleepiness drops low
  enough even if the brain nominally wants to wake.

#### `DefecationSystem` / `UrinationSystem` (Behavior)
- **Reads:** `DriveComponent` (Dominant == Defecate/Pee), `ColonComponent` / `BladderComponent`  
- **Writes:** Zeros `StoolVolumeMl` / `VolumeML` — the act of elimination.

#### `InteractionSystem` (Transit)
- **Purpose:** Converts a food/liquid entity that an eater has "picked up" into esophagus
  transit. In future this will handle hand→mouth; currently FeedingSystem directly creates
  transit items.

#### `EsophagusSystem` (Transit)
- **Reads:** `EsophagusTransitComponent`  
- **Writes:** Increments `Progress` by `Speed * deltaTime`. When `Progress >= 1.0`, the
  content has reached the stomach — the transit component is removed and stomach volume
  is increased.

#### `DigestionSystem` (Transit)
- **Reads:** `StomachComponent`  
- **Writes:** Each tick digests `DigestionRate * deltaTime` ml from the stomach; converts
  nutrients to satiation/hydration via config multipliers; passes `ResidueToLarge` fraction
  of digested volume as chyme to `SmallIntestineComponent`.

#### `SmallIntestineSystem` (Elimination)
- **Reads:** `SmallIntestineComponent`  
- **Writes:** Drains chyme at `AbsorptionRate`; passes `ResidueToLargeFraction` to
  `LargeIntestineComponent`.

#### `LargeIntestineSystem` (Elimination)
- **Reads:** `LargeIntestineComponent`  
- **Writes:** Reabsorbs water at `WaterReabsorptionRate`; forms stool at `StoolFraction`
  rate; passes formed stool to `ColonComponent.StoolVolumeMl`.

#### `ColonSystem` / `BladderSystem` (Elimination)
- **Reads:** `ColonComponent` / `BladderComponent`  
- **Writes:** Applies `DefecationUrgeTag` / `UrinationUrgeTag` when fill exceeds
  `UrgeThresholdMl`; applies `BowelCriticalTag` / `BladderCriticalTag` near capacity.

#### `RotSystem` (World)
- **Reads:** `RotComponent` on food entities  
- **Writes:** Increments `AgeSeconds`; computes `RotLevel`; adds `RotTag` when
  `RotLevel >= config.RotTagThreshold`.

#### `MovementSystem` (World)
- **Reads:** `PositionComponent`, `MovementComponent`, `MovementTargetComponent` (if present)  
- **Writes:** Moves entity toward target or wander destination; removes
  `MovementTargetComponent` on arrival; updates `PositionComponent`.

---

## 6. EntityManager — O(1) Querying

**File:** `APIFramework/Core/EntityManager.cs`

The `EntityManager` owns all entities. Its critical feature is the **component-type index**:

```csharp
private readonly Dictionary<Type, HashSet<Entity>> _index = new();
```

When any entity calls `Add<T>()` or `Remove<T>()`, the `_onChange` callback fires, and
`EntityManager` adds/removes that entity from `_index[typeof(T)]`.

The result: `Query<MetabolismComponent>()` returns the pre-filtered set in O(1) — no scan
over all entities.

```csharp
// WITHOUT index: O(E) — scan every entity, check if it has the component
foreach (var e in _entities)
    if (e.Has<MetabolismComponent>()) yield return e;

// WITH index: O(1) — the set is already built
return _index.TryGetValue(typeof(T), out var set) ? set : Enumerable.Empty<Entity>();
```

**Why this matters at scale:** With 1,000 entities and 15 systems each calling Query<T>
60 times per second = 900,000 entity scans per second without index vs. 15,000 set lookups
with index. The index makes the O(1) approach structurally sound for large simulations.

---

## 7. The onChange Subscription Pattern

This is the **single notification mechanism** in the engine. It is how `EntityManager`
stays aware of component changes without polling.

```csharp
// EntityManager creates entities with a callback wired to itself:
public Entity CreateEntity()
{
    var e = new Entity(Guid.NewGuid(), OnEntityChanged);
    _entities.Add(e);
    return e;
}

private void OnEntityChanged(Entity entity, Type componentType, bool added)
{
    if (added)
    {
        if (!_index.TryGetValue(componentType, out var set))
            _index[componentType] = set = new HashSet<Entity>();
        set.Add(entity);
    }
    else
    {
        if (_index.TryGetValue(componentType, out var set))
            set.Remove(entity);
    }
}
```

When `entity.Add<MetabolismComponent>(...)` is called inside `Entity.Add<T>()`:

```csharp
public void Add<T>(T component) where T : struct
{
    bool isNew = !_components.ContainsKey(typeof(T));
    _components[typeof(T)] = component;   // store the data
    if (isNew) _onChange?.Invoke(this, typeof(T), true);  // notify only on first add
}
```

Note the `if (isNew)` guard — **overwriting an existing component does not fire the
callback**. This is intentional: updating a position 60 times per second should not
trigger index maintenance.

### Why callback instead of events?

A `delegate Action<Entity, Type, bool>` (a callback) instead of `event` or a full
observer list is a deliberate choice:
- There is exactly **one subscriber**: the `EntityManager` that created the entity.
- A delegate is a single function pointer — zero allocation overhead per invocation.
- It keeps `Entity` decoupled from `EntityManager` — Entity does not import or know about
  EntityManager. The coupling flows one way: EntityManager knows Entity; Entity does not
  know EntityManager.

### Comparison to Unity Events / C# Events

Unity's `UnityEvent` and C#'s `event` keyword both support **multiple subscribers**. That
generality has a cost: every subscriber must be registered and deregistered; memory leaks
are common; ordering of notification is implicit.

This engine's onChange pattern is narrower — one subscriber, one purpose — and therefore
more efficient and less error-prone. If you needed multiple subscribers (say, both an index
and a debug logger), you would wrap them: `(e, t, a) => { Index(e,t,a); Debug(e,t,a); }`.

---

## 8. SimulationClock — Scaled Time

**File:** `APIFramework/Core/SimulationClock.cs`

```csharp
public class SimulationClock
{
    public float TimeScale { get; set; }  // default 120 — 1 real-s = 120 game-s = 2 game-min

    public float Tick(float realDeltaTime)
    {
        float scaled = realDeltaTime * TimeScale;
        TotalGameSeconds += scaled;
        return scaled;
    }

    public float CircadianFactor { get; }  // 0.0 (midnight) → 1.0 (noon) → 0.0 (midnight)
    public string GameTimeDisplay { get; } // "06:15"
}
```

`SimulationEngine.Update(float realDeltaTime)` calls `Clock.Tick(realDeltaTime)` first,
gets back `scaledDelta`, then passes `scaledDelta` to every system. Systems never see
real time — they always work in game-seconds. This decoupling means:
- Pausing: set `TimeScale = 0`.
- Fast-forward: set `TimeScale = 1200` (20× faster than default).
- Testing: pass any deltaTime you want — the simulation is deterministic.

---

## 9. SimulationBootstrapper — The Composition Root

**File:** `APIFramework/Core/SimulationBootstrapper.cs`

The Bootstrapper is the **only place in the codebase** where concrete types are wired
together. Everything else uses interfaces or abstract types.

```csharp
public SimulationBootstrapper(IConfigProvider configProvider)
{
    Config        = configProvider.GetConfig();   // load config
    EntityManager = new EntityManager();
    Clock         = new SimulationClock { TimeScale = Config.World.DefaultTimeScale };
    Engine        = new SimulationEngine(EntityManager, Clock);

    RegisterSystems();  // wire all 20 systems in phase order
    SpawnWorld();       // create all entities and world objects
}
```

### The Composition Root Pattern

A composition root is the one place in a program where the dependency graph is assembled.
This is a key architectural pattern:
- **Everything else** receives its dependencies via constructor injection.
- **Nothing** uses `new` to create collaborators inside logic code.
- Swapping an implementation (e.g., `FileConfigProvider` → `InMemoryConfigProvider` for
  tests) requires changing **one line** in the composition root.

```csharp
// For Unity / production:
var sim = new SimulationBootstrapper(new FileConfigProvider("SimConfig.json"));

// For unit tests:
var sim = new SimulationBootstrapper(new InMemoryConfigProvider(new SimConfig()));

// For hot-reload:
sim.ApplyConfig(newConfig);   // pushes new values into live systems, no restart needed
```

### Hot-Reload

`ApplyConfig(SimConfig newCfg)` uses reflection to walk all system config objects and
copy changed primitive values in-place:

```csharp
private static void MergeFlat<T>(T src, T dst, List<string> changes) where T : class
{
    foreach (var prop in typeof(T).GetProperties(...).Where(p => p.PropertyType.IsValueType))
    {
        var old = prop.GetValue(dst);
        var val = prop.GetValue(src);
        if (!Equals(old, val)) { prop.SetValue(dst, val); changes.Add(...); }
    }
}
```

Because systems hold references to their config objects (passed in the constructor), mutating
those objects in-place means the system picks up the new values on its very next tick —
no system reference changes, no restart.

---

## 10. SimulationSnapshot — The Decoupled Data Contract

**File:** `APIFramework/Core/SimulationSnapshot.cs`

The Snapshot is the **only bridge** between the engine and any frontend.

```csharp
// Engine side (called once per frame by any frontend):
SimulationSnapshot snap = sim.Capture();

// Frontend side (read only — no engine coupling):
foreach (var entity in snap.LivingEntities)
    Render(entity.Name, entity.Satiation, entity.Dominant, entity.ColonFill, ...);
```

### Why This Exists

Without the snapshot, frontends reach directly into engine internals:
```csharp
// BAD — tight coupling; frontend must know EntityManager, Query<T>(), component types
var satiation = sim.EntityManager.Query<MetabolismComponent>()
    .First().Get<MetabolismComponent>().Satiation;
```

With the snapshot, frontends are completely decoupled:
```csharp
// GOOD — frontend only knows the snapshot record type
var satiation = snap.LivingEntities[0].Satiation;
```

### Snapshot Contents

```csharp
public sealed class SimulationSnapshot
{
    public ClockSnapshot        Clock          { get; init; }
    public IReadOnlyList<EntitySnapshot>      LivingEntities  { get; init; }
    public IReadOnlyList<TransitItemSnapshot> TransitItems    { get; init; }
    public IReadOnlyList<WorldItemSnapshot>   WorldItems      { get; init; }
    public IReadOnlyList<WorldObjectSnapshot> WorldObjects    { get; init; }
    public int ViolationCount { get; init; }
}
```

All values are copies (`init`-only properties, struct values). The snapshot cannot mutate
the engine. Frontends can safely read it from any thread.

### Key Records

```csharp
public sealed record EntitySnapshot(
    Guid Id, string Name,
    float Satiation, float Hydration, float Energy, float Sleepiness,
    bool IsSleeping, DesireType Dominant,
    float EatUrgency, float DrinkUrgency, float SleepUrgency,
    float SiFill, float LiFill, float ColonFill, float BladderFill,
    bool ColonIsCritical, bool BladderIsCritical,
    float PosX, float PosY, float PosZ,
    bool HasPosition, bool IsMoving, string MoveTarget
);

public sealed record WorldObjectSnapshot(
    Guid Id, string Name, float X, float Y, float Z,
    bool IsFridge, bool IsSink, bool IsToilet, bool IsBed,
    int StockCount   // fridge banana count; -1 for non-container objects
);
```

---

## 11. Multi-Frontend Architecture

Three completely independent frontends share one engine. None knows the others exist.

```
SimulationBootstrapper
    +-- sim.Capture() → SimulationSnapshot
             +-- CLI renderer (Console.Write, ANSI colours)
             +-- Avalonia GUI (MVVM, scrolling charts, real-time panels)
             +-- Unity Visualizer (3D world + HUD overlay)
```

### CLI (`ECSEngine.CLI`)

Runs a console loop: `sim.Engine.Update(1/60f)` → `sim.Capture()` → print formatted
table. Reads `SimConfig.json` from disk; supports `--watch` flag for hot-reload.

### Avalonia GUI (`ECSEngine.Avalonia`)

MVVM pattern: `MainViewModel` drives a `DispatcherTimer` at 60fps. On each tick:
`sim.Capture()` → update `ObservableCollection<T>` → Avalonia data-binding renders
charts and dashboards. The `ScrollingChart` is a custom Avalonia control.

### Unity Visualizer (`UnityVisualizer`)

`SimulationManager` is a MonoBehaviour singleton. In `Awake()` it creates a
`SimulationBootstrapper`. In `Update()`:
1. `_sim.Engine.Update(Time.deltaTime)` — advances game time
2. `_sim.Capture()` → `SimulationManager.Snapshot` (static property)
3. `WorldSceneBuilder.Update()` reads the snapshot, creates/moves 3D cubes
4. `BiologyOverlayUI.OnGUI()` reads the snapshot, draws the right-side HUD

**The `netstandard2.1` requirement:** Unity's Mono editor runtime (used in Unity 6 during
Play mode) is roughly `netstandard2.1`-equivalent. A `net8.0` DLL contains PE metadata
referencing `System.Runtime 8.0.0.0` and emits `NullableContextAttribute`, which Mono
cannot resolve. The fix: target `netstandard2.1` + `LangVersion=latest`. All modern C#
features (records, init, nullable, pattern matching, global usings) work because they are
**compiler features** — the IL they emit is compatible with older runtimes.

---

## 12. The Unity Visualizer Layer

### Scene Setup — Auto-Bootstrap

No manual Inspector work is needed. `SceneBootstrap` fires on Play via
`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]`:

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
private static void Boot()
{
    if (Object.FindObjectOfType<SimulationManager>() != null) return;
    CreateSimulationManager();   // creates and starts the ECS engine
    CreateWorldSceneBuilder();   // reads snapshot → creates 3D GameObjects
    SetupDirectionalLight();
    SetupCamera();               // viewport rect left half: new Rect(0, 0, 0.5f, 1f)
    CreateBiologyOverlay();      // OnGUI HUD on the right half
}
```

### The Rendering Pipeline

```
SimulationManager.Update() → Snapshot
        ↓
WorldSceneBuilder.Update()
        +-- SyncWorldObjects()    — fridge, sink, bed, toilet cubes
        +-- SyncEntities()
                +-- EntityCubeView.UpdateFromSnapshot()
                        +-- Set cube colour (EcsColors.ForEntity)
                        +-- Lerp position toward entity.PosX/Z
                        +-- Update floating TextMesh label
                        +-- OrganCluster.UpdateFromSnapshot()
                                +-- Fill bars per organ (shell + fill cube, Y-scaled)
                                +-- Motion bolus cubes (ping-pong sliding dot)
                                +-- Esophagus transit boluses (per TransitItemSnapshot)
                                +-- Discharge cubes (falling drop on big fill drop)
        ↓
BiologyOverlayUI.OnGUI()
        +-- Background panel (right half)
        +-- Header: name, drive, move destination
        +-- GI tract bars: esophagus → stomach → SI → LI → colon → bladder
        +-- Vitals bars: satiation, hydration, energy, sleepiness
        +-- Status: fridge stock, clock, urgency scores
```

### Key Design Decisions in the Visualizer

**Why `OnGUI` for the overlay instead of Canvas/UGUI?**  
Unity's immediate-mode GUI (`OnGUI`) requires no GameObject setup, no RectTransform
anchoring, and no prefabs. For a programmatically-driven HUD that needs to be created
at runtime with no scene setup, `OnGUI` is far simpler than creating a full Canvas
hierarchy in code. The trade-off is slightly lower performance — acceptable for a
diagnostic overlay.

**Why a separate camera viewport instead of a split Scene/Game view?**  
`cam.rect = new Rect(0f, 0f, 0.5f, 1f)` constrains the 3D camera to the left half of
the screen. The `OnGUI` canvas then fills the right half. This is simpler than two
cameras and avoids UGUI Canvas depth/sorting issues.

**Why organ strips attached to entity cubes?**  
The `OrganCluster` is parented to `EntityCubeView` and positioned in local space
(+X offset from the entity). This means the organ strip automatically follows Billy as
he moves — no separate update needed.

---

## 13. This Engine vs Unity DOTS

Unity's **Data-Oriented Technology Stack (DOTS)** is Unity's production ECS implementation.
Understanding the parallels and differences is valuable for any architect.

### The Shared DNA

Both this engine and DOTS are built on the same foundational insight: **separate data from
logic, group entities by component composition, run systems as batches**.

| Concept | This Engine | Unity DOTS |
|---------|-------------|------------|
| Entity | `Guid` + `Dictionary<Type, object>` | 32-bit integer (index + version) |
| Component | `struct` | `struct` implementing `IComponentData` |
| System | `class : ISystem` | `class : SystemBase` or `ISystem` |
| Query | `EntityManager.Query<T>()` | `EntityQuery` (built-in) |
| World | `EntityManager` singleton | `World` (can have multiple) |
| Update loop | `SimulationEngine.Update(dt)` | `World.Update()` via `PlayerLoop` |

### Key Differences

**Memory layout.** DOTS stores components in **Archetypes** — contiguous blocks of memory
(chunks) grouped by exact component combination. All entities with {Position, Velocity}
share one chunk; adding Health creates a new chunk. This enables SIMD vectorization and
cache-line-perfect iteration. This engine stores components in per-entity dictionaries —
flexible and simple, but each component access is a dictionary lookup.

**Parallelism.** DOTS systems declare read/write dependency graphs via `[ReadOnly]` and
`RequireForUpdate<T>()` attributes. The scheduler can then run independent systems on
separate job threads. This engine runs all systems serially on the main thread.

**Scale.** DOTS is designed for 10,000–100,000+ entities. This engine is designed for
1–100 deeply-simulated biological entities. The design priorities are opposite.

**Authoring.** DOTS has Baker/Authoring components, SubScenes, and a Blob asset system.
This engine has `SimulationBootstrapper` and JSON config. DOTS is complete; this engine
is intentionally minimal.

### What DOTS Teaches About This Engine's Limitations

The dictionary-per-entity approach has one significant cost: **cache misses**. When
`MetabolismSystem` iterates 1,000 entities and calls `entity.Get<MetabolismComponent>()`,
each call hops to a different memory location. DOTS avoids this by packing all
MetabolismComponents contiguously in a chunk — the CPU prefetcher can predict and load
them before they are needed.

For a simulation with 2–5 entities this is irrelevant. At 10,000 it would matter.

### What This Engine Does Better

**Simplicity.** DOTS has a steep learning curve: Burst compiler, safety checks, job
dependencies, entity conversion workflows. This engine is ~2,000 lines of vanilla C#.
Any intermediate developer can read and modify any system in isolation.

**Flexibility.** Adding a new component in DOTS requires an authoring component, a Baker,
and an Archetype change. Here: `entity.Add(new MoodComponent { Joy = 0 })` — done.

**Testability.** Every system in this engine is tested with `new SystemName(config).Update(em, dt)`.
In DOTS, testing requires a full `World` setup.

**Biological fidelity over performance.** A `NutrientProfile` struct with 12 fields (water,
protein, fat, carbs, vitamins, minerals...) would be painful to lay out efficiently in DOTS
chunks. Here it is just a `struct` field — straightforward.

### The Learning Path from Here to DOTS

Understanding this engine deeply prepares you for DOTS:
1. You understand why entity identity is separated from data (OK)
2. You understand why systems are stateless functions over queries (OK)
3. You understand phase ordering and data dependency graphs (OK)
4. You understand the performance motivation for contiguous memory layouts (OK)
5. DOTS adds: Burst → vectorized jobs, chunks → cache efficiency, multiple worlds → isolation

---

## 14. What to Study Next

To move from "senior developer" to "senior framework architect", focus on these areas:

### Immediate (continue with this project)
- **Write the missing unit tests** (Task #31 — the 3 intestine systems). Tests are where
  architecture reveals itself — if a system is hard to test in isolation, the seams are wrong.
- **Add DrinkingSystem proximity** (make water also require going to the sink). You already
  have the pattern from FeedingSystem.
- **Add the `MovementTargetComponent` for sleep and elimination** — Billy should walk to
  the bed to sleep, to the toilet to eliminate.

### Design Patterns to Internalize
- **Composition Root / DI** — `SimulationBootstrapper` is the clearest example here.
  Read "Dependency Injection in .NET" by Mark Seemann.
- **Command/Query Responsibility Segregation (CQRS)** — The Snapshot is a read model;
  the engine is the write model. This is CQRS at the micro level.
- **Event Sourcing** — The tick-by-tick simulation is essentially event sourcing; every
  `deltaTime` update is an event applied to state.

### Architecture Books
- **"A Philosophy of Software Design"** — John Ousterhout. Deep vs. shallow modules. The
  EntityManager's O(1) query index is a "deep module" — simple interface, complex
  implementation. This book teaches you to seek that balance.
- **"Clean Architecture"** — Robert Martin. The dependency rule (outer layers depend on
  inner layers, never the reverse) is exactly what this engine does: frontends depend on
  the engine API; the engine never depends on frontends.
- **"Designing Data-Intensive Applications"** — Martin Kleppmann. When your simulations
  need persistence, replay, or multi-node distribution, this is the map.

### Unity-Specific Next Steps
- **Unity DOTS Entities package** — Build the same Billy simulation in DOTS. The contrast
  will crystallize every architectural lesson here.
- **Unity Netcode for GameObjects / DOTS** — How do you synchronize an ECS simulation
  across a network? The snapshot pattern you already understand is the foundation.

---

*This guide reflects the engine as of v0.8+ (spatial layer, Unity visualizer, starvation scenario). For the original architectural reasoning behind the ECS pattern choice, the O(1) query optimization, and the phase model, see `ENGINEERING-GUIDE.md`.*
