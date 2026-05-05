# WP-1.4.A — Social Engine: Components + Drive Dynamics + Willpower + Relationships — Completion Note

**Executed by:** sonnet-4.6
**Branch:** feat/wp-1.4.A
**Started:** 2026-04-25T00:00:00Z
**Ended:** 2026-04-25T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Landed the Phase-1 social engine. Four new ECS components (`SocialDrivesComponent`, `WillpowerComponent`, `PersonalityComponent`, `InhibitionsComponent`) plus `RelationshipComponent` on relationship entities. Three new systems: `DriveDynamicsSystem` (decay + circadian shape + Neuroticism-scaled noise), `WillpowerSystem` (event-queue drain + sleep regen), `RelationshipLifecycleSystem` (intensity decay + transition-table skeleton). `WillpowerEventQueue` is registered as a singleton on `SimulationBootstrapper`.

**Schema fields the engine now produces values for:** all eight drives (`belonging`, `status`, `affection`, `irritation`, `attraction`, `trust`, `suspicion`, `loneliness`) each with `current`/`baseline`; willpower `current`/`baseline`; all five Big Five traits; vocabulary register; current-mood label; all eight inhibition classes with strength and awareness; relationship patterns (13 values) and intensity.

**Systems iterate which entities:** `DriveDynamicsSystem` and `WillpowerSystem` iterate `NpcTag`; `RelationshipLifecycleSystem` iterates `RelationshipTag`.

**Judgement calls:** (1) Fractional drive deltas accumulate in a per-entity `double[]` inside `DriveDynamicsSystem` and are applied as integer steps only when they cross ±1 — this is required because the raw decay rate (0.15/tick) is less than 1 and would be lost to int truncation on each tick. (2) `AiDescribeCommand.AppendSimConfig` required a one-line fix to short-circuit `IDictionary` values instead of recursing into them; the new config class introduced `Dictionary<string, double>` fields and the reflection walk would have thrown otherwise.

**Deferred:** action-selection, memory recording, projector update (WP-1.4.B), stress integration, cast-generator inhibition installation.

---

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | OK | All components compile, instantiate, clamp, enforce max-counts and canonical ordering. |
| AT-02 | OK | Loneliness drive from 90→50 baseline, 1000 ticks, no noise/circadian: ends at 50. |
| AT-03 | OK | Circadian peak for Loneliness at phase 0.85: average delta positive at peak, negative at anti-peak (1000 seeds). |
| AT-04 | OK | Neuroticism +2 produces higher variance than –2 across 5000 seeds. |
| AT-05 | OK | SuppressionTick magnitude 5 reduces Current by 5, clamped at 0. |
| AT-06 | OK | SleepingTag causes 1 RestTick per tick; Current rises by N × regenPerTick (clamped at 100). |
| AT-07 | OK | Relationship Intensity decays 1 point/tick over 20 ticks (80→60). |
| AT-08 | OK | Transition table loads via LoadFromFile, contains 13 entries (≥ 5); no transition fires in 1000 ticks. |
| AT-09 | OK | RelationshipComponent(7, 3) canonicalizes to (3, 7); two relationships same canonical pair share same ids. |
| AT-10 | OK | SocialDeterminismTests: two runs same seed → identical 5000-tick loneliness trajectory. |
| AT-11 | OK | Warden.Telemetry.Tests: all 24 pass; projector still emits SchemaVersion = "0.1.0". |
| AT-12 | OK | Warden.Contracts.Tests: all 38 pass; DTOs unchanged. |
| AT-13 | OK | dotnet build ECSSimulation.sln — 0 warnings, 0 errors. |
| AT-14 | OK | dotnet test ECSSimulation.sln — 450 passed, 0 failed across all 6 test projects. |

---

## Files added

```
APIFramework/Components/SocialDrivesComponent.cs
APIFramework/Components/WillpowerComponent.cs
APIFramework/Components/PersonalityComponent.cs
APIFramework/Components/InhibitionsComponent.cs
APIFramework/Components/RelationshipComponent.cs
APIFramework/Systems/WillpowerEventSignal.cs
APIFramework/Systems/WillpowerEventQueue.cs
APIFramework/Systems/DriveDynamicsSystem.cs
APIFramework/Systems/WillpowerSystem.cs
APIFramework/Systems/RelationshipLifecycleSystem.cs
APIFramework/Data/RelationshipTransitionTable.json
APIFramework.Tests/Components/SocialDrivesComponentTests.cs
APIFramework.Tests/Components/WillpowerComponentTests.cs
APIFramework.Tests/Components/PersonalityComponentTests.cs
APIFramework.Tests/Components/InhibitionsComponentTests.cs
APIFramework.Tests/Components/RelationshipComponentTests.cs
APIFramework.Tests/Systems/DriveDynamicsSystemTests.cs
APIFramework.Tests/Systems/WillpowerSystemTests.cs
APIFramework.Tests/Systems/RelationshipLifecycleSystemTests.cs
APIFramework.Tests/Systems/SocialDeterminismTests.cs
docs/c2-infrastructure/work-packets/_completed/WP-1.4.A.md
```

## Files modified

```
APIFramework/Components/Tags.cs                  — added NpcTag and RelationshipTag
APIFramework/Components/EntityTemplates.cs       — added WithSocial() builder helper
APIFramework/Config/SimConfig.cs                 — added SocialSystemConfig class and Social property on SimConfig
APIFramework/Core/SimulationBootstrapper.cs      — registered 3 new systems + WillpowerEvents singleton property
SimConfig.json                                   — added "social" section with all tuning knobs
ECSCli/Ai/AiDescribeCommand.cs                   — added IDictionary short-circuit in AppendSimConfig to prevent reflection recursion into Dictionary<string,double> fields
```

## Diff stats

26 files changed (21 added, 5 modified). Approximately 1000 insertions.

## Followups

- WP-1.4.B: update TelemetryProjector to populate social fields and bump SchemaVersion to "0.2.1".
- Action-selection layer: reads drives + willpower + inhibitions; needs Phase 1.1 spatial/proximity.
- Memory recording: proximity events produce drive deltas and write MemoryComponent events.
- Cast-generator (Phase 1.8): populates InhibitionsComponent and WillpowerComponent.Baseline from archetype ranges.
- Stress integration: StressSystem pushes SuppressionTick events into WillpowerEventQueue (replaces open-loop today).
- Pattern-transition trigger wiring: conditions need memory + proximity (Phase 1.1+).
- Slow personality drift and per-archetype circadian shapes: later packet.
- `SimulationBootstrapper.ApplyConfig` hot-reload: does not yet merge SocialSystemConfig on reload.
