# Cost Model — ROI Math for the 1-5-25 Workflow

**Goal G2 is cheap.** This document turns that into numbers. Every figure here is pinned to publicly listed Anthropic pricing as of Q2 2026. Prices move; when they do, update this file and regenerate the ledger's cost coefficients via `WP-08`.

**Read this before you argue about an architectural choice on cost grounds.** It shows why caching is non-optional, why batching Haikus is non-optional, and why fail-closed workers save more money than any other single lever.

---

## 1. List prices used in this document

| Model | Input / Mtok | Output / Mtok | Cache write / Mtok | Cache read / Mtok | Batch input / Mtok | Batch output / Mtok |
|:---|---:|---:|---:|---:|---:|---:|
| `claude-opus-4-6` | $15 | $75 | $18.75 | $1.50 | $7.50 | $37.50 |
| `claude-sonnet-4-6` | $3 | $15 | $3.75 | $0.30 | $1.50 | $7.50 |
| `claude-haiku-4-5-20251001` | $1 | $5 | $1.25 | $0.10 | $0.50 | $2.50 |

Cache write is 1.25× the input rate. Cache read is 0.10× the input rate. Batch API is 50% of the interactive rate (input, output, and cache reads apply the same proportional discount when batch + cache are combined, per Anthropic's policy). **Confirm these numbers at `WP-08` implementation time and pin them in `CostRates.cs`.**

---

## 2. Baseline: the naïve multi-agent cost (what we are avoiding)

Assume:

- Shared context per call: ~30,000 tokens of engine docs.
- Sonnet per-call variable input: ~2,500 tokens (SpecPacket + recent worktree).
- Sonnet per-call output: ~1,500 tokens (diff + result JSON).
- Haiku per-call variable input: ~4,000 tokens (scenario + telemetry digest).
- Haiku per-call output: ~800 tokens (result JSON).

**Naïve (no caching, no batching, all interactive):**

```
1× Opus   (human invokes; out of scope for the orchestrator's budget)
5× Sonnet input   = 5 × 32,500  = 162,500 tokens × $3   /Mtok =  $0.488
5× Sonnet output  = 5 × 1,500   =   7,500 tokens × $15  /Mtok =  $0.113
25× Haiku input   = 25 × 34,000 = 850,000 tokens × $1   /Mtok =  $0.850
25× Haiku output  = 25 × 800    =  20,000 tokens × $5   /Mtok =  $0.100
                                                              ────────
                                           Naïve mission cost: $1.551
```

**Per mission.** One mission a day, 30 days → ~$46.50/month. Ten missions a day → $465. This scales badly.

---

## 3. With prompt caching turned on (Sonnet layer)

The 30,000 shared tokens are cached once per 5-minute window.

```
1× Sonnet cache write = 30,000 × $3.75 /Mtok =  $0.1125
4× Sonnet cache read  = 4 × 30,000 × $0.30 /Mtok = $0.036
5× Sonnet variable input  = 5 × 2,500 × $3 /Mtok = $0.0375
5× Sonnet output          = 5 × 1,500 × $15 /Mtok = $0.1125
                                                   ─────────
                              Sonnet tier with cache: $0.2985
```

**Versus naïve Sonnet tier ($0.488 + $0.113 = $0.601).** Saving: $0.30 per mission just at the Sonnet tier. ~50% cut.

---

## 4. With Message Batches API (Haiku layer)

Batching cuts input and output in half. We assume caching does not apply to the batch API's input in this baseline — treat it as pure half-off. (If Anthropic allows combining cache + batch by the time `WP-07` lands, update this number downward.)

```
25× Haiku input   = 850,000 × $0.50 /Mtok = $0.425
25× Haiku output  =  20,000 × $2.50 /Mtok = $0.050
                                            ──────
                     Haiku tier batched:   $0.475
```

**Versus naïve Haiku tier ($0.950).** Saving: $0.475 per mission. 50% cut.

---

## 5. With both levers, per-mission total

```
Sonnet tier (cached):     $0.2985
Haiku tier (batched):     $0.4750
                         ────────
Total optimised mission:  $0.7735
```

**Versus naïve ($1.551).** Saving: $0.778, or 50.1%. Ten missions per day: $7.74 vs $15.51. A month of heavy usage: $232 vs $465.

---

## 6. The sleeper lever: fail-closed workers

The numbers above assume **all workers succeed on the first try**. In practice, multi-agent systems without fail-closed discipline burn far more tokens than list prices suggest, because:

- A confused worker that retries its own failure three times costs 3× its list price.
- A Sonnet that spawns a "helper" Sonnet doubles its token footprint before writing a single line.
- A Haiku that edits `SimConfig.json` to work around an assertion kicks off a cascade of re-verification calls.

Real-world multi-agent cost overruns frequently run 2–5× the naïve math. The fail-closed rule is what keeps this project at the list-price baseline. It is the highest-leverage cost lever we have, and it costs zero to implement because it is a **policy**, not a dependency.

**Budget guidance:** assume 1.2× the optimised cost as a reasonable upper bound. A mission that looks like $0.77 on paper should be budgeted at ~$1.00.

---

## 7. Worked examples by mission size

| Mission profile | Sonnets | Haikus | Optimised cost | Budget rec. |
|:---|:---:|:---:|---:|---:|
| Tiny (config tuning only) | 1 | 5 | ~$0.11 | $0.15 |
| Small (one new system) | 2 | 10 | ~$0.26 | $0.35 |
| Standard mission | 5 | 25 | ~$0.77 | $1.00 |
| Heavy (five systems + big balance pass) | 5 | 25 × 3 cycles | ~$2.20 | $3.00 |

The orchestrator's `--budget-usd` flag defaults to the **Budget rec.** column for each mission profile, selected by `--profile`. Exceeding budget triggers the fail-closed halt.

---

## 8. What is not counted in these numbers

- **Opus calls.** You invoke Opus as a human at the top. That token spend is outside the orchestrator's ledger. Track it separately if it matters.
- **Retries.** Polly retries on 429/5xx log to the ledger but are not pre-budgeted. Rate is typically <1% in practice.
- **Telemetry file I/O.** Free. Lives on disk.
- **`ECSCli ai replay` runtime.** Free (CPU only, no API).

If the ledger shows a run exceeding its budget, 99% of the time the cause is either (a) a fail-closed rule was skipped, (b) prompt caching missed its window (cold start across >5 min boundary), or (c) the prompt assembly code put variable content into a cached slab by mistake. `WP-06` includes regression tests for (b) and (c).

---

## 9. Update protocol

When Anthropic's pricing page changes:

1. Update the table in §1.
2. Run `dotnet test --filter CostRatesTests`.
3. If any test fails, the coefficients in `Warden.Anthropic/CostRates.cs` are out of date — fix and re-run.
4. Re-generate the worked examples in §2–7 by running `Warden.Orchestrator cost-model --emit docs/c2-infrastructure/02-cost-model.md` (this tooling lives in `WP-08`).

Cost math is a living artifact, not a one-time estimate.
