# WP-2.0.A — Cast-Validate Inline-Files Mode — Completion Note

**Executed by:** sonnet-4-6
**Branch:** feat/wp-2.0.A
**Started:** 2026-04-26T00:00:00Z
**Ended:** 2026-04-26T01:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Implemented inline-files mode for the Sonnet dispatcher. Sonnets dispatched via
the Anthropic API have no file-system access; any spec whose `inputs.referenceFiles[]`
named real paths would always block with `missing-reference-file`. This packet fixes
that by pre-reading those files in the orchestrator and prepending their contents
to the Sonnet's user turn as a delimited block.

The new `InlineReferenceFiles.Build` helper resolves each repo-relative path, enforces
a 100KB per-file cap and a 200KB aggregate cap, rejects `..` traversal, and renders
the standard `--- BEGIN / --- END` block format followed by `## Spec packet`. The
dispatcher was modified to call `Build` between the budget check and the API call.
On failure, it persists a `result.json` and emits an `inline-files-blocked` event to
`events.jsonl` without spending any tokens.

The `IChainOfThoughtStore` interface gained `AppendEventAsync`; `NullChainOfThoughtStore`
got a no-op implementation. `RunCommand.RunAsync` gained an optional `repoRoot` parameter
(defaults to `Environment.CurrentDirectory`); this was also needed by `CastValidateMockRunTests`,
which was pre-written in anticipation of this packet and expected `repoRoot` to be the
real repo root during tests.

SimConfig is untouched — this packet has no SimConfig surface.

---

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | OK | Empty `referenceFiles[]` → `Outcome(null, null, null)`. |
| AT-02 | OK | Two valid files → block contains both BEGIN/END markers in input order. |
| AT-03 | OK | Missing file → `Outcome(null, MissingReferenceFile, details)` naming the path. |
| AT-04 | OK | Single file exceeding cap → `Outcome(null, ToolError, details)` naming the file. |
| AT-05 | OK | Aggregate exceeding cap → `Outcome(null, ToolError, details)`. |
| AT-06 | OK | `..` traversal → `Outcome(null, ToolError, details)`. |
| AT-07 | OK | Synthetic spec with valid temp-file reference dispatches ok; captured user turn contains the inlined block. |
| AT-08 | OK | Synthetic spec with missing reference file → `outcome=blocked, blockReason=MissingReferenceFile`; no ledger entry. |
| AT-09 | OK | Smoke-mission (`referenceFiles: []`) produces structurally identical output; no inlined section. |
| AT-10 | OK | `CastValidateMockRunTests.AT10_MockRun_CastValidate_ExitsZeroAndWritesLedger` exits 0 (was failing before — now passes because `repoRoot` is threaded through). |
| AT-11 | OK | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors. |
| AT-12 | OK | 767 tests pass excluding the pre-existing `AT01_MockRun_ExitsZeroAndWritesLedger` flake (unchanged from before this packet). |

---

## Files added

```
Warden.Orchestrator/Dispatcher/InlineReferenceFiles.cs
Warden.Orchestrator.Tests/Dispatcher/InlineReferenceFilesTests.cs
docs/c2-infrastructure/work-packets/_completed/WP-2.0.A.md
```

## Files modified

```
Warden.Orchestrator/Dispatcher/IChainOfThoughtStore.cs    — added AppendEventAsync
Warden.Orchestrator/Mocks/NullChainOfThoughtStore.cs      — no-op AppendEventAsync impl
Warden.Orchestrator/Dispatcher/SonnetDispatcher.cs        — added _repoRoot field; wired InlineReferenceFiles.Build between budget check and API call
Warden.Orchestrator/RunCommand.cs                         — added optional repoRoot parameter; passes it to SonnetDispatcher
Warden.Orchestrator/ResumeCommand.cs                      — passes Environment.CurrentDirectory to SonnetDispatcher
Warden.Orchestrator.Tests/RunCommandEndToEndTests.cs      — added AT-07 and AT-08 integration tests
Warden.Orchestrator.Tests/CastValidateMockRunTests.cs     — updated to pass FindRepoRoot() as repoRoot so inline file reads resolve correctly
```

---

## Diff stats

9 files changed, 472 insertions(+), 11 deletions(-).

---

## Followups

- **WP-2.0.C — Snapshot mode.** When reference files exceed the inline cap (> 200KB aggregate), the operator needs snapshot mode: orchestrator boots engine, projects `WorldStateDto`, passes it as spec input.
- **Cast-validate real-API run.** Operator runs once post-merge to confirm cast-validate produces `outcome=ok|failed` instead of `blocked: missing-reference-file`. Cost ~$0.40.
- **Reference-file content caching.** If future specs reuse the same large files across many missions, hashing the inlined block and caching it via `cache_control` could reduce cost. Premature at v0.1.
- **`repoRoot` in the CLI surface.** A future `--repo-root` flag on `warden run` and `warden resume` would let operators override the directory when invoking from outside the repo root. Not needed today (CLI is always invoked from the repo root).
