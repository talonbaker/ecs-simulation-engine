# WP-2.0.C — BatchScheduler Cross-Spec Scenario ID Dedup Fix

**Tier:** Sonnet
**Depends on:** WP-2.0.A (cast-validate spec exists), WP-2.0.B (CostLedger flake fixed; this packet unblocks the secondary failure WP-2.0.B exposed)
**Parallel-safe with:** WP-2.2.A, WP-2.3.A, WP-2.4.A (different project: `Warden.Orchestrator` vs `APIFramework`)
**Timebox:** 45 minutes
**Budget:** $0.20

---

## Goal

Fix the `ArgumentException: An item with the same key has already been added. Key: sc-01` that `BatchScheduler.RunAsync` throws when two specs in a single run produce overlapping scenario IDs. The completion note for WP-2.0.B documents the symptom: `examples/smoke-specs/spec-smoke-01.json` and `examples/smoke-specs/cast-validate.json` both produce `scenarioBatch` entries with IDs `sc-01..sc-05`. When `RunCommand` collects both batches and passes them to `BatchScheduler.RunAsync`, the method calls `.ToDictionary(sc => sc.ScenarioId)` across the flat list and throws on the first duplicate.

The bug pre-dates Phase 2; it was masked by the IOException flake (PHASE-1-HANDOFF §6 item 5) until WP-2.0.B fixed that flake and exposed this one. Two end-to-end tests stay red on `staging` HEAD because of it (`AT01_MockRun_ExitsZeroAndWritesLedger`, `AT01b_MockRun_TenIterations_AllSucceed`); both should pass after this packet lands.

The fix: make scenario IDs unique per scenario *batch* (Sonnet) rather than per run. Two specs that both name a scenario `sc-01` must coexist; the orchestrator namespaces them by their parent ScenarioBatch's `BatchId` for all internal lookups, and submits composite identifiers to Anthropic's batch API as `custom_id` values.

---

## Reference files

- `Warden.Orchestrator/Batch/BatchScheduler.cs` — the file to fix. Read it end to end. The bug surfaces at the `haikuIdFor` `.ToDictionary(...)` call (around line 80); the same shape repeats in `parentBatchIdFor` (added by PHASE-1-HANDOFF §4.1), in `uniqueResults`, and in the `ScenarioDeduper` interface. All four lookups need composite-key treatment.
- `Warden.Orchestrator/Batch/ScenarioDeduper.cs` (or wherever the deduper lives) — confirm whether the deduper already operates per-batch or flattens across batches. If it flattens, it needs the same composite-key change.
- `Warden.Orchestrator.Tests/Batch/BatchSchedulerTests.cs` — existing test surface. Read AT-02 specifically (within-batch dedup of identical scenarios) to confirm your fix doesn't regress it.
- `Warden.Orchestrator.Tests/RunCommandEndToEndTests.cs` — `AT01_MockRun_ExitsZeroAndWritesLedger` and `AT01b_MockRun_TenIterations_AllSucceed` are the failing tests this packet must turn green. Don't modify them; the fix must make them pass as-written.
- `Warden.Anthropic/BatchRequestEntry.cs` — confirm the `custom_id` field type and any length constraint. Anthropic's published limit is 64 characters; the composite id (`<batchId>::<scenarioId>`) must fit. With current naming (`batch-smoke-01-haiku::sc-05` = 27 chars), there's plenty of headroom, but defensively validate at construction.
- `examples/smoke-specs/spec-smoke-01.json`, `examples/smoke-specs/cast-validate.json` — the colliding test cases. Don't modify.
- `docs/c2-infrastructure/PHASE-1-HANDOFF.md` — §4.1 (the post-closure pass) is essential context for the `parentBatchIdFor` map this packet must update; §6 item 5 (Phase-2 backlog) names the original IOException flake whose fix exposed this bug.
- `docs/c2-infrastructure/work-packets/_completed/WP-2.0.B.md` — the prior Sonnet's diagnosis and verbatim stack trace. The "Followups" section names this exact fix.

## Non-goals

- Do **not** change the `OpusSpecPacket` schema, the `SonnetResult.scenarioBatch.batchId` shape, the `HaikuResult` schema, or any wire format. The composite-key change is purely internal to `BatchScheduler` plus the Anthropic `custom_id` round-trip.
- Do **not** modify `examples/smoke-specs/*.json`. They are the reproduction case, not the fix surface.
- Do **not** rename or restructure existing scenario IDs. Sonnets emit `sc-01..sc-25`; that pattern stays. The composite key is internal namespacing.
- Do **not** modify `RunCommand.cs`, `SonnetDispatcher.cs`, the report aggregator, or the cost ledger. Scope is `BatchScheduler.cs` plus its direct test partner.
- Do **not** reduce or change `ScenarioDeduper`'s within-batch semantics. AT-02 of WP-07 (identical scenarios within ONE batch produce one entry, multiple results) must still hold.
- Do **not** introduce a NuGet dependency.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing.

---

## Design notes

### Composite keys

Internally, every per-scenario lookup in `BatchScheduler.RunAsync` uses `(string BatchId, string ScenarioId)` as its key, never `ScenarioId` alone. Concretely:

```csharp
private readonly record struct ScenarioKey(string BatchId, string ScenarioId);

// haikuIdFor: (BatchId, ScenarioId) → "haiku-NN"
// parentBatchIdFor: (BatchId, ScenarioId) → BatchId  ← becomes redundant; eliminate it
// uniqueResults: (BatchId, ScenarioId) → HaikuResult
// dupToOrig: (BatchId, ScenarioId) → (BatchId, ScenarioId)
```

Note `parentBatchIdFor` (added in PHASE-1-HANDOFF §4.1) becomes redundant once the lookup key is composite — the BatchId is already part of every key. Delete the map entirely; replace its usage with `key.BatchId` at call sites.

### Anthropic custom_id format

The `custom_id` field on Anthropic batch entries has a 64-character limit (verify in `BatchRequestEntry.cs` or its docs). Encode the composite as:

```
<batchId>::<scenarioId>
```

Two-colon separator because single colons appear in some user batchId conventions. With current naming (`batch-smoke-01-haiku::sc-05` = 27 chars) we're well under the limit, but validate at construction:

```csharp
if (composite.Length > 64)
    throw new InvalidOperationException(
        $"Composite custom_id '{composite}' exceeds Anthropic's 64-char limit. " +
        $"Shorten the scenarioBatch.batchId in the upstream Sonnet result.");
```

This is a fail-closed exit: if upstream produces a too-long batchId, the orchestrator refuses to submit rather than truncating silently.

### Parsing on the way back

Anthropic's batch result stream returns each entry's `custom_id` verbatim. Parse it back to `(batchId, scenarioId)` with a deterministic split on `::`:

```csharp
private static ScenarioKey ParseCompositeCustomId(string customId)
{
    var idx = customId.LastIndexOf("::", StringComparison.Ordinal);
    if (idx < 0)
        throw new InvalidOperationException(
            $"Malformed custom_id '{customId}' — expected '<batchId>::<scenarioId>'.");
    return new ScenarioKey(customId[..idx], customId[(idx + 2)..]);
}
```

Use `LastIndexOf` (not `IndexOf`) so a batchId containing `::` doesn't break the parse. This is defensive — current naming forbids `::` in batchIds, but the schema doesn't, so survive it.

### Updates to `ParseSucceeded` / `BlockedEntry`

`ParseSucceeded(succeeded, batchId, haikuId, scenarioId)` already accepts `batchId` — its signature is fine. The composite-key change happens at the call site (the result-stream loop), where `entry.CustomId` is parsed and `(batchId, scenarioId)` passed in.

### What about ScenarioDeduper?

If `ScenarioDeduper.Deduplicate` operates on `IList<Scenario>` and produces `(unique scenarios, dup → orig map)`, both halves of its output need to use the composite key. Two options:

1. **Extend the deduper signature** to take `IList<(string BatchId, Scenario)>`. Cleanest; deduper now knows about batches.
2. **Wrap calls with adapters** that encode/decode the composite key as a synthetic prefix on a `Scenario` clone. Less invasive but uglier.

Pick option 1 if the deduper is small (likely — it's content-hashing). Pick option 2 if extending the signature ripples through more than two files. The Sonnet decides based on the actual code.

### A simpler internal redesign worth considering

The `RunAsync` method is starting to accumulate parallel dictionaries (`haikuIdFor`, `parentBatchIdFor`, `uniqueResults`, `dupToOrig`). They're all keyed by the same scenario identity. A small helper record could collapse them:

```csharp
private sealed record ScenarioContext(
    string BatchId,
    Scenario Scenario,
    string HaikuId);

// One list, indexed by composite key when needed:
List<ScenarioContext> contexts = batches
    .SelectMany(b => b.Scenarios.Select(s => new { Batch = b, Scenario = s }))
    .Select((x, i) => new ScenarioContext(x.Batch.BatchId, x.Scenario, $"haiku-{i + 1:D2}"))
    .ToList();

Dictionary<ScenarioKey, ScenarioContext> contextByKey =
    contexts.ToDictionary(c => new ScenarioKey(c.BatchId, c.Scenario.ScenarioId));
```

This is a cleanup the Sonnet may or may not pursue. The acceptance tests don't require it; the bug fix is the priority. If touching it adds more than 30 lines of churn, defer to a Phase-2 polish packet.

### Tests to add

Two unit tests in `BatchSchedulerTests.cs`:

1. **`AT_BS_X_TwoBatches_SameScenarioIds_BothDispatchAndReturn`**: build two `ScenarioBatch` instances with overlapping scenario IDs (`sc-01..sc-03` in each, eight scenarios total); pass both to `RunAsync`; assert (a) no exception thrown, (b) the result list has 6 items, (c) each batch's results retain the correct `parentBatchId` distinguishing them, (d) `haikuId` is uniquely assigned.

2. **`AT_BS_X_CustomIdRoundTrip_RecoversBatchAndScenarioIds`**: a focused test of the composite-id parser. Encode `("batch-foo", "sc-01")` → submit → fake Anthropic returns it verbatim → parse → assert tuple round-trips. Also: parse rejects malformed `custom_id` lacking `::`.

The end-to-end fix is verified by the existing failing tests:
- `RunCommandEndToEndTests.AT01_MockRun_ExitsZeroAndWritesLedger` — should pass after fix.
- `RunCommandEndToEndTests.AT01b_MockRun_TenIterations_AllSucceed` — should pass after fix.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Orchestrator/Batch/BatchScheduler.cs` (modified) | Composite-key the four lookup dictionaries; encode/decode composite `custom_id`; delete the now-redundant `parentBatchIdFor` map; defensive 64-char validation at submission. |
| code | `Warden.Orchestrator/Batch/ScenarioDeduper.cs` (modified, if needed) | If the deduper currently keys by `ScenarioId` alone, extend its signature to take `(BatchId, Scenario)` pairs and operate on `(BatchId, ScenarioId)` composite keys. If it already operates per-batch, no change. |
| code | `Warden.Orchestrator.Tests/Batch/BatchSchedulerTests.cs` (modified) | Add the two new tests per Design notes. Existing tests must stay green. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-2.0.C.md` | Completion note. Standard template. Confirm both AT01 and AT01b now pass; report whether `parentBatchIdFor` was eliminated and whether the optional `ScenarioContext` cleanup was pursued; note any platform observations. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | Existing `Warden.Orchestrator.Tests.RunCommandEndToEndTests.AT01_MockRun_ExitsZeroAndWritesLedger` passes (the previously-failing test, unmodified, now green). | unit-test |
| AT-02 | Existing `Warden.Orchestrator.Tests.RunCommandEndToEndTests.AT01b_MockRun_TenIterations_AllSucceed` passes — 10 iterations of the AT01 body in one `[Fact]`, all succeed. | unit-test |
| AT-03 | New `AT_BS_X_TwoBatches_SameScenarioIds_BothDispatchAndReturn` passes — two batches with overlapping `sc-NN` ids both dispatch; results list has all entries; per-result `parentBatchId` correctly distinguishes them; `haikuId` assignments are unique. | unit-test |
| AT-04 | New `AT_BS_X_CustomIdRoundTrip_RecoversBatchAndScenarioIds` passes — composite `custom_id` encode/decode round-trips; malformed values throw a clean diagnostic exception. | unit-test |
| AT-05 | Existing `BatchSchedulerTests` (all WP-07 acceptance tests) stay green — within-batch dedup behaviour preserved (AT-02 of WP-07: identical scenarios within ONE batch produce one entry but multiple results). | unit-test |
| AT-06 | All other Warden.Orchestrator.Tests stay green — 122+ existing non-flaky tests + the new ones. | unit-test |
| AT-07 | `dotnet test ECSSimulation.sln` (no filter) — every test passes, 0 failures, 0 skipped. | build + unit-test |
| AT-08 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-09 | Composite `custom_id` length validation: a Sonnet-emitted `scenarioBatch.batchId` of 60 characters with a scenario id of `sc-01` produces a clean `InvalidOperationException` at submission ( `60 + 2 + 5 = 67 > 64`). | unit-test |

---

## Followups (not in scope)

- **Length budget for batchId in role frame.** The Anthropic 64-char limit on `custom_id` minus the `::` separator and the longest realistic scenarioId (`sc-25`) gives a max batchId length of 56 chars. Document that constraint in the Sonnet's role frame so models know not to emit absurd batchIds. Tiny doc-only follow-up.
- **`ScenarioContext` record cleanup** — if the Sonnet didn't pursue the four-dictionary collapse during this packet, a polish packet later can do it.
- **Cross-batch within-content dedup** — if the deduper currently flattens-then-hashes, two specs that produce semantically identical scenarios will share one Anthropic batch entry; this is *desirable* (cost saving) but the per-result reattachment must distinguish them. Verify the post-fix behaviour preserves this; if not, file a follow-up.
