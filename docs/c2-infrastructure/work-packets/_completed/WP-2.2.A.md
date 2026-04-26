# WP-2.2.A — schedule-layer — Completion Note

**Executed by:** sonnet-4-6
**Branch:** feat/wp-2.2.A
**Started:** 2026-04-26T00:00:00Z
**Ended:** 2026-04-26T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Implemented the full schedule layer for the ECS simulation engine. Two new systems (`ScheduleSpawnerSystem` at `PreUpdate=0` and `ScheduleSystem` at `Condition=20`) attach per-archetype daily routines to NPCs and resolve the active schedule block each tick. `ActionSelectionSystem` was extended to add one low-weight (0.30) `Approach`/`Linger` candidate from the NPC's current scheduled anchor; drive-driven candidates win when elevated, the schedule wins when drives are quiet.

All 10 archetypes from the cast bible were authored into `docs/c2-content/schedules/archetype-schedules.json`. Anchor IDs were verified against `office-starter.json` and substituted where the cast bible's descriptive names had no matching entity (see Followups below). Each archetype covers the full 24-hour day with 5–7 blocks and a wrap-around `sleeping` block.

`APIFramework/AssemblyInfo.cs` was added to expose `internal static` test helpers (`ScheduleSystem.IsBlockActive`, `ScheduleSpawnerSystem.LoadSchedules`, `ScheduleSpawnerSystem.FindSchedulesFile`) to `APIFramework.Tests` via `InternalsVisibleTo`.

Build: 0 warnings, 0 errors. Test suite: 547 tests pass (35 new, 512 pre-existing).

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | Pass | `ScheduleComponent`, `CurrentScheduleBlockComponent`, `ScheduleBlock` — all construct, round-trip, all activity kinds enumerated |
| AT-02 | Pass | 4-block schedule; correct `ActiveBlockIndex` at each hour boundary; -1 in gap |
| AT-03 | Pass | Wrap-around block (22→06) active at 23:00, 00:30, 05:30; not at 07:00; `IsBlockActive` Theory with 7 cases |
| AT-04 | Pass | Spawner attaches `ScheduleComponent` (5 blocks) + empty `CurrentScheduleBlockComponent` to `the-vent` NPC |
| AT-05 | Pass | Idempotent — running spawner twice does not duplicate blocks |
| AT-06 | Pass | Quiet drives → schedule wins → `Approach` to anchor entity |
| AT-07 | Pass | `Irritation=80` + coworker → drive overrides schedule → `Dialog(LashOut)` |
| AT-08 | Pass | NPC at anchor within threshold + `AtDesk` activity → `Linger` |
| AT-09 | Pass | 5000-tick same-seed run → byte-identical intent stream; different seeds differ |
| AT-10 | Pass | JSON loads; all 10 archetypes present; all anchor IDs valid; every archetype covers 24h with no gaps or overlaps (sampled every 30 min) |
| AT-11 | Pass | All WP-2.1.A acceptance tests (AT-01 through AT-10 in prior test files) still green |
| AT-12 | Pass | All WP-1.x tests still green |
| AT-13 | Pass | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors |
| AT-14 | Pass | `dotnet test … --filter "FullyQualifiedName!~RunCommandEndToEndTests.AT01"` — 547/547 pass |

## Files added

| Path | Description |
|:---|:---|
| `APIFramework/AssemblyInfo.cs` | `[assembly: InternalsVisibleTo("APIFramework.Tests")]` — exposes `internal` test helpers across assembly boundary |
| `APIFramework/Components/ScheduleComponent.cs` | `ScheduleActivityKind` enum, `ScheduleBlock` record struct, `ScheduleComponent` struct, `CurrentScheduleBlockComponent` struct |
| `APIFramework/Systems/ScheduleSystem.cs` | Phase `Condition=20`; resolves active block from `SimulationClock.GameHour`; handles wrap-around blocks; caches anchor Guid per-block |
| `APIFramework/Systems/ScheduleSpawnerSystem.cs` | Phase `PreUpdate=0`; reads `archetype-schedules.json` via upward CWD search (6 levels); attaches `ScheduleComponent` + empty `CurrentScheduleBlockComponent` idempotently |
| `docs/c2-content/schedules/archetype-schedules.json` | All 10 archetypes with 5–7 blocks each; full 24h coverage; anchor IDs verified against `office-starter.json` |
| `APIFramework.Tests/Components/ScheduleComponentTests.cs` | AT-01 (7 tests) |
| `APIFramework.Tests/Systems/ScheduleSystemTests.cs` | AT-02, AT-03 (11 tests) |
| `APIFramework.Tests/Systems/ScheduleSpawnerSystemTests.cs` | AT-04, AT-05 (6 tests) |
| `APIFramework.Tests/Integration/ActionSelectionScheduleIntegrationTests.cs` | AT-06, AT-07, AT-08 (3 tests) |
| `APIFramework.Tests/Systems/ScheduleDeterminismTests.cs` | AT-09 (2 tests) |
| `APIFramework.Tests/Data/ArchetypeSchedulesJsonValidationTests.cs` | AT-10 (3 tests) |

## Files modified

| Path | Change |
|:---|:---|
| `APIFramework/Config/SimConfig.cs` | Added `ScheduleConfig` class (`ScheduleAnchorBaseWeight=0.30`, `ScheduleLingerThresholdCells=2.0f`); added `Schedule` property to `SimConfig` root |
| `APIFramework/Core/SimulationBootstrapper.cs` | Registered `ScheduleSpawnerSystem` (`PreUpdate`) and `ScheduleSystem` (`Condition`); updated `ActionSelectionSystem` registration to include `Config.Schedule` |
| `APIFramework/Systems/ActionSelectionSystem.cs` | Added `CandidateSource` enum; added `Source` field to `Candidate`; added `_scheduleCfg` field; added `scheduleCfg` constructor parameter; added schedule-candidate enumeration block post-drive enumeration |
| `SimConfig.json` | Added `"schedule"` section with `scheduleAnchorBaseWeight` and `scheduleLingerThresholdCells` |
| `APIFramework.Tests/Systems/ActionSelectionSystemTests.cs` | Added `new ScheduleConfig()` argument to `MakeSystem` helper |
| `APIFramework.Tests/Systems/ActionSelectionDeterminismTests.cs` | Added `new ScheduleConfig()` argument to `ActionSelectionSystem` constructor |
| `APIFramework.Tests/Systems/ApproachAvoidanceInversionTests.cs` | Added `new ScheduleConfig()` argument to all `ActionSelectionSystem` constructor calls |
| `APIFramework.Tests/Systems/SuppressionEventEmissionTests.cs` | Added `new ScheduleConfig()` argument to all `ActionSelectionSystem` constructor calls |
| `APIFramework.Tests/Integration/ActionSelectionToMovementTests.cs` | Added `new ScheduleConfig()` argument to all `ActionSelectionSystem` constructor calls |
| `APIFramework.Tests/Integration/ActionSelectionToDialogTests.cs` | Added `new ScheduleConfig()` argument to `ActionSelectionSystem` constructor |

## Diff stats

11 files added, 10 files modified (not counting the completion note itself).

## Followups

- **Anchor ID substitutions** — the cast bible describes archetype desks by name (`vent-desk`, top-floor office, etc.) but `office-starter.json` only has 6 named anchors. The following substitutions were applied and should be reviewed when the floor plan is extended:
  - All per-archetype desk anchors → `the-window` (the main desk anchor in the current office)
  - `the-climber`'s executive corridors → `the-conference-room`
  - `the-founders-nephew`'s top-floor office → `the-conference-room`
  - Smoking bench / outdoor breaks → `the-parking-lot`
  - `the-affair`'s private-meeting anchor → `the-supply-closet`
  When additional named anchors are added to the world definition, update `archetype-schedules.json` to reference them.

- **`NamedAnchorComponent.AnchorId` reference in WP-2.2.A design notes is stale** — the actual property is `.Tag`, not `.AnchorId`. All implementation uses `.Tag` per the live code. Update the design notes if referenced by future packets.

- **Weekday and seasonal awareness** — deferred per packet Non-goals.
- **Activity-driven behaviour** — `Meeting`, `Sleeping` semantics (locking NPCs, suppressing drives) are Phase 3+.
- **Integration with cast generator** — currently bolt-on; Phase 3 refactor.
