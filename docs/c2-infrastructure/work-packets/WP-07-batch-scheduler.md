# WP-07 — Batch Scheduler (Haiku Dispatch via Message Batches API)

**Tier:** Sonnet
**Depends on:** WP-05, WP-06
**Timebox:** 75 minutes
**Budget:** $0.35

---

## Goal

Submit Haiku scenarios as a single Message Batches API job, poll for completion, stream results back, and deduplicate identical scenarios within and across missions. The batch scheduler owns the 50% Haiku discount on Goal G2.

---

## Reference files

- `docs/c2-infrastructure/00-SRD.md` §2 Pillar B step 6, Pillar D.2
- `Warden.Anthropic/AnthropicClient.cs` (from WP-05)
- `Warden.Orchestrator/Cache/PromptCacheManager.cs` (from WP-06)
- `Warden.Contracts/Handshake/ScenarioBatch.cs` (from WP-02)
- `Warden.Contracts/Handshake/HaikuResult.cs` (from WP-02)

## Non-goals

- Do not invent a second concurrency primitive here. The batch scheduler holds zero long-lived HTTP connections; there is no `SemaphoreSlim` inside it.
- Do not attempt to preserve batch-submission order in results — results arrive keyed by `custom_id`. Callers rely on the key, not the order.
- Do not retry inside this packet. The orchestrator owns retry policy (WP-09).

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Orchestrator/Batch/BatchScheduler.cs` | Public entry point. |
| code | `Warden.Orchestrator/Batch/BatchSubmission.cs` | Internal record tracking an in-flight batch. |
| code | `Warden.Orchestrator/Batch/ScenarioDeduper.cs` | Hashes each scenario by `(seed, configDelta, commands, assertions)` and collapses duplicates before submission. A dedupe hit is a logged cost saving, not an error. |
| code | `Warden.Orchestrator/Batch/BatchPoller.cs` | Timer-based polling. Default interval 60s. Exponential backoff to 5min on consecutive no-change polls. |
| code | `Warden.Orchestrator.Tests/Batch/BatchSchedulerTests.cs` | See acceptance. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-07.md` | Completion note. Include: average poll-cycles-to-completion on the mock batch. |

### Public surface

```csharp
public sealed class BatchScheduler
{
    public BatchScheduler(AnthropicClient client, PromptCacheManager cache, ChainOfThoughtStore cot, CostLedger ledger, ILogger<BatchScheduler> log);

    public async Task<IReadOnlyList<HaikuResult>> RunAsync(
        string runId,
        IReadOnlyList<ScenarioBatch> batches,
        CancellationToken ct);
}
```

`RunAsync` does the following, in order:

1. Flatten all scenarios from all input `ScenarioBatch` objects into a single list (≤ 25, enforced with a hard check). If >25, throw — the orchestrator should have enforced this upstream.
2. Dedupe by content hash. Persist a dedupe ledger line per suppressed scenario.
3. Build one `BatchRequest` with one entry per unique scenario. Each entry uses `PromptCacheManager.BuildRequest` to assemble its prompt, passing `expectedTotalLatency = TimeSpan.FromMinutes(30)` (so slab 1 uses the 1h TTL).
4. Submit via `AnthropicClient.CreateBatchAsync`. Persist the batch id to the chain-of-thought store.
5. Poll `GetBatchAsync` every 60s. On `status == "ended"`, call `StreamBatchResultsAsync`.
6. For each result entry: validate against `haiku-result.schema.json`, reattach to the suppressed duplicates (so every original scenario id gets a result), append to the output list, and record token usage.
7. Write the cost ledger line per result.
8. Return the flat list. Ordering matches input scenario ids.

Scenarios that failed to parse as valid `HaikuResult` JSON do not get a retry. They get an `outcome = "blocked"` stub with `blockReason = "tool-error"` and move on.

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `RunAsync` submits exactly one batch for 25 unique scenarios. | unit-test |
| AT-02 | Identical scenarios produce one batch entry and N results that reference the same underlying response. (Assert via mock client call count.) | unit-test |
| AT-03 | A >25 scenario input throws `InvalidOperationException` with a clear message. | unit-test |
| AT-04 | Poll interval is 60s by default and backs off to 5min after 10 consecutive no-change polls. | unit-test |
| AT-05 | Cancellation via `CancellationToken` cancels the poll loop within 2 seconds. | unit-test |
| AT-06 | Malformed Haiku JSON in a batch result becomes a `blocked` `HaikuResult`, not an exception. | unit-test |
| AT-07 | The cost ledger receives one line per **unique** Haiku call (not per suppressed duplicate). | unit-test |
| AT-08 | All paths write to the chain-of-thought store — a 5-scenario run produces `haiku-01/`…`haiku-05/` directories with `scenario.json` and `result.json`. | unit-test |
