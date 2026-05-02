# Known Bugs / Backlog

Issues confirmed but deferred — to be revisited when the relevant system is mature enough to support a proper fix.

---

## Build Mode — Drag & Place

### BUG-001: Large prop placed on top of small prop causes disappear / incorrect placement

**Symptom:** Dragging a table onto a banana sitting on the floor causes the table to disappear or settle at an incorrect position. The banana is not displaced.

**Root cause (investigated):** `GetSurfaceYAtXZ` returns the banana's top surface as the floor height, so the table's pivot-to-bottom offset stacks incorrectly. Attempts to auto-displace the banana (socket-snap or raise-to-top) each introduced secondary bugs: `OnDropped` firing on displacement triggering PropToEngineBridge snap-back, free-floating displaced props causing `_dragPlaneY` oscillation ("freak-out") on next pick-up.

**Deferred because:** Prop-on-prop displacement resolution belongs in a broader "build mode v2" spatial pass alongside proper footprint tracking, multi-prop stacking rules, and undo/redo. The core drag workflow (grab → move → snap → socket drop) is unaffected.

**Workaround:** Place small props AFTER large props, or manually move the small prop out of the way before placing the large one.

**Files relevant to fix:** `DragHandler.cs` (`GetSurfaceYAtXZ`, displacement logic), `DraggableProp.cs` (`SnapToSocketSilent`, `CancelDrag` — scaffolding already in place for a future attempt).

---

## Project Hygiene — Unity Meta Files

### BUG-003: Unity .meta files globally gitignored — GUIDs non-portable across checkouts

**Symptom:** Hand-authored / Sonnet-authored scenes referenced fabricated script GUIDs that did not match anyone's actual `.cs.meta` files. PlaytestScene's DevConsole GameObject showed "Missing Script" in Inspector; backtick (`~`) did nothing. A Sonnet attempting to "fix" the GUID replaced one fake value with another fake value, deepening the regression. 21 of 252 `.cs.meta` files in `ECSUnity/Assets/` were tracked in git; the other 231 existed only on Talon's local disk with locally-generated GUIDs that no other checkout could see.

**Root cause:** Root `.gitignore` line 93 contained `*.meta` — copy-pasted from the Visual Studio C++ template (where `*.meta` denotes VS-build metadata). For a Unity project this is catastrophic: Unity requires `.meta` files to be committed alongside their assets, because every asset's GUID lives in its `.meta` file and every scene/prefab references those GUIDs. Without committed meta files:

- Every fresh checkout of a script gets a freshly-generated random GUID.
- Hand-authored scenes referencing specific GUIDs only work on the machine where those GUIDs were generated.
- LLM-authored scenes (which can't run Unity to discover real GUIDs) tend to fabricate GUIDs entirely, producing scenes that are broken by construction.

**Severity:** **High** (was masquerading as Critical via the dev-console-doesn't-open symptom; the underlying gitignore misconfiguration is High-severity project hygiene).

**Repro:**
1. Fresh-clone the repo to a new machine (or a fresh worktree).
2. Open `ECSUnity` in Unity Editor.
3. Open `Assets/Scenes/PlaytestScene.unity`.
4. Inspector shows "The referenced script (Unknown) on this Behaviour is missing!" on the DevConsole GameObject (and any other GameObject whose script's `.cs.meta` was not committed and whose locally-generated GUID differs from what's in the scene).
5. Press Play. Press backtick. Console does not open.

**Resolution (applied in this commit, 2026-05-02):**

1. **`.gitignore` corrected:** added `!ECSUnity/**/*.meta` and `!UnityVisualizer/**/*.meta` exception lines below the VS-template `*.meta` rule. Unity meta files now pass the ignore check.
2. **`ECSUnity/Assets/Scripts/DevConsole/DevConsolePanel.cs.meta` committed** to lock the GUID `f7afdf11114f0524d82fff4ed410d797` as canonical. PlaytestScene's m_Script for the DevConsole GameObject reverted to this GUID (was fabricated `1c3726b26c7be2742801aeab3a401319` from commit `505e91a`'s misdirected fix attempt).
3. **Talon's main checkout has 83 other un-committed `.cs.meta` files** that should be force-added in a follow-up commit. After pulling this fix to `staging`, run from Talon's main checkout:
   ```
   git add ECSUnity/Assets/Scripts/**/*.cs.meta ECSUnity/Assets/Scripts/**/*.asmdef.meta
   git status   # review the diff before committing — should be ~80+ new tracked meta files
   git commit -m "Project hygiene: commit Unity .cs.meta files (BUG-003 follow-up)"
   ```

**Files relevant:** `.gitignore` (line 93 + new exception lines); every `*.cs.meta` file in `ECSUnity/Assets/Scripts/` and `ECSUnity/Assets/Editor/`.

**Discovered in:** WP-PT.1 first-light testing (2026-05-02). Talon noticed PlaytestScene had multiple "Missing Script" warnings and the dev console wouldn't open. Triage traced the immediate symptom to one fabricated GUID in the scene; root-cause traced to the gitignore.

**Why this went undetected:** the verification stack at this point in the project (xUnit, sandbox protocol, Editor Play mode on Talon's machine) cannot detect missing-meta-file regressions, because Talon's local meta files exist and provide stable GUIDs *on his machine*. The failure mode only surfaces on a fresh checkout — which is exactly what Sonnets do when dispatched into new worktrees. The Build Verification Recipe (BUG-002 follow-on) does not catch this either, because the Player build doesn't load scenes via meta-file GUID resolution the same way the Editor does. Future cleanup: extend the playtest program with a "fresh-checkout sanity recipe" that verifies a scene loads cleanly from a clean clone.

---
