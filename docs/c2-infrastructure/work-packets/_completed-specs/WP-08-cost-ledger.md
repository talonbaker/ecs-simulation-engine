# WP-08 — Cost Ledger and Budget Enforcement

**Tier:** Sonnet
**Depends on:** WP-05
**Timebox:** 45 minutes
**Budget:** $0.20

---

## Goal

Record every API call's token usage and USD cost to a crash-safe, append-only JSONL ledger, and enforce per-run USD budgets with a fail-closed halt. Make spend visible live. Make it impossible to overspend silently.

---

## Reference files

- `docs/c2-infrastructure/02-cost-model.md` (all of it)
- `Warden.Anthropic/CostRates.cs` (from WP-05)
- `Warden.Anthropic/MessageResponse.cs` (from WP-05)

## Non-goals

- Do not attempt to estimate cost before a call completes. Pre-call estimates belong in reports, not in the ledger.
- Do not track wall-clock time — token counts from the API response are the source of truth.
- No SQLite, no database. Flat JSONL, fsync'd, is the record.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Orchestrator/Persistence/CostLedger.cs` | Thread-safe, append-only JSONL writer. |
| code | `Warden.Orchestrator/Persistence/LedgerEntry.cs` | Record shape from `00-SRD.md` §2 Pillar D.5. |
| code | `Warden.Orchestrator/Persistence/BudgetGuard.cs` | Holds a running sum. `public BudgetVerdict Check(decimal projectedCostUsd)`. Called before every dispatch. |
| code | `Warden.Orchestrator/Persistence/BudgetVerdict.cs` | `record BudgetVerdict(bool CanProceed, decimal SpentUsd, decimal RemainingUsd, string? HaltReason)`. |
| code | `Warden.Orchestrator/Persistence/CostCalculator.cs` | Pure function. `public decimal CalculateUsd(ModelId model, TokenUsage usage, bool isBatch)`. |
| code | `Warden.Orchestrator.Tests/Persistence/CostLedgerTests.cs` | See acceptance. |
| code | `Warden.Orchestrator.Tests/Persistence/BudgetGuardTests.cs` | See acceptance. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-08.md` | Completion note including the decimals used in each calculation path. |

---

## Design notes

**Thread safety.** The ledger is written concurrently from the Sonnet dispatcher and the batch scheduler. Use a single `SemaphoreSlim(1)` around the write. Serial writes are acceptable because JSONL writes are ~1ms each and bursts are bounded by the 30 concurrent calls per mission.

**Crash safety.** Open the file with `FileShare.Read`, write one line, call `stream.Flush(true)` (fsync). Never buffer more than one line. A mission that crashes mid-run leaves a partial but parseable ledger.

**`decimal`, not `double`.** Token counts are integers; per-token rates are fractional. Use `decimal` for USD everywhere. Converting rates from documented per-Mtok prices to per-token fractions introduces rounding — do this once, statically, in `CostRates.cs`, and cache the constants to 10 decimal places.

**Batch cost math.** When `isBatch == true`, the rate is `baseRate * 0.5m`. Cache write and cache read discounts compose with batch: we model batch-api cached reads as `input * 0.10 * 0.5 = 0.05x`. **Verify this against Anthropic's current policy at implementation time.** If the policy is "batch OR cache, not both," update `CostCalculator` to take the more favourable of the two instead of composing.

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `CostCalculator.CalculateUsd` for a Sonnet call with 32k cached-read + 2.5k input + 1.5k output produces exactly the number from `02-cost-model.md` §3. | unit-test |
| AT-02 | `CostLedger` writes one JSONL line per `LedgerEntry`. `File.ReadAllLines` round-trips cleanly. | unit-test |
| AT-03 | Two concurrent writers produce no interleaved lines (stress test, 1000 writes across 10 tasks). | unit-test |
| AT-04 | `BudgetGuard` reports `CanProceed = false, HaltReason = "budget-exceeded"` when running sum + projected cost > budget. | unit-test |
| AT-05 | `BudgetGuard` with `--budget-usd 1.00` allows 5 Sonnet calls at ~$0.06 each but blocks the 25th. | unit-test |
| AT-06 | Crashing mid-write (simulated via `Stream.Dispose()` before flush) leaves a partial ledger that `File.ReadAllLines` can still parse up to the last complete line. | unit-test |
| AT-07 | `CostCalculator.CalculateUsd` returns `decimal.Zero` when all token counts are zero (safety net for dry-run mode). | unit-test |
