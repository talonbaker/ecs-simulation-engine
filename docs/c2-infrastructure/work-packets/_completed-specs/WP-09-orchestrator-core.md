# WP-09 — Orchestrator Core (Task.WhenAll, Concurrency, Program.cs)

**Tier:** Sonnet
**Depends on:** WP-05, WP-06, WP-07, WP-08
**Timebox:** 120 minutes
**Budget:** $0.50

---

## Goal

Stitch the Anthropic client, cache manager, batch scheduler, and cost ledger into a single end-to-end orchestrator binary. Implement the Sonnet fan-out with `Task.WhenAll` bounded by a `SemaphoreSlim(5)`, wire the command-line interface, and ship the mock-Anthropic test harness that lets Phase 0 pass acceptance without spending real tokens.

---

## Reference files

- `docs/c2-infrastructure/00-SRD.md` §2 Pillar B, §6 acceptance
- `docs/c2-infrastructure/01-architecture-diagram.md` (request lifecycle)
- All deliverables from WP-05 through WP-08.

## Non-goals

- Do not implement chain-of-thought persistence in this packet — that is WP-10 (small scope, split for clarity).
- Do not implement report aggregation — WP-12.
- Do not implement the fail-closed state machine in this packet — WP-11 owns it. This packet wires the hook; WP-11 fills in the behaviour.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Orchestrator/Program.cs` | `System.CommandLine` root with subcommands `run`, `resume`, `cost-model`, `validate-schemas`. |
| code | `Warden.Orchestrator/RunCommand.cs` | Entry point for `Warden.Orchestrator run --mission <md> --specs <glob> [--budget-usd <n>] [--mock-anthropic] [--dry-run] [--run-id <s>]`. |
| code | `Warden.Orchestrator/Dispatcher/ConcurrencyController.cs` | `Task.WhenAll` + `SemaphoreSlim(5)` for Sonnets. Surfaces `IProgress<SonnetProgress>` for the console. |
| code | `Warden.Orchestrator/Dispatcher/SonnetDispatcher.cs` | One Sonnet call end-to-end: assemble prompt (WP-06), call Anthropic (WP-05), validate result, emit ScenarioBatch if present, write ledger line (WP-08), persist result (WP-10 — call through an interface this packet declares and stubs). |
| code | `Warden.Orchestrator/Dispatcher/HaikuDispatcher.cs` | Thin wrapper over `BatchScheduler.RunAsync`. |
| code | `Warden.Orchestrator/Dispatcher/IChainOfThoughtStore.cs` | Interface declared here; implementation lands in WP-10. Stub implementation (no-op to stdout) lives in `Mocks/NullChainOfThoughtStore.cs`. |
| code | `Warden.Orchestrator/Mocks/MockAnthropic.cs` | In-process replacement for `AnthropicClient` that returns canned responses based on the `specId`. Activated by `--mock-anthropic`. |
| code | `Warden.Orchestrator/Mocks/MockCannedResponses.cs` | Dictionary of `specId → (SonnetResult, HaikuResult[])` sourced from `./examples/mocks/`. |
| code | `Warden.Orchestrator/RetryPolicy.cs` | Polly pipeline: 3 retries on 429/5xx, exponential backoff starting at 2s, jitter. No retry on 4xx other than 429. |
| code | `Warden.Orchestrator.Tests/Dispatcher/ConcurrencyControllerTests.cs` | Asserts max-in-flight ≤ 5. |
| code | `Warden.Orchestrator.Tests/RunCommandEndToEndTests.cs` | Full mock-Anthropic mission completes, produces ledger file, returns exit code 0. |
| code | `examples/smoke-mission.md` | A tiny mission brief used for AT-01/02 of Phase 0. |
| code | `examples/smoke-specs/spec-smoke-01.json` | One SpecPacket validating a trivial task ("add a code comment to EntityManager.cs"). |
| code | `examples/mocks/spec-smoke-01.sonnet.json` | Canned SonnetResult. |
| code | `examples/mocks/spec-smoke-01.haiku-*.json` | 5 canned HaikuResults. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-09.md` | Completion note. Include the observed max-in-flight count during the smoke run. |

---

## Control flow (`run` subcommand)

```
parse args → load mission.md → load specs (1..5) → validate each against opus-to-sonnet.schema.json
  → prime cache via PromptCacheManager
  → for each spec in parallel (SemaphoreSlim(5)):
       SonnetDispatcher.RunAsync(spec)
         → assemble prompt (cache manager)
         → POST /v1/messages via AnthropicClient (wrapped in Polly)
         → validate response against sonnet-result.schema.json
         → CostLedger.Record(...)
         → ChainOfThoughtStore.Persist(spec, result)
         → FailClosedEscalator.Evaluate(result) → decide whether to proceed
         → if result.scenarioBatch present: add to pending Haiku work
  → wait Task.WhenAll(sonnetTasks)
  → merge all pending ScenarioBatch into Haiku dispatch (≤ 25 total)
  → HaikuDispatcher.RunAsync(...)
  → ReportAggregator.Emit(...) — stub in this WP, filled in WP-12
  → return 0 (all ok) | 1 (at least one failed) | 2 (blocked) | 3 (budget exceeded)
```

---

## Exit codes (contract)

| Code | Meaning |
|:---:|:---|
| 0 | All Sonnets and Haikus returned `outcome = ok`. |
| 1 | At least one worker returned `outcome = failed`. |
| 2 | At least one worker returned `outcome = blocked`. Takes precedence over 1 if both occur. |
| 3 | Budget exceeded; dispatch halted partway. |
| 4 | Unrecoverable orchestrator error (config load failure, API auth failure). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `run --mission examples/smoke-mission.md --specs "examples/smoke-specs/*.json" --mock-anthropic` exits 0 and writes a ledger. | cli-exit-code |
| AT-02 | Max concurrent in-flight Sonnet calls ≤ 5 even when 10 specs are dispatched. | unit-test |
| AT-03 | Retry policy retries a 429 up to 3 times then raises; a 400 raises immediately. | unit-test |
| AT-04 | `--dry-run` skips all API calls and prints the assembled prompts for each tier, then exits 0. | cli-exit-code |
| AT-05 | Passing a spec that fails `opus-to-sonnet.schema.json` validation aborts before any API call, exit code 4. | cli-exit-code |
| AT-06 | `MockAnthropic` returns token counts consistent with `02-cost-model.md` §3 so ledger totals match predicted values. | unit-test |
| AT-07 | Ctrl-C during a mock run cancels within 2 seconds and writes a partial ledger. | unit-test |
| AT-08 | Calling the real API with `--budget-usd 0.05` on the smoke mission yields exit 3 and halts after the first Sonnet call. | cli-exit-code |
