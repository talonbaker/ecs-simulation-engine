# WP-13 — Wire Banned-Pattern Detection into SonnetDispatcher

**Tier:** Sonnet
**Depends on:** WP-09, WP-11
**Timebox:** 45 minutes
**Budget:** $0.20

---

## Goal

Close the gap flagged in WP-11's completion-note followups: `FailClosedEscalator.Evaluate(SonnetResult, string? worktreeDiff)` exists and is tested, `BannedPatternDetector.HasBannedPattern(string diff)` exists and is tested — but `SonnetDispatcher.RunAsync` never retrieves a worktree diff or passes it in. The check is code, not live policy. This packet makes it live.

---

## Reference files

- `Warden.Orchestrator/Dispatcher/SonnetDispatcher.cs`
- `Warden.Orchestrator/Dispatcher/FailClosedEscalator.cs`
- `Warden.Orchestrator/Dispatcher/BannedPatternDetector.cs`
- `Warden.Contracts/Handshake/SonnetResult.cs` (has a `WorktreePath` field)
- `docs/c2-infrastructure/work-packets/_completed/WP-11.md` §Followups
- `docs/c2-infrastructure/00-SRD.md` §4.1

## Non-goals

- Do not modify the escalator's or detector's public API surfaces. They are done.
- Do not change the `SonnetResult` schema. Work with the existing fields.
- Do not add retry or auto-remediation. A banned pattern is a terminal block.
- Do not touch `HaikuDispatcher`. Haikus don't edit code; they read telemetry.

---

## Architectural note

In Phase-0's API-driven dispatch, the Sonnet returns a `SonnetResult` JSON whose `WorktreePath` may be a real `./runs/<runId>/sonnet-<n>/worktree/` path *or* may be unset / not a real git working tree (Sonnets-via-API don't necessarily populate one). The implementation must degrade gracefully in both cases: if a real worktree diff can be obtained, feed it to the escalator; if not, the check is a no-op and execution continues per the state machine alone. This is fail-safe, not fail-silent — a missing diff is logged at `Information` level.

When Phase 1+ introduces worktree-based Sonnet execution for real, this wiring is already in place and enforcement becomes effective without further plumbing.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Orchestrator/Dispatcher/IWorktreeDiffSource.cs` | Interface: `Task<string?> GetDiffAsync(string? worktreePath, CancellationToken ct)`. Returns null if path is null, empty, or not a valid git working tree. Never throws on git errors — returns null and logs. |
| code | `Warden.Orchestrator/Dispatcher/GitWorktreeDiffSource.cs` | Default implementation. Shells out via `System.Diagnostics.Process` to `git diff main...HEAD` inside the worktree. 10-second timeout. Returns null on any non-zero exit code, timeout, or missing `git` binary. Constructor takes `ILogger<GitWorktreeDiffSource>`. |
| code | `Warden.Orchestrator/Dispatcher/NullWorktreeDiffSource.cs` | Test-time stub. Constructor takes an optional `string? cannedDiff`; `GetDiffAsync` returns it verbatim. |
| code | `Warden.Orchestrator/Dispatcher/SonnetDispatcher.cs` (modified) | Constructor gains an `IWorktreeDiffSource` parameter. After successful schema validation of the Sonnet response and before ledger/CoT persistence, call `diffSource.GetDiffAsync(result.WorktreePath, ct)`. Pass the result (string-or-null) to `FailClosedEscalator.Evaluate(result, diff)`. If the verdict's `TerminalOutcome` is `Blocked` while `result.Outcome == Ok`, return a new `SonnetResult` with `Outcome=Blocked`, `BlockReason=ToolError`, and `BlockDetails` containing the verdict's `HumanMessage`. The ledger entry for the call is still written (the call happened, the money was spent) — only the return value is overridden. |
| code | `Warden.Orchestrator/RunCommand.cs` (or the DI composition root, wherever `SonnetDispatcher` is instantiated) | Wire `GitWorktreeDiffSource` as the default. The mock-anthropic path should use `NullWorktreeDiffSource` with a null canned diff so mock runs skip the check (matching pre-WP-13 behaviour). |
| code | `Warden.Orchestrator.Tests/Dispatcher/SonnetDispatcherBannedPatternTests.cs` | See acceptance. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-13.md` | Completion note. Call out explicitly whether the default wiring path uses `GitWorktreeDiffSource` or `NullWorktreeDiffSource` in each of `run`, `resume`, `--mock-anthropic`, and `--dry-run`. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | A Sonnet result with `outcome=ok` and a clean diff (no banned patterns) returns unchanged. | unit-test |
| AT-02 | A Sonnet result with `outcome=ok` but a diff containing any of the six banned patterns is overridden to `outcome=blocked, reason=tool-error`, with `BlockDetails` citing the verdict's `HumanMessage`. | unit-test |
| AT-03 | A Sonnet result with `outcome=blocked` or `outcome=failed` is passed through unchanged — the banned-pattern override never escalates a result that is already non-ok. | unit-test |
| AT-04 | `GitWorktreeDiffSource` with a non-existent path returns null and logs at `Information`. | unit-test |
| AT-05 | `GitWorktreeDiffSource` whose `git diff` subprocess times out returns null and logs at `Warning`. | unit-test |
| AT-06 | `NullWorktreeDiffSource` with `cannedDiff=null` causes the dispatcher to skip the banned-pattern check (equivalent to AT-01 behaviour regardless of `result.Outcome`). | unit-test |
| AT-07 | The ledger entry is written *before* the banned-pattern override. (The money was spent; the override only changes the return value, not the historical record.) | unit-test |
| AT-08 | Existing `RunCommandEndToEndTests` from WP-09 continues to pass with the wiring in place — the smoke mock run still exits 0 and produces a valid report. | unit-test |
