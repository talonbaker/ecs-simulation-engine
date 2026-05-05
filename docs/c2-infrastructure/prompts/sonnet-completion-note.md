# Sonnet Completion Note Template

Every executed Work Packet produces a completion note at `docs/c2-infrastructure/work-packets/_completed/WP-NN.md`. This is the template. Copy it, rename it, fill every section. Empty sections are kept (write "none" or "n/a") so the audit trail is uniform.

---

## Template (copy from `# WP-NN` to the bottom)

```markdown
# WP-NN — <slug> — Completion Note

**Executed by:** sonnet-<n>
**Branch:** <branch-name>
**Started:** <iso-utc-timestamp>
**Ended:** <iso-utc-timestamp>
**Outcome:** ok | failed | blocked

---

## Summary (≤ 200 words)

What you did, in plain prose. Not a list of files; the diff is its own record. Mention any judgement calls — places where the packet was ambiguous and you chose a path. Future packets and reviewers learn from these notes.

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | OK/FAIL |  |
| AT-02 | OK/FAIL |  |
| AT-03 | OK/FAIL |  |
| ... |  |  |

Every AT in the packet must appear here. If an AT is `n/a` for any reason, mark it `n/a` and explain in Notes.

## Files added

(One per line, repo-relative path.)

## Files modified

(One per line, with a one-line reason each.)

## Diff stats

`<n>` files changed, `<n>` insertions(+), `<n>` deletions(-).

(Get this from `git diff --stat main...HEAD` on your branch.)

## Followups

Anything you noticed that is *out of scope* for this packet but worth a future packet's attention. One bullet per item, ≤ 15 words each. Empty list (`(none)`) is fine and common.

## If outcome ≠ ok: blocking reason

| Field | Value |
|:---|:---|
| `blockReason` | (use the SRD §4.1 enum: ambiguous-spec / build-failed / tool-error / exception / tests-red / schema-mismatch-on-own-output / budget-exceeded / timebox-exceeded) |
| `blockingArtifact` | repo-relative path to the log/file showing the failure |
| `humanMessage` | one sentence the operator can act on without reading code |

(Omit this section if outcome == ok.)

## Token usage (optional, for cost-tuning)

| Field | Value |
|:---|---:|
| Input tokens | |
| Cache-read tokens | |
| Cache-write tokens | |
| Output tokens | |
| Estimated USD | |

If you don't have these numbers, omit the section. Do not invent them.
```

---

## Why every section matters

- **Summary** is what an Opus brief in 6 months reads to learn from your judgement calls. Skipping it makes future missions costlier because future Opuses re-derive what you already figured out.
- **Acceptance tests** prove the packet's claims. A pass with no AT table is not a pass.
- **Files added/modified** are how reviewers verify scope discipline — packets that touched files they shouldn't are caught here.
- **Followups** is how scope discipline coexists with iterative improvement. You spotted something but it isn't your job to fix? Write it here. A future packet picks it up.
- **Blocking reason** with structured fields (not free prose) is what makes a `blocked` outcome actionable instead of mysterious. The operator should not have to read your code to learn what you tried.
