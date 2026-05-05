# Mission Report — mission-smoke-01

**Run id:** smoke-20260424-203806
**Started:** 2026-04-25T03:38:07Z
**Ended:**   2026-04-25T03:39:08Z (1m 00s)
**Terminal outcome:** ok
**Exit code:** 0
**Total spend:** $0.0373
**Budget:** unlimited

---

## Summary

1 mission, 1 Sonnet spec, 0 Haiku scenarios. All passed acceptance. No invariant violations.

---

## Tier 2 — Sonnet results

| Spec | Worker | Outcome | ATs passed | Spend | Notes |
|:---|:---|:---|:---:|---:|:---|
| spec-smoke-01 | sonnet-01 | ok | 1/1 | $0.0229 | — |

### spec-smoke-01 — Add XML doc comment to EntityManager.Initialize

- Acceptance test results:
    - AT-01 OK — Mock: diff contains '/// <summary>Initializes the entity manager.</summary>'

---

## Tier 3 — Haiku results

No Haiku scenarios run.
---

## Cost summary

| Tier | Calls | Input tok | Cached read tok | Output tok | Spend |
|:---|---:|---:|---:|---:|---:|
| Sonnet | 1 | 1,240 | 28,000 | 720 | $0.0229 |
| Haiku (batched) | 5 | 4,100 | 97,500 | 1,050 | $0.0144 |
| **Total** | **6** | **5,340** | **125,500** | **1,770** | **$0.0373** |

---

## Notable events

- 03:38:07 run-started
- 03:39:08 run-completed exit=0

---

## Artifacts

- Ledger: [cost-ledger.jsonl](./cost-ledger.jsonl)
- Events: [events.jsonl](./events.jsonl)
- JSON mirror: [report.json](./report.json)
