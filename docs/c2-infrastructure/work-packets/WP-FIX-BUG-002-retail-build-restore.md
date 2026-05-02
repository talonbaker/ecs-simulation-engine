# WP-FIX-BUG-002 — Restore RETAIL Standalone Build

**Track:** 2 (Unity)
**Phase:** Inline fix — playtest-program-driven (sibling to phase development)
**Author:** Opus, 2026-05-02
**Sonnet executor:** assigned by Talon
**Branch:** `sonnet-wp-fix-bug-002`
**Worktree:** `.claude/worktrees/sonnet-wp-fix-bug-002/`
**Timebox:** 60–120 minutes (depends on triage outcome)
**Cost envelope:** $0.40–$0.80
**Feel-verified-by-playtest:** NO (this packet is build-correctness only; no feel surfaces)
**Build-verified-by-recipe:** YES — this packet's entire raison d'être is restoring the recipe's PASS state.
**Why:** modifies `ProjectSettings.asset`, asmdefs, and/or `#if WARDEN` guards.

---

## Goal

Restore SRD §8.7's commitment: a Standalone Player build with `WARDEN` removed from the scripting defines must compile cleanly and produce a runnable .exe. The engine ticks; the player surfaces work; WARDEN-only code (dev console, JSONL emitter, scenario verbs, AsciiMap projector) is correctly stripped.

This packet fixes BUG-002 (`docs/known-bugs.md`).

---

## Non-goals

- Do not introduce new features. Pure correctness restoration.
- Do not refactor the WARDEN/RETAIL boundary's *shape*. The split is established; this packet enforces it.
- Do not modify Editor-mode behaviour. Editor Play mode currently works; the fix must not regress it.
- Do not modify the engine substrate (`APIFramework/`). The engine is host-agnostic and the issue is on the Unity host side. (If triage reveals an `APIFramework/` issue, escalate — that would be a deeper §8.7 violation.)

---

## Before-implementation triage (REQUIRED first step)

The 960-error count strongly suggests a single root cause cascading. Triage in this order — **stop at the first finding** and address it before continuing, because the cascade may resolve once the root is fixed.

### Step T1 — Capture the error log

Reproduce the failure:
1. Open the project in Unity Editor.
2. **File > Build Settings**: switch to Windows Standalone, x86_64.
3. **Player Settings > Other Settings > Scripting Define Symbols**: remove `WARDEN` for the Standalone target. Apply.
4. **Build Settings > Build** (output to a scratch directory).
5. Wait for the build to fail. Open the build log (Console > Editor.log path; or `<project>/Library/Logs/Build.log` if present).
6. **Save the first 100 errors** to `WP-FIX-BUG-002-error-log.txt` in the worktree root. Don't commit it; use it for analysis.

### Step T2 — Identify the cascade root

Look at the **first 5–10** errors chronologically. The rest are almost certainly cascade.

- If errors mention `nunit.framework`, `UnityEngine.TestTools`, `[Test]` attribute, `Assert.*` → root cause is **test-asmdef leakage** (Hypothesis 1 in BUG-002). Skip to §"Fix path A."
- If errors mention specific WARDEN-only types like `DevConsolePanel`, `JsonlStreamEmitter`, `AsciiMapProjector`, `ScenarioCommand`, etc. → root cause is **non-WARDEN code referencing WARDEN-only types** (Hypothesis 2). Skip to §"Fix path B."
- If errors mention `UnityEditor.*` namespaces → root cause is **runtime code with missing `#if UNITY_EDITOR` guard** (Hypothesis 3). Skip to §"Fix path C."
- If errors are a mix → **multiple root causes**; address each in sequence; the cascade count drops with each fix.

### Step T3 — Verify Editor still works after every fix

After applying any fix, **before** re-running the recipe, confirm the Editor still works:
1. Re-add `WARDEN` to the Standalone scripting defines (the recipe's Phase 4 normally does this; do it manually after each iteration).
2. Open `Assets/Scenes/PlaytestScene.unity`. Press Play. Confirm 15 NPCs spawn, dev console opens with `~`, FPS holds.
3. If Editor regressed: revert the last fix, reconsider, try again.

---

## Fix path A — Test asmdef leakage

If the error log fingerprints test-framework symbols:

1. Open `ECSUnity/Assets/Tests/Edit/ECSUnity.Tests.Edit.asmdef`. In the Inspector:
   - **Include Platforms:** uncheck "Any Platform"; check ONLY `Editor`.
   - **Auto Referenced:** off.
   - Save.
2. Open `ECSUnity/Assets/Tests/Play/ECSUnity.Tests.Play.asmdef`. In the Inspector:
   - Play-mode tests run in the Editor and also in Standalone development builds. Check **Editor** at minimum. If the asmdef currently includes `nunit.framework` references for Standalone, those references must be moved to a Define Constraints `UNITY_INCLUDE_TESTS` block, or the asmdef restricted to Editor only (preferred unless the project actively runs Play tests on Standalone — verify with Talon).
   - Save.
3. Confirm the asmdef JSON is well-formed (Unity may reformat; verify `git diff` looks reasonable).
4. Re-run the build-verification recipe Phase 1–2. The cascade should clear or massively reduce.

---

## Fix path B — Non-WARDEN code referencing WARDEN-only types

If the error log fingerprints WARDEN-only types:

1. For each unique WARDEN-only type mentioned in the errors, search the project:
   ```bash
   git grep -l "DevConsolePanel\|JsonlStreamEmitter\|AsciiMapProjector\|ScenarioCommand\|<other-types>" -- 'ECSUnity/Assets/Scripts/'
   ```
2. For each file containing a reference, open it and verify:
   - Either the entire file is wrapped in `#if WARDEN ... #endif`, OR
   - The specific reference is wrapped in `#if WARDEN ... #endif`.
3. If a file has a non-guarded reference: wrap it. Use file-level guarding if the entire file's purpose is WARDEN-only; line-level guarding if the file mostly does non-WARDEN work and only references WARDEN types in specific code paths.
4. After each fix, re-run the recipe; cascade should reduce.

**Common offenders to check first:**
- `ECSUnity/Assets/Scripts/Engine/SceneBootstrapper.cs` — uses reflection to wire up renderers; verify any `JsonlStreamEmitter` or similar references are guarded.
- `ECSUnity/Assets/Scripts/UI/SettingsPanel.cs` — settings UI may expose dev-time toggles that reference WARDEN types.
- `ECSUnity/Assets/Scripts/UI/SaveLoadPanel.cs` — may emit telemetry on save events.
- Any `MonoBehaviour` with a `[SerializeField]` reference to a WARDEN-only `MonoBehaviour` type — `[SerializeField]` references are particularly easy to miss because the field declaration is a type reference even if the field is never used at runtime.

---

## Fix path C — `UnityEditor` references in runtime code

If the error log fingerprints `UnityEditor.*`:

1. Search:
   ```bash
   git grep -l "using UnityEditor" -- 'ECSUnity/Assets/Scripts/'
   ```
2. For each match, verify the `using` statement is inside a `#if UNITY_EDITOR ... #endif` block. If not, wrap it.
3. Verify any *uses* of `UnityEditor.*` types are also inside `#if UNITY_EDITOR` blocks (not just the using).
4. Re-run recipe; cascade should clear.

---

## Acceptance criteria

### A — Build verification recipe passes

A1. The recipe at `docs/playtest/BUILD-VERIFICATION-RECIPE.md` runs to completion on Standalone Windows x86_64 with WARDEN removed from scripting defines:
- Phase 1 setup completes.
- Phase 2 build succeeds (zero errors; warnings acceptable if pre-existing).
- Phase 3 runtime verification: .exe boots, 15 NPCs spawn, FPS ≥ 60, all "should be present" surfaces work, all "should be stripped" surfaces are absent (no dev console, no JSONL stream emitted, no PlaytestScene indicator).
- Phase 4 restoration: WARDEN re-added; Editor unaffected.

A2. The recipe's runtime verification of "Player log clean" passes — no `Error:`, `Exception:`, `NullReferenceException`, or "type/namespace could not be found" entries in the Player log during boot or first 60 seconds of runtime.

### B — Editor mode unaffected

B1. After all fixes applied and WARDEN restored to Editor scripting defines, opening `PlaytestScene.unity` and pressing Play produces an experience identical to pre-fix:
- 15 NPCs spawn.
- Dev console opens with `~`.
- All scenario verbs work (if WP-PT.1 is merged).
- All UI panels reachable.
- FPS holds.

### C — xUnit suite still green

C1. `dotnet test` from repo root: all existing tests pass. No regressions.

C2. If asmdefs were modified (Fix path A), the test asmdefs still actually load tests in the Editor — open Window > Test Runner; both Edit and Play test sets populate.

### D — Documentation updates

D1. Update `docs/known-bugs.md` BUG-002 entry: append `**Resolution:** Fixed in WP-FIX-BUG-002 (commit <SHA> on YYYY-MM-DD). Root cause was: <one-sentence summary>.` and move the entry to a "Resolved" subsection (or whatever the project's convention becomes — for now, just append Resolution and leave in place; the Phase 3 Reality-Check pattern is "leave the bug entry as audit trail, mark Resolved").

D2. Note in the commit message which Fix path(s) (A / B / C / multiple) were taken.

### E — Side-effect prevention

E1. Add a CI hint or README update: include a comment in `docs/playtest/BUILD-VERIFICATION-RECIPE.md` (already says this — verify it survives) that the recipe must be run after this fix and at minimum once per phase wave going forward.

E2. (Stretch — defer to a follow-up packet if scope creeps): wire a `dotnet test` / `Unity build standalone --batchmode` check into a future CI pipeline so this regression cannot silently recur. Out of scope for this packet.

---

## Files likely to modify (depends on triage outcome)

| Fix path | File | Change |
|---|---|---|
| A | `ECSUnity/Assets/Tests/Edit/ECSUnity.Tests.Edit.asmdef` | Restrict Include Platforms to Editor |
| A | `ECSUnity/Assets/Tests/Play/ECSUnity.Tests.Play.asmdef` | Same |
| B | Various scripts under `ECSUnity/Assets/Scripts/` | Wrap WARDEN-only references in `#if WARDEN` |
| C | Various scripts under `ECSUnity/Assets/Scripts/` | Wrap `UnityEditor` references in `#if UNITY_EDITOR` |
| All | `docs/known-bugs.md` | Append Resolution to BUG-002 |

The packet's actual file diff depends on what triage finds. The Sonnet executor commits the actual changes; the spec is intentionally vague on file count because the cascade root determines scope.

---

## Dependencies

- **Hard:** none. This packet can dispatch immediately — does not depend on WP-PT.1 merging.
- **Soft:** may interact with WP-PT.1 if the cascade root is in a WP-PT.1-introduced file. If that's the case, coordinate with the WP-PT.1 Sonnet (or fix here and merge order both before merging WP-PT.1 to staging).

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

This is a Track 2 packet. The acceptance criteria are entirely build-correctness, but the build-verification recipe IS the visual verification (it validates the runtime behaviour of the .exe).

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm you are in `.claude/worktrees/sonnet-wp-fix-bug-002/` on branch `sonnet-wp-fix-bug-002` based on recent `origin/staging`. If anything is wrong, stop and notify Talon.
1. **Triage** per §"Before-implementation triage" above. **Do not skip the triage step**; the fix shape depends on it.
2. **Apply fixes** per the appropriate Fix path(s).
3. **Verify Editor still works** after each fix iteration.
4. **Run the build-verification recipe** end to end. Acceptance is the recipe passing, not just compilation succeeding.
5. **Run xUnit suite** (`dotnet test`). Must be green.
6. **Update `docs/known-bugs.md`** BUG-002 entry with Resolution.
7. Stage all changes including the self-cleanup deletion (see below).
8. Commit on `sonnet-wp-fix-bug-002`. Final commit message line: `READY FOR VERIFICATION — re-run docs/playtest/BUILD-VERIFICATION-RECIPE.md`.
9. Push.
10. Stop. Do not open a PR; do not merge.

Talon's pipeline:

1. Pull the branch.
2. Re-run the build-verification recipe independently.
3. If passes: open PR, merge.
4. If still fails (cascade not fully cleared): file a follow-up `WP-FIX-BUG-002.b` or PR-comment iteration. The 960-error case may have multiple roots in series.

### Cost envelope

Target: **$0.40–$0.80**. Timebox 60–120 minutes. Triage is the cheap step (read errors, identify root); the fix can be mechanical (one asmdef edit, or N file-level guards) or extensive (deep WARDEN-boundary cleanup if multiple files leak references). Escalate at 80% budget if cascade isn't clearing.

### Self-cleanup on merge

After Talon's verification passes and the PR merges:

1. Check downstream dependents:
   ```bash
   git grep -l "WP-FIX-BUG-002\|BUG-002" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```

2. If grep returns no pending packets: include `git rm docs/c2-infrastructure/work-packets/WP-FIX-BUG-002-retail-build-restore.md` in the staging set. Add `Self-cleanup: spec file deleted, no pending dependents.` to the commit message.

3. If grep returns dependents (e.g., a `WP-FIX-BUG-002.b` follow-up exists): leave the spec in place with a `> **STATUS:** SHIPPED` header.

4. **Do not** delete the BUG-002 entry from `known-bugs.md`. Resolved bugs stay as audit trail; the entry's Resolution line marks the closure.

5. **Do not touch** files under `_completed/` or `_completed-specs/`.
