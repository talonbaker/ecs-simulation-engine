# Changelog

All notable changes to this project are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

### Planned
- `AutonomySystem` — Billy's mood-gated disobedience mechanic
- World food/water sources as spawnable entities (fridge, sink, bowl)
- Proximity / spatial grid system — so RotTag on nearby food raises Disgust before consumption
- **v0.7.4 — Bladder and kidneys.** `KidneyComponent`, `BladderComponent`, `KidneySystem`,
  `EliminationUrgeSystem`. Tracks fluid balance; spawns a `Pee` desire that can override
  other drives when fullness is high.
- Spatial / movement layer — Billy in an apartment with fridge, sink, toilet, bed.
- Lua scripting layer for moddable system/mechanic definitions (post-stabilization)

---

## [0.7.3] — 2026-04-17

### Added

- **Full GI elimination pipeline** — three new components, four new systems, and the
  `Defecate` desire type complete the digestive tract from stomach to elimination.

- **`SmallIntestineComponent`** (`APIFramework/Components/SmallIntestineComponent.cs`) —
  holds chyme (semi-digested stomach output) in transit. Fields: `ChymeVolumeMl`,
  `AbsorptionRate`, `Chyme` (NutrientProfile for display), `ResidueToLargeFraction`.
  Capacity: 250 ml. `Fill` and `IsEmpty` computed properties.

- **`LargeIntestineComponent`** (`APIFramework/Components/LargeIntestineComponent.cs`) —
  receives indigestible residue from the SI. Performs water reabsorption and stool
  compaction. Fields: `ContentVolumeMl`, `WaterReabsorptionRate`, `MobilityRate`,
  `StoolFraction`. Capacity: 300 ml.

- **`ColonComponent`** (`APIFramework/Components/ColonComponent.cs`) — terminal stool
  holding vessel. Fields: `StoolVolumeMl`, `UrgeThresholdMl`, `CapacityMl`. Computed:
  `Fill` (0–1 relative to capacity), `HasUrge`, `IsCritical`, `IsEmpty`.

- **`SmallIntestineSystem`** (`APIFramework/Systems/SmallIntestineSystem.cs`) — drains
  `ChymeVolumeMl` at `AbsorptionRate` per game-second. Passes `ResidueToLargeFraction`
  of each processed batch to `LargeIntestineComponent`. No re-absorption of nutrients
  (DigestionSystem already handled that); the chyme NutrientProfile is decremented
  proportionally for display purposes only. Phase: Elimination (55).

- **`LargeIntestineSystem`** (`APIFramework/Systems/LargeIntestineSystem.cs`) — two
  concurrent processes each tick: (1) water reabsorption — adds `WaterReabsorptionRate × dt`
  to `MetabolismComponent.Hydration` while content is present (secondary hydration source,
  slower than drinking); (2) stool formation — advances content at `MobilityRate`, deposits
  `StoolFraction` of processed volume into `ColonComponent`. Phase: Elimination (55).

- **`ColonSystem`** (`APIFramework/Systems/ColonSystem.cs`) — pure tag manager. Each tick:
  applies/removes `DefecationUrgeTag` when `StoolVolumeMl >= UrgeThresholdMl`; applies/
  removes `BowelCriticalTag` when `StoolVolumeMl >= CapacityMl`. Owns all writes to these
  two tags — no other system touches them. Phase: Elimination (55).

- **`DefecationSystem`** (`APIFramework/Systems/DefecationSystem.cs`) — action system
  (Behavior/40). When `DriveComponent.Dominant == Defecate`, sets `ColonComponent.StoolVolumeMl`
  to 0, modelling a complete bowel movement. Backwards-compatible: entities without
  `ColonComponent` are silently skipped.

- **`Elimination` system phase** (`SystemPhase.Elimination = 55`) — inserted between
  Transit (50) and World (60). SmallIntestineSystem, LargeIntestineSystem, and ColonSystem
  all run here, one tick after DigestionSystem deposits chyme.

- **`DefecationUrgeTag`** and **`BowelCriticalTag`** — two new biological urge tags.
  Both appear in `EntityViewModel.ActiveTags` (DEFECATION URGE, BOWEL CRITICAL) and
  are checked by `BrainSystem` to score the Defecate drive.

- **`DesireType.Defecate`** — added to the `DesireType` enum. `DriveComponent` gained
  `DefecateUrgency` (0–1). `Dominant` computed property updated to include it at the
  lowest priority (Eat > Drink > Sleep > Defecate in tie-breaking). `BowelCriticalTag`
  overrides DefecateUrgency to 1.0, making defecation the absolute dominant drive.

- **`BrainSystemConfig.DefecateMaxScore`** (0.85) — ceiling for DefecateUrgency at
  full colon fill. Keeps defecation below sleep urgency in normal circumstances, but above
  it when `BowelCriticalTag` forces a 1.0 override.

- **`DigestionSystemConfig.ResidueFraction`** (0.2) — fraction of digested stomach volume
  deposited as chyme into SmallIntestineComponent. The remaining 80% is considered directly
  absorbed by the stomach lining (pre-existing behavior). Backwards-compatible guard:
  `if (!entity.Has<SmallIntestineComponent>()) continue`.

- **GI pipeline config in `SimConfig.json`** — `smallIntestine`, `largeIntestine`, and
  `colon` sections added for both `human` and `cat` entity types. `defecateMaxScore`
  added to the `brain` section; `residueFraction` added to `digestion`. At TimeScale 120,
  the human pipeline produces a defecation urge roughly once per game-day (matching
  biological frequency of 1–2 bowel movements daily).

- **`EntityTemplates` updated** — `SpawnHuman` and `SpawnCat` now add
  `SmallIntestineComponent`, `LargeIntestineComponent`, and `ColonComponent` from
  entity config on spawn.

- **`SimulationBootstrapper` updated** — `RegisterSystems` adds DefecationSystem
  (Behavior), SmallIntestineSystem, LargeIntestineSystem, ColonSystem (Elimination).
  `ApplyConfig` merges SI, LI, Colon configs via `MergeFlat`. System pipeline grows from
  13 → 17 registered systems.

- **`InvariantSystem` updated** — three new `Check*` methods guard the new components:
  `CheckSmallIntestine` (ChymeVolumeMl ∈ [0, MaxVolumeMl], Chyme fields ≥ 0),
  `CheckLargeIntestine` (ContentVolumeMl ∈ [0, MaxVolumeMl]),
  `CheckColon` (StoolVolumeMl ∈ [0, CapacityMl]).
  `CheckDrives` extended with `DefecateUrgency ∈ [0, 1]`.

- **`SimulationSnapshot.EntitySnapshot` updated** — five new fields added:
  `DefecateUrgency`, `SiFill`, `LiFill`, `ColonFill`, `ColonHasUrge`, `ColonIsCritical`.
  `Capture()` populates them from the GI components when present (defaults to 0/false for
  entities without the pipeline).

- **Avalonia GI PIPELINE panel** — added to the entity card in `MainWindow.axaml`.
  Three horizontal fill bars (SI / LI / COLON) with percentage labels, driven by
  `EntityViewModel.SiFill`, `LiFill`, `ColonFill`. A status text line shows "Urge" in
  amber and "CRITICAL" in red when ColonComponent threshold is exceeded.

- **`EntityViewModel` extended** — 10 new observable properties for the GI pipeline
  (`HasGiPipeline`, `SiFill/LiFill/ColonFill`, `*FillLabel`, `ColonIsOk/IsUrge/IsCritical`).
  `DriveScores` label now includes `poop {DefecateUrgency:F2}`.

- **`IntestineSystemTests.cs`** (`APIFramework.Tests/Systems/`) — 26 unit tests covering
  SmallIntestineSystem (volume drain, chyme proportional drain, residue handoff, LI caps,
  missing-LI robustness), LargeIntestineSystem (water reabsorption, hydration cap, empty
  skip, no-Metabolism robustness, content mobility, stool formation, colon capping,
  missing-Colon robustness), ColonSystem (tag lifecycle: below urge → no tags; at/above
  urge → UrgeTag; at capacity → both tags; drops below → tags removed), and a full
  pipeline integration test (SI → LI → Colon in one pass).

- **GI tract HTML prototype** (`gi-tract-prototype.html`) — standalone canvas visualization
  of the GI pipeline with particle animation. Particle types cycle bolus (bright) → chyme
  (tan) → residue (brown) → stool (dark) through 6 anatomical segments. Controls: EAT,
  DRINK, POOP, AUTO. Annotation panels mirror the ECS component data for design review.

### Changed

- **System pipeline** expanded from 13 → 17 registered systems. New phase `Elimination(55)`
  inserted between Transit(50) and World(60).
- **`CliRenderer` / `PrintSnapshot`** — `DriveScores` line now includes defecate urgency
  and the colon fill level. Active tags section shows DEFECATION URGE and BOWEL CRITICAL.
- **`ScrollingChart`** — chart height reduced 140 → 85 (biological charts), 100 → 70
  (performance charts). Label font bumped to 13pt; value font to 11pt; label colors
  brightened (#444 → #606060, #777 → #909090).
- **Version** bumped 0.7.2 → 0.7.3.

---

## [0.7.2] — 2026-04-17

### Added

- **`ISpatialIndex` interface** (`APIFramework/Core/ISpatialIndex.cs`) — contract
  for spatial lookup structures used by v0.9+ world systems. Defines `Register`,
  `Unregister`, `Update`, `QueryRadius`, and `QueryNearest`. No production
  implementation yet; stub exists so that v0.8 world components can declare their
  spatial dependency from the start. Systems that receive `ISpatialIndex` via
  constructor injection are automatically decoupled from the concrete data
  structure choice (SimpleGrid, Quadtree, BVH, or NullIndex for tests).

- **`SystemPhase` enum** (`APIFramework/Core/SystemPhase.cs`) — explicit phase
  model for system execution order. Eight phases with numeric values that define
  their run order: `PreUpdate(0)`, `Physiology(10)`, `Condition(20)`,
  `Cognition(30)`, `Behavior(40)`, `Transit(50)`, `World(60)`, `PostUpdate(100)`.
  Phase boundaries document data dependencies and are the prerequisite for future
  parallel execution.

- **`SystemRegistration` record** (`APIFramework/Core/SystemPhase.cs`) — wraps
  an `ISystem` with its declared phase. Used by `SimulationEngine` to group and
  sort systems without requiring them to know about phases themselves.

- **`docs/ARCHITECTURE.md`** — decision records covering component storage
  (Dictionary boxing and why ComponentStore<T> is deferred), the O(E)→O(1)
  query index change, the system phase model, ISpatialIndex contract-first
  design, honest capacity assessment ("thousands of entities"), and what was
  explicitly not done and why.

### Changed

- **`Entity` onChange callback** (`APIFramework/Core/Entity.cs`) — constructors
  now accept an optional `Action<Entity, Type, bool>? onChange` parameter. The
  callback fires when a component type is first added (`added=true`) or removed
  (`added=false`). Overwriting an existing component via `Add<T>()` does not
  fire the callback. This change is backward-compatible — the zero-argument
  constructor still works.

- **`EntityManager` component index** (`APIFramework/Core/EntityManager.cs`) —
  now maintains `Dictionary<Type, HashSet<Entity>> _componentIndex` updated via
  the Entity onChange callback. `Query<T>()` returns the pre-built bucket in
  O(1) instead of scanning all entities in O(E). `DestroyEntity` cleans the
  index in O(buckets) by removing the entity from every bucket it occupies.
  `CreateEntity(Guid existingId)` overload added for deserialization.

- **`SimulationEngine` phase-aware execution** (`APIFramework/Core/SimulationEngine.cs`) —
  `AddSystem(ISystem)` now has an explicit-phase overload `AddSystem(ISystem, SystemPhase)`.
  The engine groups all `SystemRegistration` records by phase and runs them in
  ascending numeric order. The sorted execution list is built lazily and cached
  until the next `AddSystem` call. Systems within a phase remain sequential.
  The old `AddSystem(ISystem)` signature is preserved for backward compatibility
  (defaults to `Physiology` phase).

- **`SimulationBootstrapper` phase assignments** (`APIFramework/Core/SimulationBootstrapper.cs`) —
  all 13 registered systems now use the explicit `AddSystem(system, phase)` overload.
  Phase assignments: `InvariantSystem→PreUpdate`, `MetabolismSystem+EnergySystem→Physiology`,
  `BiologicalConditionSystem→Condition`, `MoodSystem+BrainSystem→Cognition`,
  `FeedingSystem+DrinkingSystem+SleepSystem→Behavior`,
  `InteractionSystem+EsophagusSystem+DigestionSystem→Transit`, `RotSystem→World`.

### Fixed (post-release patch, same version tag)

- **`NutrientProfile` JSON deserialization** — `System.Text.Json` ignores public
  fields by default and only reads public properties. `NutrientProfile` is a
  struct of 16 public fields, so every `Nutrients` sub-object in `SimConfig.json`
  silently deserialised to all-zeros, overriding the C# class initializer defaults.
  This caused `DrinkingSystemConfig.Water.Nutrients.Water = 0` (instead of 15 ml),
  making the drinking cap check `NutrientsQueued.Water >= 15` unreachable, which
  triggered infinite water-entity spawning and filled the stomach to 1000ml with
  zero nutrients — the entity starved and dehydrated while the stomach stayed full.
  Fix: added `IncludeFields = true` to `JsonSerializerOptions` in `SimConfig.Load`.

- **Performance report in CLI** — Added a PERFORMANCE section to `CliRenderer.PrintReport`
  showing: entity counts, average tick µs, ticks-per-second, system phase layout,
  component index bucket count, and a concrete estimate of entity-checks eliminated
  by the O(1) Query index (previously O(E) scan per system per tick).

### Architecture notes

This release is a pure architectural refactor. Simulation behavior is unchanged —
no tuning values, component data, or system logic was modified. The observable
difference: `Query<T>()` is now O(1); at 100+ entities the engine will show
measurably lower CPU time in profiler traces.

---

## [0.7.0] — 2026-04-16

### Added
- **`NutrientProfile` struct** — a universal carrier for real-biology nutrients
  attached to every piece of food, liquid, bolus, and body store in the simulation.
  Contains 16 fields: 4 macros (Carbohydrates, Proteins, Fats, Fiber in grams),
  water (ml), 6 vitamins (A, B, C, D, E, K in mg), and 5 minerals
  (Sodium, Potassium, Calcium, Iron, Magnesium in mg). Calories derived via
  Atwater factors (4/4/9 kcal per gram). Operator overloads for `+`, `-`,
  and scalar `*` give ergonomic nutrient arithmetic throughout the pipeline.

- **`MetabolismComponent.NutrientStores`** — the body's ongoing real-biology layer.
  DigestionSystem pools absorbed macros, water, vitamins, and minerals here
  across the run. Future organ-systems (small/large intestine, kidneys) will
  drain from this pool. Gameplay-facing `Satiation` / `Hydration` (0–100)
  remain; they are now *derived* from the calorie/water release, not set directly.

- **`DigestionSystemConfig`** — two conversion factors:
  `SatiationPerCalorie` (0.3) and `HydrationPerMl` (2.0). These map biological
  absorption back onto the existing 0–100 metrics. Tuned so the pre-v0.7 feel
  is preserved: a medium banana (117 kcal × 0.3) ≈ 35 satiation, a gulp of
  water (15 ml × 2.0) = 30 hydration.

- **Real banana nutrition** — the hard-coded banana bolus now carries the real
  macros for a medium banana: 27 g carbs, 1.3 g protein, 0.4 g fat, 3.1 g
  fiber, 89 ml water, plus realistic B-complex, C, potassium, and magnesium.
  Water carries 15 ml and nothing else by default; milk/juice/coffee will
  layer macros/vitamins onto Water in future drink configs.

- **Stomach nutrient queue** — `StomachComponent.NutrientsQueued` (a full
  `NutrientProfile`) replaces the two scalars `NutritionQueued` +
  `HydrationQueued`. EsophagusSystem adds each arriving bolus/liquid's full
  profile; DigestionSystem releases a ratio-proportional slice each tick.

- **CLI NUTRIENTS section** — `PrintSnapshot()` now shows stored calories,
  macros in grams, water in ml, plus vitamins and minerals in mg when present.
  Also shows queued-stomach macros for each tick of absorption.

- **Avalonia NUTRIENTS panel** — green accumulator panel beneath the Stomach
  panel showing total calories (large), macros line, water line, vitamins
  and minerals lines. Panel hides automatically when stores are empty.

### Changed
- **`BolusComponent.NutritionValue` → `BolusComponent.Nutrients`** (now a
  `NutrientProfile`). Every consumer updated (EsophagusSystem, FeedingSystem,
  InteractionSystem).
- **`LiquidComponent.HydrationValue` → `LiquidComponent.Nutrients`** (also a
  `NutrientProfile` — water plus any dissolved macros/minerals).
- **`FoodObjectComponent.NutritionPerBite` → `NutrientsPerBite`** (full breakdown
  per bite; future food items can have wildly different profiles).
- **`FoodItemConfig.NutritionValue` → `Nutrients`** (NutrientProfile) and
  **`DrinkItemConfig.HydrationValue` → `Nutrients`** in `SimConfig.cs`.
- **`FeedingSystemConfig.NutritionQueueCap`** now measured in kcal (was flat
  "nutrition points"). Default bumped `70 → 240` so two bananas still fit under
  the cap at the new 0.3 sat/kcal factor.
- **`DrinkingSystemConfig.HydrationQueueCap` / `HydrationQueueCapDehydrated`**
  now measured in ml of water queued (was flat hydration points).
  Defaults `30 → 15` and `60 → 30` so one/two gulps in flight still match
  the old feel at the new 2.0 hydr/ml factor.
- **DigestionSystem** now takes `DigestionSystemConfig`, releases a fraction
  of the queued `NutrientProfile` each tick, pools it into `NutrientStores`,
  and derives Satiation/Hydration increments from released Calories/Water.
- **System pipeline** still 13 systems; DigestionSystem (position 12) now
  accepts its config via the bootstrapper and `ApplyConfig` merges it.
- **SimConfig.json** gained a `systems.digestion` section and restructured
  `banana.nutrients` and `water.nutrients` as embedded profile objects.
- **Version** bumped 0.6.0 → 0.7.0.

### Notes
Pre-v0.7 tuning is preserved by design — the digestion factors are calibrated
so Billy still gets ~35 satiation from a banana and 30 hydration from a gulp
of water. Everything that changed is additive biology plumbing; gameplay feel
should be identical, and the `NutrientStores` field gives future systems the
data they need to simulate real metabolic load without touching behaviour.

---

## [0.6.0] — 2026-04-16

### Added
- **MoodSystem fully wired** (pipeline position 5) — all 8 Plutchik emotions now have
  real biological inputs that update each tick:
  - `Joy` ← satiation, hydration, and energy all above `JoyComfortThreshold` (60%)
  - `Anger` ← `IrritableTag` present (hunger or thirst > 60%)
  - `Sadness` ← `HungerTag` or `ThirstTag` present this tick
  - `Disgust` (boredom at low intensity) ← `Dominant == None` last tick (idle state)
  - `Disgust` spike ← `ConsumedRottenFoodTag` after eating rotten food (instant +40)
  - `Anticipation` ← hunger or thirst building between 15% and 50% (drive rising but not dominant)
  - `Trust`, `Fear`, `Surprise` — stubs with decay wired; inputs pending spatial/threat systems
  - All emotions have configurable decay rates and gain rates in `SimConfig.json`

- **Minimum urgency floor in BrainSystem** — if all drive scores remain below
  `MinUrgencyThreshold` (0.05) after mood modifiers, they are zeroed and `Dominant` returns
  `None`. This is the true idle/mulling state. It's what causes boredom to accumulate naturally.

- **Mood output effects on other systems:**
  - `BoredTag` → `BrainSystem` adds `BoredUrgencyBonus` (0.04) to all drives, breaking out of idle
  - `SadTag`   → `BrainSystem` multiplies all drive scores by `SadnessUrgencyMult` (0.80 — mild suppression)
  - `GriefTag` → `BrainSystem` multiplies all drive scores by `GriefUrgencyMult` (0.50 — strong suppression)
  - `AngryTag` / `RagingTag` → `MetabolismSystem` raises drain rates by 25% (cortisol stress response)

- **RotSystem** (pipeline position 13) — ages all `RotComponent` food entities each tick.
  Once `AgeSeconds >= RotStartAge`, `RotLevel` climbs at `RotRate` per game-second.
  When `RotLevel >= RotTagThreshold` (30%), `RotTag` is applied to the food entity.

- **RotComponent** — tracks `AgeSeconds`, `RotLevel`, `RotStartAge`, `RotRate` per food entity.
  Computed properties: `IsDecaying`, `Freshness`. Configurable per entity at spawn time.

- **FeedingSystem updated** — checks for world food entities before conjuring food:
  1. If world food exists with `RotTag` → eats it, adds `ConsumedRottenFoodTag` to eater
  2. If fresh world food exists → eats it normally
  3. No world food → conjures a fresh banana bolus with a `RotComponent` attached
     (the banana can decay if it ever sits in the world uneaten)

- **ConsumedRottenFoodTag** — one-tick signal tag. FeedingSystem writes it; MoodSystem reads it,
  spikes `Disgust` + `Surprise`, and removes it. Zero coupling between the two systems.

- **Emotion tags visible everywhere** — CliRenderer's MOOD section shows all 8 emotions as
  ASCII progress bars plus valence score. Avalonia GUI has a full EMOTIONS panel with colored
  progress bars (positive emotions warm/yellow, negative emotions cool/blue). Both display
  active emotion tag names (JOYFUL, bored, ANGRY, etc.) updated every tick.

- **Avalonia MOOD panel** — 8 emotion bars with distinct colors per emotion family:
  Joy (#FFD60A), Trust (#30D158), Anticipation (#FF9F0A), Anger (#FF453A),
  Sadness (#0A84FF), Disgust (#6E40C9), Fear (#5AC8FA), Surprise (#FF6B35).
  Valence score (sum of positives minus negatives) shown in the panel header.

### Changed
- **System pipeline** expanded from 12 to 13 systems. RotSystem added at position 13.
- **`MoodSystemConfig`** gained 9 new fields: `JoyGainRate`, `JoyComfortThreshold`,
  `AngerGainRate`, `SadnessGainRate`, `BoredGainRate`, `RottenFoodDisgustSpike`,
  `AnticipationGainRate`, `AnticipationHungerMin`, `AnticipationHungerMax`.
- **`BrainSystemConfig`** gained 4 new fields: `BoredUrgencyBonus`, `MinUrgencyThreshold`,
  `SadnessUrgencyMult`, `GriefUrgencyMult`.
- **`FeedingSystemConfig`** gained 2 new fields: `FoodFreshnessSeconds`, `FoodRotRate`.
- **`SimConfig.json`** updated with all new sections: `mood` gain rates, `rot` config,
  brain urgency threshold and mood modifier multipliers.
- **Version** bumped 0.5.0 → 0.6.0.

---

## [0.5.0] — 2026-04-16

### Added
- **Hot-reload** — `SimConfigWatcher` watches `SimConfig.json` for saves and calls
  `SimulationBootstrapper.ApplyConfig()` automatically. Works in both the CLI and the
  Avalonia GUI. No restart needed to tune drain rates, thresholds, or brain weights.
  Changed values are printed to the console so you always know what just changed.

- **InvariantSystem** (pipeline position 1) — runs first every tick and checks all
  known component values against their documented valid ranges:
  - `MetabolismComponent`: Satiation, Hydration ∈ [0, 100]
  - `EnergyComponent`: Energy, Sleepiness ∈ [0, 100]
  - `StomachComponent`: CurrentVolumeMl ∈ [0, MaxVolumeMl], Queued values ≥ 0
  - `DriveComponent`: all urgency scores ∈ [0, 1]
  - `EsophagusTransitComponent`: Progress ∈ [0, 1]
  Violations are clamped (simulation continues) and recorded in `Invariants.Violations`
  for end-of-run reporting. Live violations print inline during a CLI run.

- **SimMetrics** — samples all living entities every 20 ticks; tracks resource min/
  mean/max, lifecycle event timestamps (first hunger, first sleep, etc.), feed/drink/
  sleep cycle counts.

- **End-of-run balancing report** (`CliRenderer.PrintReport`) — printed automatically
  after every CLI run unless `--no-report`. Shows:
  - Invariant summary (violation count, worst offenders, entity names)
  - Per-entity lifecycle timeline
  - Resource ranges (min → mean → max) for every resource
  - Automatic balancing hints (machine-gun feeding, resource stuck at 0, no sleep, etc.)

- **`SimulationBootstrapper.ApplyConfig(SimConfig)`** — merges new config values onto
  existing config objects in-place using reflection over value-type properties. Systems
  hold references to those objects so they see new values on the very next tick, with
  zero restart overhead.

### Changed
- **CLI default snapshot interval** changed from 10 game-seconds to 600 (every 10
  game-minutes) to match the new 120× time scale.
- **CliOptions** gained `--report` / `--no-report`, `--no-violations` flags.
- **System pipeline** expanded from 10 to 11: InvariantSystem inserted at position 1.

---

## [0.4.0] — 2026-04-15

### Added
- **EnergyComponent** — two new physiological resources per entity: `Energy` (0–100,
  starts 85 — well rested) and `Sleepiness` (0–100, starts 15 — just woke up).
  `IsSleeping` flag is the contract between `SleepSystem` and `EnergySystem`.
- **EnergySystem** — drains `Energy` and accumulates `Sleepiness` while awake;
  restores both during sleep. Manages `TiredTag`, `ExhaustedTag`, and `SleepingTag`.
  Pipeline position 2 of 10.
- **SleepSystem** — toggles `IsSleeping` when `BrainSystem` picks SLEEP as dominant.
  Enforces a `wakeThreshold` (Sleepiness ≤ 20) so entities don't snap awake mid-sleep.
  Pipeline position 7 of 10.
- **Day/Night cycle** — `SimulationClock` now tracks game time starting at 6:00 AM.
  `GameTimeDisplay` ("6:05 AM"), `DayNumber`, `IsDaytime`, and `CircadianFactor` are
  all computed properties on the clock. No extra system needed — it's pure math.
- **Circadian factor** — piecewise multiplier applied to `SleepUrgency` in `BrainSystem`.
  Morning (0.40) suppresses sleep drive after waking; night (1.20–1.40) amplifies it.
  Produces a natural ~16-hour awake / ~8-hour sleep rhythm without scripted events.
- **WorldConfig** in `SimConfig.json` — `defaultTimeScale` (120) controls the default
  game speed. 1 real second = 120 game seconds = 2 game minutes.
- `TiredTag` — new condition tag for energy between ExhaustedThreshold and TiredThreshold.

### Changed
- **Default TimeScale** changed from 1 to 120 (2 game minutes per real second).
  The game-speed slider in the Avalonia GUI now ranges 0–480×.
- **All drain rates re-tuned** for the new game-time units. Human hunger in ~4 game
  hours, thirst in ~2.5 game hours at default speed.
- **BrainSystem** now takes `SimulationClock` as a constructor argument to read
  `CircadianFactor` each tick. `SleepUrgency` is fully live — no more placeholder 0.
- **Clock display** changed from sim-seconds (`hh:mm:ss`) to 12-hour game time
  (`6:05 AM`) in both frontends. Day number shown alongside.
- **CliRenderer** header now shows `DayTimeDisplay` and the circadian factor per entity.
- **SimulationBootstrapper** system pipeline expanded from 8 to 10 systems.

---

## [0.3.0] — 2026-04-15

### Added
- **BrainSystem** — proper priority queue replacing the previous heuristic hacks.
  Scores `EatUrgency`, `DrinkUrgency`, `SleepUrgency` per tick (0.0–1.0). The
  dominant drive is stamped on `DriveComponent`. Action systems check this before acting.
- **DriveComponent** — new component holding urgency scores and the computed
  `Dominant` property (`DriveType` enum: None, Eat, Drink, Sleep).
- **DrinkingSystem** — water-spawning logic extracted from `BiologicalConditionSystem`
  into its own file. Follows the same pattern as `FeedingSystem`.
- **SimConfig.json** — all tuning values externalised from code to a JSON file at
  the repository root. Covers entity spawn values, system thresholds, food/drink
  properties, and brain drive score ceilings.
- **SimConfig.cs** — typed config loader in `APIFramework/Config/`. Deserialises
  `SimConfig.json` using `System.Text.Json`. Falls back to compiled defaults
  if the file is not found so the simulation always runs.
- **ECSSimulation.sln** — solution file tying all three projects together.
- **ECSCli** — headless console runner. Drives the simulation at max speed with
  configurable timescale, duration, tick count, and snapshot interval.
  No UI dependency whatsoever.
  - `Program.cs` — main loop with Ctrl+C handling and final summary
  - `CliOptions.cs` — arg parser (`--timescale`, `--duration`, `--ticks`, `--snapshot`, `--quiet`)
  - `CliRenderer.cs` — ASCII state snapshots showing resources, tags, stomach, esophagus

### Changed
- **BiologicalConditionSystem** — is now purely observational. Water-spawning logic
  moved to `DrinkingSystem`. Accepts `BiologicalConditionSystemConfig` via constructor.
- **FeedingSystem** — checks `DriveComponent.Dominant == Eat` before acting.
  Accepts `FeedingSystemConfig` via constructor. All magic numbers removed.
- **InteractionSystem** — accepts `InteractionSystemConfig` via constructor.
  Bite volume and esophagus speed are now config-driven.
- **EntityTemplates** — `SpawnHuman` and `SpawnCat` accept an optional `EntityConfig`
  parameter. All starting values come from config; nothing is hardcoded.
- **SimulationBootstrapper** — loads `SimConfig`, passes typed config objects to each
  system via constructor injection. System registration extracted to `RegisterSystems()`.
  World spawning extracted to `SpawnWorld()`.
- **README.md** — rewritten as a full technical wiki covering architecture, system
  pipeline table, per-system reference, component reference, tag reference,
  full `SimConfig.json` documentation, and a planned-systems roadmap.

### Removed
- All hardcoded biological thresholds from `BiologicalConditionSystem`
- Hardcoded hunger/thirst priority margin heuristic (replaced by `BrainSystem`)
- Hardcoded `DehydratedTag` check in `FeedingSystem` (replaced by `BrainSystem`)
- `DesireSystem` removed from the system pipeline (superseded by `BrainSystem`;
  file retained but unregistered)

---

## [0.2.0] — 2026-04-14

### Added
- **StomachComponent** — physical stomach state: volume, digestion rate, queued
  nutrition and hydration. Proportional digestion model: nutrients released at the
  same rate as volume drains.
- **DigestionSystem** — final stage of the digestive pipeline. Drains stomach over
  time, releases queued nutrients proportionally into `MetabolismComponent`.
- **LiquidComponent** — liquid substance with `VolumeMl`, `HydrationValue`,
  `LiquidType`. Completes the food/water symmetry in the pipeline.
- **SimulationBootstrapper** — composition root. Any frontend creates one instance
  and drives `Engine.Update(deltaTime)`. APIFramework never knows a UI exists.
- **Avalonia MVVM frontend** — `MainViewModel` with `DispatcherTimer` loop,
  `EntityViewModel` cache (`Dictionary<Guid, EntityViewModel>`), two observable
  collections (`LivingEntities`, `PipelineEntities`).
- **MainWindow.axaml** — dark terminal aesthetic. SATIATION bar (green),
  HYDRATION bar (blue) as primary resources. Hunger/Thirst shown as derived
  sensation labels beneath. STOMACH bar (orange). ESOPHAGUS PIPELINE (purple).
- Time-scale slider (0–10x) in GUI header.
- `Microsoft.Extensions.DependencyInjection` DI container wiring in `App.axaml.cs`.

### Changed
- **MetabolismComponent** redesigned — `Satiation` and `Hydration` are now the
  canonical resources (0–100, deplete over time). `Hunger` and `Thirst` are
  readonly computed properties (`100 - resource`). Invalid states are structurally
  impossible.
- **MetabolismSystem** — now drains `Satiation`/`Hydration` rather than
  accumulating drive values.
- **EsophagusSystem** — delivers to `StomachComponent` instead of directly
  modifying `MetabolismComponent`.
- **BiologicalConditionSystem** — thresholds updated for new 0–100 resource scale.
  Machine-gun water hack added (hydration queue cap, later replaced in 0.3.0).

### Fixed
- `EsophagausSystem.cs` filename typo → `EsophagusSystem.cs` (git mv)
- `FeedingSystem` missing `TargetEntityId` — boluses were arriving at `Guid.Empty`
  and nutrition was lost
- Machine-gun water intake — `BiologicalConditionSystem` was spawning a new water
  entity every tick; capped with `HydrationQueued` threshold
- Water absorption rate math — `HydrationValue` raised from 15 to 30 so hydration
  recovery can outpace the drain rate
- Food never entering — `BiologicalConditionSystem` always won the throat race
  before `FeedingSystem` ran; resolved with priority heuristic (later replaced
  properly by `BrainSystem` in 0.3.0)
- `IServiceProvider` compile error — `using System;` added to `App.axaml.cs`
  (project lacks `ImplicitUsings`)

---

## [0.1.0] — Initial

### Added
- Hand-rolled ECS framework
  - `Entity` — `Guid` ID, `Dictionary<Type, object>` component store
  - `EntityManager` — entity lifecycle and generic `Query<T>()` method
  - `ISystem` — `Update(EntityManager, float)` interface
  - `SimulationEngine` — ordered system list, `Update(realDelta)` loop
  - `SimulationClock` — `TotalTime`, `TimeScale`, `DeltaTime`
- `MetabolismSystem` — hunger/thirst accumulation (later redesigned in 0.2.0)
- `BiologicalConditionSystem` — tag management for biological states
- `DesireSystem` — maps biological tags to desire tags
- `FeedingSystem` — hardcoded banana source when hungry
- `InteractionSystem` — bite/swallow mechanics for held food
- `EntityTemplates` — `SpawnHuman`, `SpawnCat` factories
- `Tags.cs` — biological urge, vital state, entity identity, and desire tags
- `IdentityComponent`, `BolusComponent`, `FoodObjectComponent`,
  `EsophagusTransitComponent`, `MetabolismComponent`
