# Haiku Cleanup Work Item — Active Spec Directory

> **Audience:** A Claude Haiku dispatched to perform this exact task in one pass.
> **Scope:** Mechanical file deletion + commit + push. No judgment calls.
> **Timebox:** 5 minutes wall-time. Budget: $0.05.
> **Branch:** `haiku-active-specs-cleanup`. Worktree at `.claude/worktrees/haiku-active-specs-cleanup/`.

---

## What this is

Phase 3 closed with 22 stale spec files in `docs/c2-infrastructure/work-packets/`. Every one of them is for a packet that has already been merged to staging. Per the **Self-cleanup on merge** rule in `_PACKET-COMPLETION-PROTOCOL.md`, these specs should have been `git rm`'d in their respective merge commits but weren't. This work item cleans them up in one batch.

After this work item ships, `ls docs/c2-infrastructure/work-packets/*.md` will show **zero files** (the directory still contains `_PACKET-COMPLETION-PROTOCOL.md`, `_completed/`, `_completed-specs/`, and `p3-wip/` — none of those are touched). That's the desired end state: an empty active directory means no pending Phase 3 work.

---

## Pre-flight

1. Confirm you are in a worktree at `.claude/worktrees/haiku-active-specs-cleanup/` on branch `haiku-active-specs-cleanup` based on recent `origin/staging`. If not, stop and notify Talon.
2. Confirm the working tree is clean: `git status` shows nothing modified or untracked.
3. Confirm the 22 files listed below all exist:

```
docs/c2-infrastructure/work-packets/WP-10-chain-of-thought-persistence.md
docs/c2-infrastructure/work-packets/WP-11-fail-closed-escalation.md
docs/c2-infrastructure/work-packets/WP-12-report-aggregator.md
docs/c2-infrastructure/work-packets/WP-13-dispatcher-banned-pattern-wiring.md
docs/c2-infrastructure/work-packets/WP-3.0.3-slip-and-fall-and-locked-in-and-starved.md
docs/c2-infrastructure/work-packets/WP-3.0.4-live-mutation-hardening.md
docs/c2-infrastructure/work-packets/WP-3.0.W.1-haiku-prompt-ascii-map-integration.md
docs/c2-infrastructure/work-packets/WP-3.1.S.0-INT-camera-rig-into-mainscene.md
docs/c2-infrastructure/work-packets/WP-3.1.S.0-camera-rig-sandbox.md
docs/c2-infrastructure/work-packets/WP-3.1.S.1-INT-selection-into-npc-renderer.md
docs/c2-infrastructure/work-packets/WP-3.1.S.1-selection-outline-sandbox.md
docs/c2-infrastructure/work-packets/WP-3.1.S.2-INT-draggable-into-build-mode.md
docs/c2-infrastructure/work-packets/WP-3.1.S.2-draggable-prop-sandbox.md
docs/c2-infrastructure/work-packets/WP-3.1.S.3-INT-popup-into-selection.md
docs/c2-infrastructure/work-packets/WP-3.1.S.3-inspector-popup-sandbox.md
docs/c2-infrastructure/work-packets/WP-3.2.0-save-load-round-trip-hardening.md
docs/c2-infrastructure/work-packets/WP-3.2.1-sound-trigger-bus.md
docs/c2-infrastructure/work-packets/WP-3.2.2-rudimentary-physics.md
docs/c2-infrastructure/work-packets/WP-3.2.3-chore-rotation-system.md
docs/c2-infrastructure/work-packets/WP-3.2.4-rescue-mechanic.md
docs/c2-infrastructure/work-packets/WP-3.2.5-per-archetype-tuning-jsons.md
docs/c2-infrastructure/work-packets/WP-3.2.6-silhouette-animation-state-expansion.md
```

If any file is missing, **stop and notify Talon** — the merged-state assumption may be wrong.

---

## Execute

Run this exact command from the repo root (the worktree's working directory):

```bash
git rm docs/c2-infrastructure/work-packets/WP-10-chain-of-thought-persistence.md \
       docs/c2-infrastructure/work-packets/WP-11-fail-closed-escalation.md \
       docs/c2-infrastructure/work-packets/WP-12-report-aggregator.md \
       docs/c2-infrastructure/work-packets/WP-13-dispatcher-banned-pattern-wiring.md \
       docs/c2-infrastructure/work-packets/WP-3.0.3-slip-and-fall-and-locked-in-and-starved.md \
       docs/c2-infrastructure/work-packets/WP-3.0.4-live-mutation-hardening.md \
       docs/c2-infrastructure/work-packets/WP-3.0.W.1-haiku-prompt-ascii-map-integration.md \
       docs/c2-infrastructure/work-packets/WP-3.1.S.0-INT-camera-rig-into-mainscene.md \
       docs/c2-infrastructure/work-packets/WP-3.1.S.0-camera-rig-sandbox.md \
       docs/c2-infrastructure/work-packets/WP-3.1.S.1-INT-selection-into-npc-renderer.md \
       docs/c2-infrastructure/work-packets/WP-3.1.S.1-selection-outline-sandbox.md \
       docs/c2-infrastructure/work-packets/WP-3.1.S.2-INT-draggable-into-build-mode.md \
       docs/c2-infrastructure/work-packets/WP-3.1.S.2-draggable-prop-sandbox.md \
       docs/c2-infrastructure/work-packets/WP-3.1.S.3-INT-popup-into-selection.md \
       docs/c2-infrastructure/work-packets/WP-3.1.S.3-inspector-popup-sandbox.md \
       docs/c2-infrastructure/work-packets/WP-3.2.0-save-load-round-trip-hardening.md \
       docs/c2-infrastructure/work-packets/WP-3.2.1-sound-trigger-bus.md \
       docs/c2-infrastructure/work-packets/WP-3.2.2-rudimentary-physics.md \
       docs/c2-infrastructure/work-packets/WP-3.2.3-chore-rotation-system.md \
       docs/c2-infrastructure/work-packets/WP-3.2.4-rescue-mechanic.md \
       docs/c2-infrastructure/work-packets/WP-3.2.5-per-archetype-tuning-jsons.md \
       docs/c2-infrastructure/work-packets/WP-3.2.6-silhouette-animation-state-expansion.md
```

**This work item itself is also deleted as part of the cleanup.** It served its purpose; the git history is the audit trail. Add this command to the same execution:

```bash
git rm docs/c2-infrastructure/HAIKU-CLEANUP-WORK-ITEM.md
```

---

## Verify

After the deletions, verify both expected end-states with one command each:

```bash
ls docs/c2-infrastructure/work-packets/*.md 2>/dev/null
```

**Expected:** no files matched (the only remaining content is `_PACKET-COMPLETION-PROTOCOL.md`, but that pattern excludes it via the underscore prefix). If any `WP-*.md` file remains, the deletion was incomplete — investigate and re-run.

```bash
ls docs/c2-infrastructure/HAIKU-CLEANUP-WORK-ITEM.md 2>/dev/null
```

**Expected:** no such file (this work item itself is gone).

---

## Commit and push

Commit message verbatim (multi-line, copy as-is):

```
Phase 3 closure: clean 22 stale specs from active work-packets dir

Every spec listed below is for a packet already merged to staging.
Per the Self-cleanup on merge rule in _PACKET-COMPLETION-PROTOCOL.md
these should have been git rm'd in their merge commits but weren't.
This commit cleans them up in one batch as part of Phase 3 closure.

Removed (22 specs):
- WP-10, 11, 12, 13 (Phase 0)
- WP-3.0.3, 3.0.4 (engine substrate)
- WP-3.0.W.1 (Haiku ASCII-map integration)
- WP-3.1.S.0, S.0-INT, S.1, S.1-INT, S.2, S.2-INT, S.3, S.3-INT
  (sandbox protocol re-do, all four foundation primitives + integrations)
- WP-3.2.0 through 3.2.6 (gameplay deepening — save/load, sound,
  physics, chores, rescue, per-archetype tuning, animation states)

Also removed: HAIKU-CLEANUP-WORK-ITEM.md itself, having served its
purpose. Git history is the audit trail.

After this commit, ls docs/c2-infrastructure/work-packets/*.md
returns empty — Phase 3 has no pending packets.
```

Then:

```bash
git push origin haiku-active-specs-cleanup
```

Stop. Talon merges the PR after review. Do **not** merge.

---

## Don't do these

- **Do NOT** touch `docs/c2-infrastructure/work-packets/_PACKET-COMPLETION-PROTOCOL.md`. That is the canonical reference; not subject to cleanup.
- **Do NOT** touch `docs/c2-infrastructure/work-packets/_completed/` or `_completed-specs/`. Frozen historical artifacts.
- **Do NOT** touch `docs/c2-infrastructure/work-packets/p3-wip/`. Phase 3 work-in-progress notes; preserved for the audit trail.
- **Do NOT** move any spec to `_completed-specs/`. The 2026-04-30+ convention is **deletion**, not archival. Git history is the audit trail.
- **Do NOT** add a "shipped" status header to any spec. They are being deleted, not retained.
- **Do NOT** run `dotnet test` or `dotnet build`. This is a doc-only PR; no code is touched.
- **Do NOT** modify any other file. If you discover a separate cleanup need (e.g., a stale entry in `MEMORY.md` or a typo in a bible), surface it as a follow-up PR. This work item is one job, one commit.
- **Do NOT** dispatch this work item alongside any other. Solo dispatch.

---

## Cost envelope

- Target: **$0.05**.
- Wall-time: **≤5 minutes**.
- If the task takes longer or any verification step fails, stop and write a `HAIKU-CLEANUP-BLOCKER.md` note in the worktree explaining what went wrong. Do not silently proceed.

---

*This work item is the simplest possible job: 22 deletions, one commit, one push. The point is to leave the active work-packets directory empty so Phase 4 starts clean. The next packet to land in the active directory will be `WP-4.0.0 — UX/UI bible v0.2 critique` once Talon's v0.2 draft is ready.*
