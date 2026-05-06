# Packet Completion Protocol — Reference

> **Audience:** Future Opus authors of work packets.
> **Purpose:** The "Completion protocol" footer is **inlined verbatim** into every packet so Sonnet executors have a single self-contained source of truth. This file is the canonical text. **Do not reference this file from packet bodies** — copy the appropriate variant in.
> **Authority:** Established 2026-04-30 by Opus General + Talon. Living document.

---

## Why this protocol exists

Sonnet executors must finish a packet without further instruction from Talon. Every packet declares:

1. **Dispatch discipline** — one packet, one worktree, one branch. Named for the packet ID.
2. **Whether visual verification is needed** in Unity Editor before PR (Track 2) or whether passing tests is sufficient (Track 1).
3. **Cost envelope** — orchestrator dispatches must stay within the 1-5-25 Claude army's per-packet budget ($0.50–$1.20).
4. **Self-cleanup on merge** — the packet spec file is deleted on merge unless a still-pending packet depends on it. Keeps the active `work-packets/` directory clean as the project moves forward.

---

## Dispatch protocol — one worktree per packet

**Rule (mandatory for every packet, Track 1 and Track 2):**

Every new work packet is dispatched into its own dedicated git worktree. Worktrees are not reused across packets. Branches are not reused across packets.

**Naming convention:**

| Artifact | Pattern | Example |
|---|---|---|
| Branch | `sonnet-wp-<id>` | `sonnet-wp-3.2.2` |
| Worktree path | `.claude/worktrees/sonnet-wp-<id>/` | `.claude/worktrees/sonnet-wp-3.2.2/` |

The packet ID is the same identifier used in the spec filename — lowercase, dot-separated. For sandbox packets: `sonnet-wp-3.1.s.2`. For integration: `sonnet-wp-3.1.s.2-int`. For Warden-side: `sonnet-wp-3.0.w`.

**Standard dispatch sequence (Talon's side):**

```
git checkout staging
git pull origin staging
git worktree add .claude/worktrees/sonnet-wp-<id> -b sonnet-wp-<id>
# point the Sonnet at the worktree directory; it does its work there
```

**Why one-per-packet:**

- **Visibility for the dispatcher.** A glance at `git worktree list` or `ls .claude/worktrees/` shows exactly what's in flight, with packet IDs in the names. No mental mapping from anonymous worktrees to active work.
- **Isolated testing.** Each worktree builds and tests against its own checkout. A failing test on one branch doesn't pollute another. Talon can run the Unity Editor on one worktree while xUnit tests churn on another.
- **Clean retirement.** After a packet merges, `git worktree remove .claude/worktrees/sonnet-wp-<id>` blows it away cleanly. No leftover state, no half-stashed changes, no "what was I doing here again."
- **Parallelism without conflict.** Multiple Sonnets in flight at once is the default mode (per Talon's operating preferences). One worktree per packet is the discipline that makes that safe.

**Sonnet executor responsibility:**

Before doing anything else, the Sonnet confirms:

1. The current working directory is a worktree at `.claude/worktrees/sonnet-wp-<id>/` (or equivalent path).
2. The current branch is `sonnet-wp-<id>` (matching the packet being implemented).
3. The branch's base is recent `origin/staging`.

If any of these is wrong — wrong directory, wrong branch, branch based on something other than recent staging — **stop and notify Talon**. Do not start implementing in someone else's worktree.

**Retirement on merge:**

When Talon merges the PR to staging, the worktree is no longer needed. Standard cleanup:

```
git worktree remove .claude/worktrees/sonnet-wp-<id>
git branch -D sonnet-wp-<id>
```

Talon may keep a worktree alive briefly post-merge for spot inspection; that's fine. The expectation is "blown away within a day or two of merge."

---

## Variant A — Track 1 packet (engine, headless)

Inline this footer in any packet that touches engine code only and is fully verifiable via `dotnet test`:

```markdown
---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: NOT NEEDED

This is a Track 1 (engine) packet. All verification is handled by the xUnit test suite. Once `dotnet test` returns green for `APIFramework.Tests` (and any other affected test project), the packet is ready to push and PR. **No Unity Editor steps required.**

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm you are in a dedicated worktree at `.claude/worktrees/sonnet-wp-<id>/` on branch `sonnet-wp-<id>` based on recent `origin/staging`. If anything is wrong, stop and notify Talon. (See the **Dispatch protocol** section above.)
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

0. **Worktree pre-flight.** Confirm you are in a dedicated worktree at `.claude/worktrees/sonnet-wp-<id>/` on branch `sonnet-wp-<id>` based on recent `origin/staging`. If anything is wrong, stop and notify Talon. (See the **Dispatch protocol** section above.)
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

### Feel-verified-by-playtest acceptance flag (when applicable)

> Per Rule 6 of `docs/UNITY-PACKET-PROTOCOL.md`. Added 2026-05-01 with the Playtest Program kickoff.

The packet header declares whether this packet has feel-level acceptance criteria:

```markdown
**Feel-verified-by-playtest:** YES | NO
**Surfaces evaluated by next PT-NNN:** <list — only when YES>
```

If **YES**, the test recipe in this spec covers *first-light* (does it boot, does it not throw, does the basic primitive function?) — but the formal feel acceptance is the next post-merge `PT-NNN` session of the Playtest Program (see `docs/playtest/README.md`). The flag does **not** gate this packet's merge; it declares that a session is owed evaluation of this work, and that bugs surfacing in that session feed normal `BUG-NNN` intake referencing this packet.

If **NO**, the test recipe + xUnit are sufficient and no playtest session is owed. (Reserve NO for packets whose acceptance is purely contract / first-light. If you find yourself writing acceptance criteria with "feels," "reads as," "doesn't stutter," etc., the answer is YES.)

The default for any Track 2 packet that ships visual output, motion, audio, or emergent gameplay is **YES**. Pure tooling / config / asset-import packets may declare NO with a sentence of justification.

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

The worktree-per-packet rule is now formalised in the **Dispatch protocol** section above (previously informal); this protocol governs the spec-file lifecycle and dispatch discipline together.
