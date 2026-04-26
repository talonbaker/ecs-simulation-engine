# WP-2.0.C — batch-scheduler-cross-spec-dedup-fix — Completion Note

**Executed by:** sonnet-1
**Branch:** feat/wp-2.0.C
**Started:** 2026-04-26T00:00:00Z
**Ended:** 2026-04-26T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

`BatchScheduler.RunAsync` was keying all internal dictionaries by `ScenarioId` alone. When two `ScenarioBatch` objects (from `spec-smoke-01.json` and `cast-validate.json`) both contained scenarios `sc-01`–`sc-05`, the `.ToDictionary()` call on the flat scenario list crashed with `ArgumentException: An item with the same key has already been added. Key: sc-01`.

The fix introduces a `ScenarioKey(BatchId, ScenarioId)` composite key used throughout `BatchScheduler.RunAsync` — in `haikuIdFor`, `uniqueResults`, `dupToOrig`, and the Anthropic `custom_id` field (encoded as `"<batchId>::<scenarioId>"`, 64-char max enforced). The redundant `parentBatchIdFor` map was eliminated; `key.BatchId` carries that information directly. `ScenarioDeduper.Deduplicate` was extended to accept `(batchId, scenario)` pairs and map duplicate `ScenarioKey` → original `ScenarioKey`. `MockAnthropic.StreamBatchResultsAsync` was updated to echo the composite `custom_id` back while looking up canned responses by plain `scenarioId`. `Warden.Orchestrator.Tests.csproj` was given `<Content>` items to copy the two reference files used by `cast-validate.json` to the test output directory so that `InlineReferenceFiles.Build` can find them.

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 `AT01_MockRun_ExitsZeroAndWritesLedger` | Pass | Exit code 0 |
| AT-02 `AT01b_MockRun_TenIterations_AllSucceed` | Pass | All 10 iterations exit 0 |
| AT-03 `AT_BS_X_TwoBatches_SameScenarioIds_BothDispatchAndReturn` | Pass | New test; 6 results, correct `ParentBatchId` per batch |
| AT-04 `AT_BS_X_CustomIdRoundTrip_RecoversBatchAndScenarioIds` | Pass | New test; round-trip and 64-char limit enforced |
| AT-05 Existing `BatchSchedulerTests` WP-07 ATs | Pass | 0 regressions |
| AT-06 All other `Warden.Orchestrator.Tests` | Pass | 136 total, 0 failures |
| AT-07 `dotnet test ECSSimulation.sln` | Pass | 657 total across 6 projects, 0 failures |
| AT-08 Build warning count | Pass | 0 warnings |
| AT-09 64-char `custom_id` validation | Pass | Covered within AT-04 |

## Files added

(none)

## Files modified

| File | Change |
|:---|:---|
| `Warden.Orchestrator/Batch/ScenarioDeduper.cs` | Added `ScenarioKey` record struct; extended `Deduplicate` to accept `(batchId, scenario)` pairs; `DupToOrig` now maps `ScenarioKey → ScenarioKey` |
| `Warden.Orchestrator/Batch/BatchScheduler.cs` | Full `RunAsync` rewrite: composite keys throughout; `parentBatchIdFor` eliminated; `BuildCompositeId` / `ParseCompositeCustomId` helpers added |
| `Warden.Orchestrator/Mocks/MockAnthropic.cs` | `StreamBatchResultsAsync` echoes composite `custom_id`; extracts plain `scenarioId` for canned-response lookup via `ExtractScenarioId` |
| `Warden.Orchestrator.Tests/Batch/BatchSchedulerTests.cs` | `BuildResultsJsonl` updated for composite `custom_id`; AT06 JSONL fixture updated; two new ATs added (AT-03, AT-04) |
| `Warden.Orchestrator.Tests/Warden.Orchestrator.Tests.csproj` | Added `<Content>` items to copy `office-starter.json` and `archetypes.json` to the test output directory |

## Diff stats

5 files changed, ~320 insertions(+), ~77 deletions(-)

## Followups

(none)
