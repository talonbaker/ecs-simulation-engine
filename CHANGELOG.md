# Changelog

All notable changes to this project are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

### Planned
- `AutonomySystem` — Billy's mood-gated disobedience mechanic
- World food/water sources as spawnable entities (fridge, sink, bowl)
- Proximity / spatial grid system — so RotTag on nearby food raises Disgust before consumption
- Nutrient/vitamin absorption layer — tracking macros and micros through the full digestion track
- Lua scripting layer for moddable system/mechanic definitions (post-stabilization)

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
