# WP-2.0.B — CostLedger Concurrency Fix

**Tier:** Sonnet
**Depends on:** Phase 1 closure (no Phase-2 dependencies)
**Parallel-safe with:** WP-2.0.A (different file footprint), WP-2.1.A (different project)
**Timebox:** 45 minutes
**Budget:** $0.20

---

## Goal

Fix the pre-existing flake in `Warden.Orchestrator.Tests.RunCommandEndToEndTests.AT01_MockRun_ExitsZeroAndWritesLedger` documented in PHASE-1-HANDOFF §4.1 and §6 backlog item 5. The test fails reproducibly on Windows with `IOException: file in use` on `cost-ledger.jsonl` during `CostLedger.AppendAsync`. Reproduces both with and without the §4.1 patches, so it's not a regression — it's a long-standing race condition that surfaced only when the post-closure verification exercised the code path more aggressively.

Root cause: `CostLedger.AppendAsync` is a thin wrapper around `File.AppendAllTextAsync` with no concurrency control. On Windows, when the writer's `FileStream` overlap with another reader (the test's post-run assertion that reads the file, or the report aggregator's `ReadLedger`), the OS occasionally refuses the new write because the previous handle hasn't fully released. On Unix this would silently succeed because POSIX file semantics permit overlap; on Windows the strict file-handle ownership produces the visible failure.

Fix: serialize all `AppendAsync` calls through a `SemaphoreSlim(1, 1)` held on the `CostLedger` instance. Single-line addition; no behaviour change other than ordering. The ledger is per-run (one `CostLedger` instance per `RunCommand.RunAsync`), so the semaphore scope is naturally bounded.

After this packet, the orchestrator unit suite passes 100% green without exclusions; CI / dispatch flow no longer needs the `--filter` workaround.

---

## Reference files

- `docs/c2-infrastructure/00-SRD.md` — §4.1 (fail-closed policy). Background only; this packet has no fail-closed surface to add.
- `docs/c2-infrastructure/PHASE-1-HANDOFF.md` — §4.1 (the bug doc), §6 backlog item 5 (the punch-list entry).
- `Warden.Orchestrator/Infrastructure/CostLedger.cs` — the file to fix. Currently 44 lines; the patch adds ~5 lines (SemaphoreSlim field, async lock-release pattern in `AppendAsync`).
- `Warden.Orchestrator.Tests/RunCommandEndToEndTests.cs` — the failing test. Read AT-01 specifically. Don't modify it; the fix must make it pass as-written.
- `Warden.Orchestrator.Tests/Infrastructure/CostLedgerTests.cs` *(may not exist; confirm)* — if there's an existing test file for CostLedger, add the new stress-loop test there; otherwise create it. Don't refactor existing tests.
- `Warden.Orchestrator/Infrastructure/LedgerEntry.cs` — the entry shape. Read for context only; do not modify.

## Non-goals

- Do **not** redesign the `CostLedger`. The mutex addition is the entire fix. Don't introduce queueing, batching, async channels, or background flush threads. Keep the surface (`AppendAsync`, `ReadAllAsync`) identical.
- Do **not** change the wire format of `LedgerEntry` or the JSONL file. Existing readers (the report aggregator, `ReadAllAsync`) must continue to consume the file unchanged.
- Do **not** add `IDisposable` to `CostLedger` unless absolutely necessary for the chosen synchronisation primitive. `SemaphoreSlim` is technically `IDisposable` but is conventionally allowed to be GC-finalized in short-lived per-run instances; add `IDisposable` only if you adopt the `FileStream`-held-open variant.
- Do **not** modify `RunCommand.cs` or any caller of `CostLedger`. The fix is contained to the ledger class and its tests.
- Do **not** introduce a NuGet dependency. `SemaphoreSlim` is in the BCL.
- Do **not** modify any other file under `Warden.Orchestrator/`. The cast-validate fix (WP-2.0.A) is a parallel packet and may touch the dispatcher and `RunCommand`; this packet must not collide.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing.

---

## Design notes

### The patch shape

Two changes to `Warden.Orchestrator/Infrastructure/CostLedger.cs`:

```csharp
public sealed class CostLedger
{
    private readonly string _ledgerPath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);   // ← new

    public CostLedger(string ledgerPath) { /* unchanged */ }

    public async Task AppendAsync(LedgerEntry entry, CancellationToken ct = default)
    {
        var line = JsonSerializer.Serialize(entry, JsonOptions.Wire) + "\n";
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);   // ← new
        try
        {
            await File.AppendAllTextAsync(_ledgerPath, line, ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();                              // ← new
        }
    }

    public async Task<IReadOnlyList<LedgerEntry>> ReadAllAsync(...) { /* unchanged */ }
}
```

That's the entire production change. `ReadAllAsync` remains untouched — readers don't conflict with each other and `File.ReadAllLinesAsync` opens with shared-read access by default.

### Why a semaphore over a `lock` keyword

`File.AppendAllTextAsync` is async, and `lock { await ... }` is a compile error. `SemaphoreSlim` is the canonical async-friendly mutual-exclusion primitive in .NET. Initial count 1, max count 1 — strict serialisation, FIFO under contention.

### Why not switch to a held `FileStream`?

A `FileStream` opened once with `FileShare.Read` and held for the run's lifetime would also work, and would be marginally faster per write (no open/close overhead). Two reasons to prefer the semaphore variant:

1. **Cancellation semantics.** The current `AppendAllTextAsync(ct)` cancels cleanly mid-write; a held `FileStream` would need a custom flush-and-close on cancellation, which is more code and more failure modes.
2. **Failure recovery.** If a held `FileStream` enters a faulted state (disk full, permission change), recovering requires reopening the handle with potential lost-write semantics. The semaphore variant fails per-write with a normal `IOException` that the caller (and Polly retries upstream) can handle.

The semaphore is the conservative, well-understood fix. Faster paths can come later if profiling shows ledger writes dominating any real workload (they won't — at most a few dozen writes per mission).

### Cancellation behaviour

`SemaphoreSlim.WaitAsync(ct)` honours cancellation. If a caller cancels while waiting for the lock, `OperationCanceledException` propagates and the `finally` block's `Release()` is skipped — but only because the `WaitAsync` never granted entry, so there's nothing to release. The pattern is correct as written. Verify in a test.

### Tests

Two test additions:

**Stress test (the AT-01 fix):** loop the existing `AT01_MockRun_ExitsZeroAndWritesLedger` body 10 times in a single `[Fact]` and assert all 10 succeed. This is the existing test's pattern, just iterated; lives in `RunCommandEndToEndTests.cs` next to AT-01. Naming: `AT01b_MockRun_TenIterations_AllSucceed`.

**Direct concurrency unit test:** in a new (or extended) `CostLedgerTests.cs`, fire 100 parallel `AppendAsync` calls against one `CostLedger` instance, then read the file back and assert the line count is exactly 100 and every line parses as a valid `LedgerEntry`. This proves the lock works at high contention. Naming: `AT-CL-01_ParallelAppends_AllPersistedAndParseable`.

Plus: ensure the existing `RunCommandEndToEndTests.AT01_MockRun_ExitsZeroAndWritesLedger` now passes without the `--filter` exclusion that PHASE-1-HANDOFF §4.1 documented. (This is a regression-removal, not a new test.)

### What if the test still fails after this fix?

If the loop test or the parallel-append test still fails, the race is not where the diagnosis assumed. In that case the Sonnet should:

1. Capture the failure stack trace and the exact `IOException` message verbatim into the completion note.
2. Mark the outcome as `blocked: tool-error`, with details enumerating what was tried and what symptoms remain.
3. Stop. Do not try alternative locking strategies (process-level mutex, FileStream variants, etc.) without a fresh packet authored on top of the new evidence.

This is fail-closed per SRD §4.1 — if the fix doesn't take, the next packet refines the design with the new diagnostic data, rather than the Sonnet exploring locking-strategy space on its own.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Orchestrator/Infrastructure/CostLedger.cs` (modified) | Add `SemaphoreSlim _writeLock = new(1, 1);` field; wrap the body of `AppendAsync` in `WaitAsync` / `try` / `Release` per Design notes. |
| code | `Warden.Orchestrator.Tests/Infrastructure/CostLedgerTests.cs` (new or extended) | Add `AT_CL_01_ParallelAppends_AllPersistedAndParseable`. If the file doesn't exist, create it with the standard `IDisposable` + temp-dir scaffold from `BatchSchedulerTests.cs`. |
| code | `Warden.Orchestrator.Tests/RunCommandEndToEndTests.cs` (modified) | Add `AT01b_MockRun_TenIterations_AllSucceed` next to the existing AT01. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-2.0.B.md` | Completion note. Standard template. Confirm the fix took (existing AT01 + new AT01b + new AT-CL-01 all pass), report whether 100 parallel appends produced 100 valid lines, and note any platform-specific observations (the bug is Windows-specific; the fix should be platform-agnostic but worth confirming Linux/Mac CI behaviour stays clean). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `Warden.Orchestrator.Tests.RunCommandEndToEndTests.AT01_MockRun_ExitsZeroAndWritesLedger` passes (the previously-flaky test, unmodified, now green). | unit-test |
| AT-02 | New `AT01b_MockRun_TenIterations_AllSucceed` passes — 10 iterations of the AT01 body in one `[Fact]`, all succeed. | unit-test |
| AT-03 | New `AT_CL_01_ParallelAppends_AllPersistedAndParseable` passes — 100 parallel `AppendAsync` calls produce a file with exactly 100 valid `LedgerEntry` lines. | unit-test |
| AT-04 | Cancellation: a `CancellationTokenSource` cancelled mid-`WaitAsync` causes `AppendAsync` to throw `OperationCanceledException` cleanly; subsequent appends succeed normally. | unit-test |
| AT-05 | Existing `Warden.Orchestrator.Tests` (122 non-flaky) all stay green. | build + unit-test |
| AT-06 | `dotnet test ECSSimulation.sln` (no filter) — every test passes, 0 failures, 0 skipped. | build + unit-test |
| AT-07 | `dotnet build ECSSimulation.sln` warning count = 0. | build |

---

## Followups (not in scope)

- **CI multi-platform check** — confirm the fix behaves identically on Linux/Mac runners. POSIX file semantics already permit the previously-failing pattern, so the fix should be a no-op there, but worth verifying once when the CI matrix exists.
- **Held-FileStream variant** — if profiling ever shows ledger-write overhead matters, switch to a held `FileStream(FileShare.Read)`. Document the cancellation/recovery handling at that point. Pending evidence; v0.1's semaphore is fine.
- **Process-level mutex** — only relevant if multiple orchestrator processes ever wrote to the same ledger file (they don't today; one `CostLedger` instance per `RunCommand`). Skip.
- **Test-time injection** — the parallel-append test could be moved into a Theory with parameterised lock counts (1, 10, 100, 1000) once the basic case is proven. Polish; not v0.1.
