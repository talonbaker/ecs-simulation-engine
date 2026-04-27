# WP-2.6.A — Workload + Tasks System — Completion Note

**Executed by:** sonnet-4-6
**Branch:** feat/wp-2.6.A
**Started:** 2026-04-26T00:00:00Z
**Ended:** 2026-04-26T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Implemented the full Workload + Tasks System giving NPCs concrete work to do. Task entities carry `TaskComponent` (effort, deadline, priority, progress, quality, assignee) and are tagged `TaskTag`. `WorkloadComponent` per-NPC tracks active task GUIDs, capacity, and current load. `TaskGeneratorSystem` (PreUpdate) fires once per game-day at hour 8 and generates `taskGenerationCountPerDay` tasks, assigning them round-robin to NPCs with available capacity via deterministic `SeededRandom`. `WorkloadSystem` (Cleanup=80) advances progress via `baseRate × physiologyMult × stressMult × consciousnessMult`, detects completion (Progress ≥ 1.0 → `TaskCompleted` on narrative bus, entity destroyed), and detects newly overdue tasks (`OverdueTag` + `OverdueTask` narrative event, exactly once per transition). `WorkloadInitializerSystem` (PreUpdate) attaches `WorkloadComponent` from `archetype-workload-capacity.json`. `ActionSelectionSystem` extended with `Work` candidate (weight 0.40) gated on `AtDesk + ActiveTasks.Count > 0`; picks highest-priority task. `StressSystem` extended with overdue-task source: each overdue task contributes `overdueTaskStressGain × neuroFactor` per tick to `AcuteLevel`, tracked in `OverdueTaskEventsToday` (reset on day advance).

All 14 ATs verified. Build: 0 warnings. Tests: 683 pass (114 new), 0 fail, 0 regressions.

## Archetype capacities committed

| Archetype | Capacity |
|:---|:---:|
| the-climber | 5 |
| the-old-hand | 4 |
| the-vent | 3 |
| the-cynic | 3 |
| the-affair | 3 |
| the-crush | 3 |
| the-hermit | 2 |
| the-newbie | 2 |
| the-recovering | 2 |
| the-founders-nephew | 1 |

## SimConfig defaults that survived

| Key | Default |
|:---|:---:|
| taskGenerationHourOfDay | 8.0 |
| taskGenerationCountPerDay | 5 |
| taskEffortHoursMin | 0.5 |
| taskEffortHoursMax | 6.0 |
| taskDeadlineHoursMin | 4.0 |
| taskDeadlineHoursMax | 48.0 |
| taskPriorityMin | 30 |
| taskPriorityMax | 80 |
| baseProgressRatePerSecond | 0.0001 |
| conscientiousnessProgressBias | 0.10 |
| qualityDecayPerStressedTick | 0.0002 |
| qualityRecoveryPerGoodTick | 0.0001 |
| workActionBaseWeight | 0.40 |
| overdueTaskStressGain | 1.0 |

All defaults match the packet's design notes without adjustment.

## Design decisions

- **`StressSystem` new constructor:** The updated constructor adds `WorkloadConfig workloadCfg` and `EntityManager em` parameters. All existing tests using the old 4-parameter constructor were updated to the new 6-parameter form.
- **Daily reset ordering:** The overdue-task source count and the daily reset both happen in the same `Update` pass. On the first tick of a new day, source #4 increments `OverdueTaskEventsToday` and then the day-advance block resets it to 0. This means `OverdueTaskEventsToday` is 0 at the end of the day-boundary tick and starts accumulating fresh on the next tick. Tests reflect this semantics explicitly.
- **`DeadlineTick = -1L`:** Task entities in tests use `DeadlineTick = -1L` to guarantee `(long)TotalTime (0) > -1` without advancing the clock.
- **`FindEntityByGuid` in `StressSystem` and `ActionSelectionSystem`:** Both systems use the constructor-injected `EntityManager` (not the Update parameter) for entity lookups. Tests pass the same `em` instance to both constructor and `Update()`.
- **`Persistent` mapping for new narrative kinds:** `OverdueTask` and `TaskCompleted` were added to `NarrativeEventKind` and emit candidates onto the bus. The `MemoryRecordingSystem` (WP-2.3.A) default `Persistent=false` applies per the packet's non-goal. A follow-up can set `OverdueTask` to `Persistent=true`.

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | pass | TaskComponent and WorkloadComponent construction, equality |
| AT-02 | pass | TaskGeneratorSystem: exact count at trigger hour; round-robin; once-per-day; determinism |
| AT-03 | pass | WorkloadSystem advances progress at baseRate × all multipliers |
| AT-04 | pass | Tired/hungry NPC progresses slower (physiology mult 0.14 vs 1.0) |
| AT-05 | pass | Progress ≥ 1.0 removes task from ActiveTasks; destroys entity; emits TaskCompleted |
| AT-06 | pass | OverdueTag added exactly once at transition; OverdueTask candidate emitted once |
| AT-07 | pass | 3 overdue tasks → AcuteLevel += 3; OverdueTaskEventsToday = 3; scales with neuroticism |
| AT-08 | pass | AtDesk + active task + quiet drives → Work intent targeting highest-priority task |
| AT-09 | pass | Irritation=80 + coworker → Dialog/LashOut overrides Work (weight 0.89 > 0.40) |
| AT-10 | pass | 5000-tick determinism; same seed → byte-identical task priorities and effort hours |
| AT-11 | pass | archetype-workload-capacity.json loads; all 10 archetypes; capacities 1..10 |
| AT-12 | pass | All Wave 1/2/prior Wave 3 tests green; 0 regressions |
| AT-13 | pass | dotnet build: 0 warnings |
| AT-14 | pass | dotnet test: 683 pass, 0 fail |

## Files added

- `APIFramework/Components/TaskComponent.cs`
- `APIFramework/Components/WorkloadComponent.cs`
- `APIFramework/Systems/TaskGeneratorSystem.cs`
- `APIFramework/Systems/WorkloadSystem.cs`
- `APIFramework/Systems/WorkloadInitializerSystem.cs`
- `docs/c2-content/archetypes/archetype-workload-capacity.json`
- `APIFramework.Tests/Components/TaskComponentTests.cs`
- `APIFramework.Tests/Components/WorkloadComponentTests.cs`
- `APIFramework.Tests/Systems/TaskGeneratorSystemTests.cs`
- `APIFramework.Tests/Systems/WorkloadSystemProgressTests.cs`
- `APIFramework.Tests/Systems/WorkloadSystemCompletionTests.cs`
- `APIFramework.Tests/Systems/WorkloadSystemOverdueTests.cs`
- `APIFramework.Tests/Systems/WorkloadDeterminismTests.cs`
- `APIFramework.Tests/Integration/WorkloadStressIntegrationTests.cs`
- `APIFramework.Tests/Integration/ActionSelectionWorkIntegrationTests.cs`
- `APIFramework.Tests/Integration/ActionSelectionWorkOverridingScheduleTests.cs`
- `APIFramework.Tests/Data/ArchetypeWorkloadCapacityJsonTests.cs`

## Files modified

- `APIFramework/Components/IntendedActionComponent.cs` — added `Work` to `IntendedActionKind`
- `APIFramework/Components/StressComponent.cs` — added `OverdueTaskEventsToday` counter
- `APIFramework/Components/Tags.cs` — added `TaskTag`, `OverdueTag`, `BurnedOutFromWorkloadTag`
- `APIFramework/Systems/Narrative/NarrativeEventKind.cs` — added `OverdueTask`, `TaskCompleted`
- `APIFramework/Config/SimConfig.cs` — added `WorkloadConfig` class and `Workload` property
- `APIFramework/Systems/ActionSelectionSystem.cs` — added `Workload` candidate source and `Work` enumeration
- `APIFramework/Systems/StressSystem.cs` — added overdue-task source #4; new constructor params
- `APIFramework/Core/SimulationBootstrapper.cs` — registered three new systems; updated ActionSelectionSystem and StressSystem calls
- `SimConfig.json` — added `"workload"` section
- `APIFramework.Tests/Systems/StressSystemTests.cs` — updated constructor calls
- `APIFramework.Tests/Systems/StressNeuroticismCouplingTests.cs` — updated constructor call
- `APIFramework.Tests/Systems/StressDeterminismTests.cs` — updated constructor call
- `APIFramework.Tests/Systems/StressBurningOutStickyTests.cs` — updated constructor call
- `APIFramework.Tests/Systems/StressWillpowerLoopTests.cs` — updated constructor calls

## Followups

- **`Persistent` flag for `OverdueTask`:** `OverdueTask` events are likely worth remembering (NPC recalls having been late on a task). Trivial follow-up to set `Persistent=true` in WP-2.3.A's kind-mapping.
- **Player-driven task assignment:** Round-robin at v0.1; Phase 3 UI work.
- **Task content / categories:** Anonymous effort blobs at v0.1; future packet adds task kinds with archetype affinity.
- **Multi-NPC tasks and task dependencies:** Deferred per non-goals.
- **Quality gameplay loop:** `QualityLevel` recorded but no consumer yet; follow-up packet.
