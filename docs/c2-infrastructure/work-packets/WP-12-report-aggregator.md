# WP-12 — Report Aggregator (Readable End-of-Run Summaries)

**Tier:** Sonnet
**Depends on:** WP-08, WP-09, WP-10, WP-11
**Timebox:** 60 minutes
**Budget:** $0.25

---

## Goal

Turn the chain-of-thought directory into a single-page Markdown report a human can skim in 90 seconds, plus a machine-parseable JSON mirror. Goal G5 — readable reports before/during/after — lives or dies on this packet.

---

## Reference files

- `docs/c2-infrastructure/00-SRD.md` §4.4
- Every deliverable from WP-07 through WP-11.

## Non-goals

- No HTML, no PDF, no charts. Markdown + JSON only.
- No AI-generated summary text. The report is composed of facts pulled from the run root. No extra tokens spent.
- No live streaming. Reports are produced at end-of-run and optionally on `--report-now` signals.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Orchestrator/Reports/ReportAggregator.cs` | `public Report Build(string runId)` returns the in-memory model; `public void Emit(Report r, string rootDir)` writes the two files. |
| code | `Warden.Orchestrator/Reports/Report.cs` | Model: header, per-spec sections, cost summary, notable events. |
| code | `Warden.Orchestrator/Reports/MarkdownReportWriter.cs` | Emits `report.md`. |
| code | `Warden.Orchestrator/Reports/JsonReportWriter.cs` | Emits `report.json` using `Warden.Contracts.JsonOptions`. |
| code | `docs/c2-infrastructure/report-template.md` | The golden template reviewers can diff against. |
| code | `Warden.Orchestrator.Tests/Reports/ReportAggregatorTests.cs` | See acceptance. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-12.md` | Completion note. Paste the first emitted smoke-mission report into this note. |

---

## Report layout (authoritative)

```
# Mission Report — <missionId>

**Run id:** 20260423-142300-smoke
**Started:** 2026-04-23T14:23:00Z
**Ended:**   2026-04-23T14:26:08Z (3m 08s)
**Terminal outcome:** ok | failed | blocked | budget-exceeded
**Exit code:** 0
**Total spend:** $0.0772
**Budget:** $1.00 (92% remaining)

---

## Summary

1 mission, 1 Sonnet spec, 5 Haiku scenarios. All passed acceptance. No invariant violations.

---

## Tier 2 — Sonnet results

| Spec | Worker | Outcome | ATs passed | Spend | Notes |
|:---|:---|:---|:---:|---:|:---|
| spec-smoke-01 | sonnet-01 | ok | 3/3 | $0.061 | — |

### spec-smoke-01 — Add code comment to EntityManager

- Worktree: `./runs/20260423-142300-smoke/sonnet-01/worktree/`
- Diff summary: 1 file modified, 3 lines added
- Acceptance test results:
    - AT-01 ✓ build green
    - AT-02 ✓ comment present at expected line
    - AT-03 ✓ no existing tests fail

---

## Tier 3 — Haiku results

Grouped by parent spec.

### Under spec-smoke-01 (5 scenarios)

| Scenario | Seed | Outcome | Assertions | Duration (gs) | Spend |
|:---|---:|:---|:---:|---:|---:|
| sc-01 | 42   | ok | 3/3 | 3600 | $0.0031 |
| sc-02 | 99   | ok | 3/3 | 3600 | $0.0030 |
| sc-03 | 101  | ok | 3/3 | 3600 | $0.0031 |
| sc-04 | 1024 | ok | 3/3 | 3600 | $0.0030 |
| sc-05 | 65535| ok | 3/3 | 3600 | $0.0031 |

Aggregate key metrics across scenarios:

| Metric | Mean | Min | Max | Stddev |
|:---|---:|---:|---:|---:|
| final satiation | 42.1 | 38.6 | 46.3 | 2.8 |
| violation count | 0    | 0    | 0   | 0   |

---

## Cost summary

| Tier | Calls | Input tok | Cached read tok | Output tok | Spend |
|:---|---:|---:|---:|---:|---:|
| Sonnet | 1 | 2,500 | 32,000 | 1,500 | $0.061 |
| Haiku (batched) | 5 | 20,000 | 150,000 | 4,000 | $0.016 |
| **Total** | **6** | **22,500** | **182,000** | **5,500** | **$0.077** |

---

## Notable events

- 14:23:02 run-started
- 14:23:09 sonnet-01 completed (ok)
- 14:23:10 batch-submitted (5 scenarios)
- 14:24:12 batch-ended
- 14:26:08 run-completed

---

## Artifacts

- Ledger: [cost-ledger.jsonl](./cost-ledger.jsonl)
- Events: [events.jsonl](./events.jsonl)
- JSON mirror: [report.json](./report.json)
```

This layout is fixed. Do not change section order, do not add "insights" that are not directly derived from the run data. The report's value is **predictability** — a human can learn to skim it once and use that muscle memory for every future mission.

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | Running `run --mission examples/smoke-mission.md --mock-anthropic` produces a `report.md` that matches `docs/c2-infrastructure/report-template.md` structurally (same section headings, same table columns). | unit-test |
| AT-02 | Totals in the cost summary equal the sum of lines in `cost-ledger.jsonl` (within 1¢ rounding). | unit-test |
| AT-03 | A run with a `failed` outcome produces a report whose terminal outcome is `failed` and whose Notes column cites the failing acceptance test. | unit-test |
| AT-04 | A `blocked` mid-run produces a partial report (only the completed workers are listed) with a prominent "Run terminated early" header. | unit-test |
| AT-05 | `report.json` round-trips to the same `Report` in-memory model. | unit-test |
| AT-06 | Report emission adds less than 200ms to total run wall-clock time. | unit-test |
| AT-07 | No section of the report contains token-count data that is not backed by a ledger entry — every number has a source. | manual-review |
