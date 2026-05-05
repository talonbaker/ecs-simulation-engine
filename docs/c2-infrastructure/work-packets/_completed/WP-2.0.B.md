# WP-2.0.B — cost-ledger-concurrency-fix — Completion Note

**Executed by:** sonnet-01
**Branch:** feat/wp-2.0.B
**Started:** 2026-04-26T00:00:00Z
**Ended:** 2026-04-26T00:00:00Z
**Outcome:** failed

---

## Summary

Added `SemaphoreSlim _writeLock = new(1, 1)` to `Infrastructure.CostLedger` and wrapped the body of `AppendAsync` in `WaitAsync` / `try` / `Release` exactly as the packet specified. The production change is four lines. The parallel-append unit test (AT_CL_01, 100 concurrent `AppendAsync` calls) passes cleanly, confirming the lock is effective. The cancellation test (AT_CL_02) passes after using `ThrowsAnyAsync<OperationCanceledException>` to accommodate `TaskCanceledException` (which derives from `OperationCanceledException`) thrown by `SemaphoreSlim.WaitAsync`.

However, AT01 and AT01b both fail with `System.ArgumentException: An item with the same key has already been added. Key: sc-01` inside `BatchScheduler.RunAsync` (line 80). The root cause: WP-2.0.A added `examples/smoke-specs/cast-validate.json` alongside `spec-smoke-01.json`; both specs produce `scenarioBatch` entries with scenario IDs `sc-01`–`sc-05`. When the mock run collects both batches and passes them to `BatchScheduler.RunAsync`, the method calls `.ToDictionary()` across all scenarios and throws on the first duplicate. This is a pre-existing `BatchScheduler` collision bug exposed by having two specs in the smoke-specs folder; it is not a CostLedger issue and is explicitly outside this packet's non-goals ("Do not modify any other file under Warden.Orchestrator/").

The `IOException: file in use` flake documented in PHASE-1-HANDOFF §4.1 no longer occurs; the race is fixed. The new failure is a different, unrelated bug that a follow-up packet must address.

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | FAIL | `ArgumentException: An item with the same key has already been added. Key: sc-01` in `BatchScheduler.RunAsync:80`. Not a CostLedger issue. See blocking-reason section. |
| AT-02 | FAIL | Same `ArgumentException` propagated into AT01b body. |
| AT-03 | OK | `AT_CL_01_ParallelAppends_AllPersistedAndParseable` — 100 parallel `AppendAsync` calls produced exactly 100 valid `LedgerEntry` lines. |
| AT-04 | OK | `AT_CL_02_Cancellation_DuringWait_PropagatesToCallerAndLeavesLedgerUsable` — pre-cancelled token throws `OperationCanceledException`; subsequent append succeeds; semaphore left at count=1. |
| AT-05 | OK | 124 non-flaky Orchestrator tests pass (the 2 new ones + 122 existing). Zero regressions. |
| AT-06 | FAIL | Full suite fails: AT01 + AT01b (2 failures in Warden.Orchestrator.Tests; all other assemblies pass). |
| AT-07 | OK | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors. |

## Files added

- `Warden.Orchestrator.Tests/Infrastructure/CostLedgerTests.cs`

## Files modified

- `Warden.Orchestrator/Infrastructure/CostLedger.cs` — Added `SemaphoreSlim _writeLock = new(1, 1)` field; wrapped `AppendAsync` body in `WaitAsync` / `try` / `Release`.
- `Warden.Orchestrator.Tests/RunCommandEndToEndTests.cs` — Added `AT01b_MockRun_TenIterations_AllSucceed` next to existing AT01.

## Diff stats

3 files changed (2 modified, 1 new), ~43 insertions, 1 deletion in modified files.

(`git diff --stat HEAD` for the two modified files; new file is untracked until commit.)

## Followups

- **BatchScheduler cross-batch key collision** — `BatchScheduler.RunAsync` calls `.ToDictionary(sc => sc.ScenarioId)` across all scenarios from all batches in a run. When two specs both produce `sc-01`–`sc-05`, this throws. Fix: either namespace scenario IDs by batch ID before dedup, or use a `scenarioBatch`-scoped dictionary. ~30 minutes of Sonnet work; blocks AT01 and AT01b.
- **TaskCanceledException vs OperationCanceledException** — `SemaphoreSlim.WaitAsync` throws `TaskCanceledException` (derived class), not the base `OperationCanceledException`, when cancelled. Used `ThrowsAnyAsync` to accommodate; no production concern, but worth noting for other callers.

## If outcome ≠ ok: blocking reason

| Field | Value |
|:---|:---|
| `blockReason` | `tests-red` |
| `blockingArtifact` | `Warden.Orchestrator/Batch/BatchScheduler.cs:80` |
| `humanMessage` | Both `spec-smoke-01.json` and `cast-validate.json` in `examples/smoke-specs/` produce scenario batches with IDs `sc-01`–`sc-05`; `BatchScheduler.RunAsync` throws `ArgumentException` when building a cross-batch dictionary — fix the dedup logic in `BatchScheduler` to namespace by `batchId` before calling `ToDictionary`. |

Full failure message verbatim:
```
System.ArgumentException : An item with the same key has already been added. Key: sc-01
   at System.Collections.Generic.Dictionary`2.TryInsert(TKey key, TValue value, InsertionBehavior behavior)
   at System.Collections.Generic.Dictionary`2.Add(TKey key, TValue value)
   at System.Linq.Enumerable.ToDictionary[TSource,TKey,TElement](IEnumerable`1 source, Func`2 keySelector, Func`2 elementSelector, IEqualityComparer`1 comparer)
   at Warden.Orchestrator.Batch.BatchScheduler.RunAsync(String runId, IReadOnlyList`1 batches, CancellationToken ct) in BatchScheduler.cs:line 80
   at Warden.Orchestrator.RunCommand.RunAsync(...) in RunCommand.cs:line 281
   at Warden.Orchestrator.Tests.RunCommandEndToEndTests.AT01_MockRun_ExitsZeroAndWritesLedger() in RunCommandEndToEndTests.cs:line 34
```
