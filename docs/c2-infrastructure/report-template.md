# Mission Report — mission-smoke

**Run id:** 20260423-142300-smoke
**Started:** 2026-04-23T14:23:00Z
**Ended:**   2026-04-23T14:26:08Z (3m 08s)
**Terminal outcome:** ok
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
| spec-smoke-01 | sonnet-01 | ok | 3/3 | $0.0610 | — |

### spec-smoke-01 — Add code comment to EntityManager

- Worktree: `./runs/20260423-142300-smoke/sonnet-01/worktree/`
- Diff summary: 1 files modified, 3 lines added
- Acceptance test results:
    - AT-01 ✓
    - AT-02 ✓
    - AT-03 ✓

---

## Tier 3 — Haiku results

Grouped by parent spec.

### Under spec-smoke-01 (5 scenarios)

| Scenario | Seed | Outcome | Assertions | Duration (gs) | Spend |
|:---|---:|:---|:---:|---:|---:|
| sc-01 | 42 | ok | 3/3 | 3600 | $0.0031 |
| sc-02 | 99 | ok | 3/3 | 3600 | $0.0030 |
| sc-03 | 101 | ok | 3/3 | 3600 | $0.0031 |
| sc-04 | 1024 | ok | 3/3 | 3600 | $0.0030 |
| sc-05 | 65535 | ok | 3/3 | 3600 | $0.0031 |

Aggregate key metrics across scenarios:

| Metric | Mean | Min | Max | Stddev |
|:---|---:|---:|---:|---:|
| final satiation | 42.1 | 38.6 | 46.3 | 2.8 |
| violation count | 0.0 | 0.0 | 0.0 | 0.0 |

---

## Cost summary

| Tier | Calls | Input tok | Cached read tok | Output tok | Spend |
|:---|---:|---:|---:|---:|---:|
| Sonnet | 1 | 2,500 | 32,000 | 1,500 | $0.0610 |
| Haiku (batched) | 5 | 20,000 | 150,000 | 4,000 | $0.0162 |
| **Total** | **6** | **22,500** | **182,000** | **5,500** | **$0.0772** |

---

## Notable events

- 14:23:00 run-started
- 14:26:08 run-completed exit=0

---

## Artifacts

- Ledger: [cost-ledger.jsonl](./cost-ledger.jsonl)
- Events: [events.jsonl](./events.jsonl)
- JSON mirror: [report.json](./report.json)
