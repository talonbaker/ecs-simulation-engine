# Changelog

All notable changes to this project are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

### Planned
- `EnergySystem` — drains energy over time, restores during sleep
- `SleepSystem` — manages sleep/wake cycle, scores SleepUrgency in BrainSystem
- `MoodSystem` — tracks happiness, stress, comfort as affected by sustained need states
- `TimeSystem` — simulates time-of-day and daylight; modifies drive score multipliers
- `AutonomySystem` — Billy's mood-gated disobedience mechanic
- World food/water sources as spawnable entities (fridge, sink, bowl)
- Lua scripting layer for moddable system definitions

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
