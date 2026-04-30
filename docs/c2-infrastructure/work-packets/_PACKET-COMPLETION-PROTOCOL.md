# Packet Completion Protocol — Reference

> **Audience:** Future Opus authors of work packets.
> **Purpose:** The "Completion protocol" footer is **inlined verbatim** into every packet so Sonnet executors have a single self-contained source of truth. This file is the canonical text. **Do not reference this file from packet bodies** — copy the appropriate variant in.
> **Authority:** Established 2026-04-30 by Opus General + Talon. Living document.

---

## Why this protocol exists

Sonnet executors must finish a packet without further instruction from Talon. Every packet declares:

1. **Whether visual verification is needed** in Unity Editor before PR (Track 2) or whether passing tests is sufficient (Track 1).
2. **Cost envelope** — orchestrator dispatches must stay within the 1-5-25 Claude army's per-packet budget ($0.50–$1.20).
3. **Self-cleanup on merge** — the packet spec file is deleted on merge unless a still-pending packet depends on it. Keeps the active `work-packets/` directory clean as the project moves forward.

---

## Variant A — Track 1 packet (engine, headless)

Inline this footer in any packet that touches engine code only and is fully verifiable via `dotnet test`:

```markdown
---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: NOT NEEDED

This is a Track 1 (engine) packet. All verification is handled by the xUnit test suite. Once `dotnet test` returns green for `APIFramework.Tests` (and any other affected test project), the packet is ready to push and PR. **No Unity Editor steps required.**

The Sonnet executor's pipeline:

1. Implement the spec.
2. Add or update xUnit tests to cover all acceptance criteria.
3. Run `dotnet test` from the repo root. Must be green.
4. Run `dotnet build` to confirm no warnings introduced.
5. Stage all changes including the self-cleanup deletion (see below).
6. Commit on the worktree's feature branch.
7. Push the branch and open a PR against `staging`.
8. Stop. Do **not** merge. Talon merges after review.

If a test fails or compile fails, fix the underlying cause. Do **not** skip tests, do **not** mark expected-failures, do **not** push a red branch.

### Cost envelope (1-5-25 Claude army)

Target: **$0.50–$1.20** per packet wall-time on the orchestrator. Timebox is stated above in the packet header. If the executing Sonnet observes its own cost approaching the upper bound without nearing acceptance criteria, **escalate to Talon** by stopping work and committing a `WP-X-blocker.md` note to the worktree explaining what burned the budget. Do not silently exceed the envelope.

Cost-discipline rules of thumb:
- Read reference files at most once per session — cache content in working memory rather than re-reading.
- Run `dotnet test` against the focused subset (`--filter`) during iteration, full suite only at the end.
- If a refactor is pulling far more files than the spec named, stop and re-read the spec; the spec may be wrong about scope.

### Self-cleanup on merge

The active `docs/c2-infrastructure/work-packets/` directory should contain only **pending** packets. Shipped packets are deleted, not archived to `_completed-specs/` (Talon's convention from 2026-04-30 forward).

Before opening the PR, the executing Sonnet must:

1. **Check downstream dependents** with this command from the repo root:
   ```bash
   git grep -l "<THIS-PACKET-ID>" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```
   Replace `<THIS-PACKET-ID>` with the packet's identifier (e.g., `WP-3.0.4`).

2. **If the grep returns no results** (no other pending packet references this one): include `git rm docs/c2-infrastructure/work-packets/<this-packet-filename>.md` in the staging set. The deletion ships in the same commit as the implementation. Add the line `Self-cleanup: spec file deleted, no pending dependents.` to the commit message.

3. **If the grep returns one or more pending packets**: leave the spec file in place. Add a one-line status header to the top of this spec file (immediately under the H1):
   ```markdown
   > **STATUS:** SHIPPED to staging YYYY-MM-DD. Retained because pending packets depend on this spec: <list>.
   ```
   Add the line `Self-cleanup: spec retained, dependents: <list>.` to the commit message.

4. **Do not touch** files under `_completed/` or `_completed-specs/` — those are historical artifacts from earlier phases.

5. The git history (commit message + PR body) is the historical record. The spec file itself is ephemeral once shipped without dependents.
```

---

## Variant B — Track 2 packet (Unity, sandbox or integration)

Inline this footer in any packet that ships Unity assets, prefabs, scripts, or scene modifications. Visual verification by Talon is required before merge:

```markdown
---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

This is a Track 2 (Unity) packet. xUnit tests are necessary but **not sufficient** — the visual layer must be verified by Talon in Unity Editor before PR is mergeable.

The Sonnet executor's pipeline:

1. Implement the spec — write scripts, build prefabs, compose sandbox scene per spec.
2. Add or update xUnit tests to cover all logic-level acceptance criteria (where applicable). Visual aspects are not unit-tested; the test recipe handles them.
3. Run `dotnet test` and `dotnet build`. Must be green.
4. Stage all changes including the self-cleanup deletion (see below).
5. Commit on the worktree's feature branch.
6. Push the branch.
7. Stop. Do **not** open a PR yet. Do **not** merge.
8. Notify Talon (via the commit message's final line: `READY FOR VISUAL VERIFICATION — run Assets/_Sandbox/<feature>.md`) that the branch is ready for Talon's manual sandbox-recipe pass.

Talon's pipeline (after Sonnet's push):

1. Open the Unity Editor on the feature branch.
2. Run the test recipe shipped with this packet (path stated below in the spec).
3. If the recipe passes: open the PR, merge to `staging`.
4. If the recipe fails: file the failure in a follow-up packet or as PR review comments. **Do not** ask the original Sonnet to iterate ad-hoc — failed visual recipes mean the spec was incomplete or the implementation diverged; either way, a fresh packet captures the fix cleanly.

**The Sonnet executor must not push a packet without confirming the test recipe is achievable.** If the recipe is infeasible against what was actually built (e.g., a prefab couldn't be serialized correctly), stop and document in the worktree before pushing.

### Cost envelope (1-5-25 Claude army)

Target: **$0.50–$1.20** per packet wall-time on the orchestrator. Timebox is stated above in the packet header. If costs approach the upper bound without acceptance criteria nearing completion, **escalate to Talon** by stopping work and committing a `WP-X-blocker.md` note to the worktree explaining what burned the budget. Do not silently exceed the envelope.

Unity-specific cost-discipline:
- Don't open and close prefabs in the Editor repeatedly — script the prefab construction via a one-shot editor utility if practical, or hand-author the YAML if simple.
- Don't probe the Unity Asset database in a loop — load once, hold the reference.
- The reference grid / sandbox geometry should be the smallest thing that exercises the feature. Don't build elaborate scenes.

### Self-cleanup on merge

The active `docs/c2-infrastructure/work-packets/` directory should contain only **pending** packets. Shipped packets are deleted, not archived to `_completed-specs/` (Talon's convention from 2026-04-30 forward).

Before opening the PR (after Talon's visual verification passes), the executing Sonnet must:

1. **Check downstream dependents** with this command from the repo root:
   ```bash
   git grep -l "<THIS-PACKET-ID>" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```
   Replace `<THIS-PACKET-ID>` with the packet's identifier (e.g., `WP-3.1.S.0`). For sandbox packets, the corresponding `-INT` integration packet is the natural dependent — don't delete the sandbox spec until its `-INT` ships, too.

2. **If the grep returns no results**: include `git rm docs/c2-infrastructure/work-packets/<this-packet-filename>.md` in the staging set. Add `Self-cleanup: spec file deleted, no pending dependents.` to the commit message.

3. **If the grep returns one or more pending packets**: leave the spec file in place. Add a one-line status header to the top of this spec file:
   ```markdown
   > **STATUS:** SHIPPED to staging YYYY-MM-DD. Retained because pending packets depend on this spec: <list>.
   ```
   Add `Self-cleanup: spec retained, dependents: <list>.` to the commit message.

4. **Sandbox prefabs and sandbox scenes are NOT deleted** by this protocol — they live in `Assets/Prefabs/` and `Assets/_Sandbox/` indefinitely. Only the spec file itself is subject to cleanup.

5. **Do not touch** files under `_completed/` or `_completed-specs/`.
```

---

## Why the file-cleanup convention

The `_completed/` and `_completed-specs/` directories accumulated 30+ historical specs over Phase 0–3 work. They serve as audit trail but make the active `work-packets/` directory hard to read at a glance. Going forward (2026-04-30+):

- Active packets live in `docs/c2-infrastructure/work-packets/` directly.
- Shipped packets are *deleted* from that directory in the same commit that ships the implementation, unless a pending packet depends on the spec.
- The git history + PR description + commit message are the audit trail.
- Specs whose pending dependents later ship trigger a follow-up cleanup commit (by the dependent-shipping Sonnet, not by hand).

Result: at any moment, `ls docs/c2-infrastructure/work-packets/` shows exactly what's pending. No mental subtraction.

---

## What this protocol does not change

- The PHASE-N-KICKOFF-BRIEF and reality-check addendum still live in `docs/`.
- The `_completed/` and `_completed-specs/` directories are frozen — historical artifacts, do not modify.
- xUnit test discipline is unchanged.
- The orchestrator's existing dispatch flow is unchanged.
- Worktree-per-packet convention is unchanged.

This protocol only governs the lifecycle of the active spec files in `docs/c2-infrastructure/work-packets/`.
