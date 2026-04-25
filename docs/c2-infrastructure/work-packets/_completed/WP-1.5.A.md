# WP-1.5.A — lighting-to-drive-coupling — Completion Note

**Executed by:** sonnet-4.6
**Branch:** feat/wp-1.5.A
**Started:** 2026-04-25T00:00:00Z
**Ended:** 2026-04-25T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Landed `LightingToDriveCouplingSystem` — the per-tick system that translates a room's illumination state into social drive deltas for NPCs inside it. Five bible mappings are now wired: flickering → irritation + loneliness; warm desk lamp → belonging + affection; dark hallway after-hours → suspicion + irritation; sun aperture beam → loneliness recovery + belonging; pitch dark → belonging decay + loneliness. A sixth entry (server-room LED → mild irritation) was added per the bible's suggestion.

**Key judgement calls:**

1. **`AiDescribeCommand.AppendSimConfig` IList guard.** The reflection walk in `AppendSimConfig` does not filter out indexer properties (`GetIndexParameters().Length > 0`). `List<LightingCouplingEntry>` has an `Item[int]` indexer that, when read without an index argument, throws `TargetParameterCountException` and produced exit-code 1 in `ECSCli.Tests`. Added a `GetIndexParameters().Length == 0` filter and a `IList` branch (emits `[N entries]`) to the reflection walk. This is not in the formal deliverables list but was required to keep existing tests green.

2. **Config → Coupling namespace dependency.** `LightingConfig` in `APIFramework.Config` references `LightingCouplingEntry` from `APIFramework.Systems.Coupling`. This is a minor layering compromise; the alternative (duplicating config-shape classes) was worse.

3. **`Coupling = 8` phase.** Added between `Lighting = 7` and `Physiology = 10`, satisfying the packet requirement to run after illumination is fresh and before drive dynamics.

4. **First-match-wins ordering in SimConfig.** The aperture-beam entry (entry 3) appears before the pitch-dark entry (entry 4), so sunlit rooms don't also trigger pitch-dark loneliness.

**Deferred per packet spec:**
- Per-archetype lighting tolerance (Greg's server-room indifference) — WP-1.5.B.
- Per-tile illumination — future.

---

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | ✓ | `AT01_EmptyWorld_RunsWithoutError` — system handles empty world (no NPCs, no rooms). |
| AT-02 | ✓ | `AT02_FlickeringSource_InHallway_IrritationIncrements4OrMoreOver60Ticks` — irritation ≥ 4, loneliness ≥ 1 after 60 ticks at 0.08/tick. |
| AT-03 | ✓ | `AT03_WarmDeskLamp_InOffice_ProducesBelongingAndAffectionDelta` — belonging ≥ 1, affection ≥ 1 after 30 ticks. |
| AT-04 | ✓ | `AT04_DimHallway_Evening_ProducesSuspicionAndIrritationDelta` — suspicion ≥ 2, irritation ≥ 1 after 25 ticks. |
| AT-05 | ✓ | `AT05_SunBeamPresent_ProducesBelongingIncreaseAndLonelinessDecrease` — loneliness < 30, belonging ≥ 1 after 40 ticks with south-facing aperture at noon. |
| AT-06 | ✓ | `AT06_PitchDarkRoom_ProducesBelongingDecayAndLonelinessRise` — belonging < 30, loneliness ≥ 2 after 40 ticks at ambient=2. |
| AT-07 | ✓ | `AT07_NpcWithNoRoom_ReceivesNoDelta` — all drives remain 0 after 100 ticks with no room membership. |
| AT-08 | ✓ | `AT08_FirstMatchWins_OnlyFirstMatchingEntryApplies` — irritation += 5 (entry 0), loneliness unchanged (entry 1 not applied). |
| AT-09 | ✓ | `AT09_SubOneDelta_AccumulatesAndProducesIntegerIncrementsAtExpectedRate` — 0.25/tick × 100 ticks = exactly 25 integer increments. |
| AT-10 | ✓ | `AT10_DriveCurrentClampsAt100_WithSustainedPositiveDeltas` — drive stays at 100 after 20 ticks of +10/tick. |
| AT-11 | ✓ | `AT11_DriveCurrentClampsAt0_WithSustainedNegativeDeltas` — drive stays at 0 after 20 ticks of -10/tick from 50. |
| AT-12 | ✓ | `CouplingDeterminismTests.TwoRunsSameSeed_ProduceIdenticalDriveTrajectories_Over5000Ticks` — two 5000-tick runs with varied lighting produce byte-identical trajectories. |
| AT-13 | ✓ | All 399 `APIFramework.Tests` pass (567 total with prior wave). |
| AT-14 | ✓ | `Warden.Telemetry.Tests` (31 passed), `Warden.Contracts.Tests` (50 passed). |
| AT-15 | ✓ | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors. |
| AT-16 | ✓ | `dotnet test ECSSimulation.sln` — 636 passed across all test projects, 0 failed. |

---

## Files added

```
APIFramework/Systems/Coupling/CouplingCondition.cs
APIFramework/Systems/Coupling/LightingDriveCouplingTable.cs
APIFramework/Systems/Coupling/SocialDriveAccumulator.cs
APIFramework/Systems/Coupling/LightingToDriveCouplingSystem.cs
APIFramework.Tests/Systems/Coupling/LightingToDriveCouplingSystemTests.cs
APIFramework.Tests/Systems/Coupling/CouplingDeterminismTests.cs
docs/c2-infrastructure/work-packets/_completed/WP-1.5.A.md
```

## Files modified

```
APIFramework/Core/SystemPhase.cs              — add Coupling = 8 phase between Lighting and Physiology
APIFramework/Config/SimConfig.cs             — add LightingCouplingEntry reference; add DriveCouplings to LightingConfig
APIFramework/Core/SimulationBootstrapper.cs  — add DriveCouplingTable + DriveAccumulator properties; register LightingToDriveCouplingSystem in Coupling phase
SimConfig.json                               — add lighting.driveCouplings array (6 entries covering all bible conditions)
ECSCli/Ai/AiDescribeCommand.cs              — filter indexer properties + add IList branch to prevent TargetParameterCountException on List<T>
```

## Diff stats

12 files changed (7 added, 5 modified). Approximately 550 insertions across production code; ~350 insertions in tests.

## Followups

- WP-1.5.B: per-archetype lighting tolerance — Greg's `Hermit` archetype tolerates server-room LED glow; reads `LightingToleranceComponent` populated by cast generator.
- Per-tile illumination: when a system needs "is this exact tile lit," derive from per-room illumination plus aperture beam projection.
- Lighting directly affects movement-speed multiplier (beyond irritation path): dim-hallway direct speed modifier.
- `AiDescribeCommand.AppendSimConfig` IList handling: the current fix emits `[N entries]` for list-type config properties. A richer emitter could enumerate drive coupling entry conditions inline for better describe output.
- World-bootstrap packet (WP-1.7.A): once real rooms are instantiated, the coupling table will exercise room-category matching against actual `CubicleGrid`, `Office`, etc. entities.
