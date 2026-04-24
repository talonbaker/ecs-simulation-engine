# WP-10 — Chain-of-Thought Persistence

**Tier:** Sonnet
**Depends on:** WP-09
**Timebox:** 45 minutes
**Budget:** $0.20

---

## Goal

Persist every prompt, response, result, and ledger line to the `./runs/<runId>/` directory structure described in the SRD so an orchestrator crash is recoverable and any human can audit any mission retroactively. Implement `IChainOfThoughtStore` stubbed in WP-09.

---

## Reference files

- `docs/c2-infrastructure/00-SRD.md` §2 Pillar B "Chain-of-thought persistence"
- `Warden.Orchestrator/Dispatcher/IChainOfThoughtStore.cs` (from WP-09)

## Non-goals

- No remote persistence. Local filesystem only.
- No compression. JSONL is human-auditable by design.
- No automatic rotation. Runs accumulate; a human prunes them.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Orchestrator/Persistence/ChainOfThoughtStore.cs` | Concrete `IChainOfThoughtStore`. Writes to `./runs/<runId>/`. |
| code | `Warden.Orchestrator/Persistence/RunId.cs` | `public static string Generate()` = `"{UtcNow:yyyyMMdd-HHmmss}-{slug}"`. Accepts a `--run-id` override. |
| code | `Warden.Orchestrator/Persistence/RunLayout.cs` | Static helper that constructs every path under the run root, keyed by (runId, tier, workerId). Single source of truth for the tree. |
| code | `Warden.Orchestrator/Persistence/ResumeScanner.cs` | Reads a partial run root, returns the list of specs and scenarios that already have `result.json` files. |
| code | `Warden.Orchestrator/ResumeCommand.cs` | `Warden.Orchestrator resume --run-id <s>`. Reloads the mission and specs from the run root, skips completed work, dispatches the rest. |
| code | `Warden.Orchestrator.Tests/Persistence/ChainOfThoughtStoreTests.cs` | See acceptance. |
| code | `Warden.Orchestrator.Tests/Persistence/ResumeScannerTests.cs` | See acceptance. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-10.md` | Completion note. |

---

## Layout (authoritative)

```
./runs/<runId>/
├── mission.md
├── events.jsonl                         # state transitions
├── cost-ledger.jsonl                    # from WP-08
├── report.md                            # from WP-12
├── report.json                          # from WP-12
├── sonnet-<nn>/
│   ├── spec.json
│   ├── prompt.txt                       # assembled slabs 1–4, with boundary markers
│   ├── response.raw.json                # exact Anthropic response body
│   ├── result.json                      # validated SonnetResult
│   ├── worktree/                        # isolated git worktree (touched by Sonnet only)
│   └── haiku-batch.json                 # present iff result.scenarioBatch present
│       └── haiku-<mm>/
│           ├── scenario.json
│           ├── prompt.txt
│           ├── response.raw.json
│           ├── result.json
│           └── telemetry.jsonl          # from the ECSCli replay the Haiku invoked
```

### Write-order invariant (for crash safety)

Per worker, in strict order: `prompt.txt → response.raw.json → result.json`. The orchestrator considers a worker "done" iff `result.json` exists. On resume, any worker missing `result.json` is redispatched from scratch (its partial `prompt.txt`/`response.raw.json` are overwritten).

### Events

Append to `events.jsonl` one line per state transition, never retroactively. Example events:

```json
{"ts":"2026-04-23T14:23:00Z","kind":"run-started","runId":"20260423-142300-smoke"}
{"ts":"2026-04-23T14:23:02Z","kind":"sonnet-dispatched","workerId":"sonnet-01","specId":"spec-smoke-01"}
{"ts":"2026-04-23T14:23:09Z","kind":"sonnet-completed","workerId":"sonnet-01","outcome":"ok"}
{"ts":"2026-04-23T14:23:10Z","kind":"batch-submitted","batchId":"batch-smoke-01","scenarioCount":5}
{"ts":"2026-04-23T14:24:12Z","kind":"batch-ended","batchId":"batch-smoke-01"}
{"ts":"2026-04-23T14:24:13Z","kind":"haiku-completed","workerId":"haiku-01","outcome":"ok"}
{"ts":"2026-04-23T14:24:15Z","kind":"run-completed","exitCode":0}
```

Events file is the operator's fast timeline. Reports derive from it.

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | A successful mock run produces every file listed in the layout. | unit-test |
| AT-02 | Killing the orchestrator mid-mission (after 2 Sonnet completions, before the 3rd) leaves a run root where `resume` continues with only specs 3/4/5. | unit-test |
| AT-03 | `resume` does not redispatch workers whose `result.json` is present and valid. | unit-test |
| AT-04 | `resume` with an invalid `result.json` (schema mismatch) redispatches that worker. | unit-test |
| AT-05 | Event lines are strictly append-only — no line is ever rewritten. (Test by sha-hashing `events.jsonl` after each append.) | unit-test |
| AT-06 | Run id collision (two concurrent mock runs with the same `--run-id`) is rejected at startup. | unit-test |
