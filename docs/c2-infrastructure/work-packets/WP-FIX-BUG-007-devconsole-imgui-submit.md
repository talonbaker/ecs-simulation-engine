# WP-FIX-BUG-007 — Dev Console IMGUI Command Submit

> Fixes BUG-007 (`docs/known-bugs.md`). Surfaced by PT-001 (`docs/playtest/PT-001-baseline.md`).

**Track:** 2 (Unity)
**Phase:** Inline fix — playtest-program-driven
**Author:** Opus, 2026-05-02
**Sonnet executor:** assigned by Talon
**Branch:** `sonnet-wp-fix-bug-007`
**Worktree:** `.claude/worktrees/sonnet-wp-fix-bug-007/`
**Timebox:** 45–75 minutes
**Cost envelope:** $0.20–$0.40
**Feel-verified-by-playtest:** YES — submit responsiveness is feel.
**Surfaces evaluated by next PT-NNN:** every `scenario *` subverb (12 of them); existing commands (`help`, `force-kill`, `force-faint`, `inspect`, `lock`, `unlock`, `move`, `pause`, `resume`, `save`, `load`, `tickrate`, `set-component`, `spawn`, `despawn`).
**Build-verified-by-recipe:** NO.
**Parallel-safe with:** WP-FIX-BUG-004 (UI wiring), WP-FIX-BUG-005 (camera + audio). Disjoint files.

---

## Goal

Pressing Enter in the IMGUI dev console actually executes the command typed. Currently:
- Backtick opens the panel ✅
- Typing produces visible characters ✅
- **Enter does nothing** ❌ — no echo, no output, no error
- Result: WP-PT.1's entire 12-subverb scenario surface is unreachable

After this packet, every command (legacy and scenario-prefixed) executes correctly through the IMGUI path, and any error condition surfaces a visible `ERROR:` line in the console history.

---

## Non-goals

- Do not switch the dev console to UI Toolkit. The IMGUI fallback is the load-bearing path.
- Do not author new scenario subverbs. Existing 12 are what we ship in WP-PT.1; this packet only fixes their dispatch path.
- Do not modify `DevConsoleCommandDispatcher` itself unless investigation reveals a bug there.

---

## Investigation phase

The bug has two candidate root causes (per BUG-007 entry). Sonnet must determine which (or both) are true before patching.

### Step T1 — Confirm OnGUI sees Enter events

Add a temporary diagnostic at the top of `DevConsolePanel.OnGUI()`:

```csharp
if (Event.current != null && Event.current.type == EventType.KeyDown)
    Debug.Log($"[DevConsolePanel.OnGUI] KeyDown {Event.current.keyCode} ({(int)Event.current.character}) _savedInput='{_savedInput}'");
```

Press Play. Press backtick. Type `help`. Press Enter. Note what shows in the Console:

- If `KeyDown Return _savedInput='help'` appears: the input is correctly bound to `_savedInput`; the bug is in the submit path itself (proceed to T2 path "submit").
- If `KeyDown Return _savedInput=''` appears: the TextField hasn't committed its value to `_savedInput` yet on this event; race condition (proceed to T2 path "race").
- If no `KeyDown Return` log appears at all: the Enter event is being consumed before OnGUI sees it; another component (UnityEngine.UI EventSystem? IMGUI default control?) is intercepting it.

### Step T2 — Pick fix path based on T1 outcome

#### Path "submit" — _savedInput has the value but submit doesn't fire

Inspect the existing OnGUI Enter-handling block:

```csharp
case KeyCode.Return:
case KeyCode.KeypadEnter:
    if (!string.IsNullOrWhiteSpace(_savedInput))
    {
        string toSubmit = _savedInput;
        _savedInput     = string.Empty;
        SubmitCommand(toSubmit);
        _guiScrollPos.y = float.MaxValue;
    }
    Event.current.Use();
    break;
```

Possible bug: `Event.current.Use()` is called even when the input is empty, suppressing further processing of subsequent events. Or `SubmitCommand` is throwing an exception silently (the dispatcher has a try/catch but it logs to Debug.LogError, which Talon may not be checking).

Action: add Debug.Log lines around `SubmitCommand(toSubmit)`:
```csharp
Debug.Log($"[DevConsolePanel] SubmitCommand('{toSubmit}')");
SubmitCommand(toSubmit);
Debug.Log($"[DevConsolePanel] After Submit: history.Count={_history.Count}");
```

If history count goes up but UI doesn't reflect it: rendering bug. If history count stays the same: dispatcher rejected the command silently.

#### Path "race" — TextField value not yet in _savedInput

The IMGUI TextField commits its value to the bound variable on the *next* OnGUI repaint, not on the same KeyDown event. So when the KeyDown handler reads `_savedInput`, it's still empty.

Fix: capture the current TextField value via `GUI.GetNameOfFocusedControl()` + Unity's text editor state, or restructure to use `Event.current.character == '\n'` which fires on the next event (after the value commits):

```csharp
// Instead of KeyDown KeyCode.Return:
if (Event.current.type == EventType.KeyDown && Event.current.character == '\n')
{
    if (!string.IsNullOrWhiteSpace(_savedInput))
    {
        string toSubmit = _savedInput;
        _savedInput = string.Empty;
        SubmitCommand(toSubmit);
        _guiScrollPos.y = float.MaxValue;
    }
    Event.current.Use();
}
```

Alternatively, set the value of the TextField from a separate variable updated via `_savedInput = GUILayout.TextField(_savedInput, ...)` and check `_savedInput` BEFORE rendering the TextField for this frame (snapshot the previous frame's value).

#### Path "consumed" — Enter never reaches OnGUI

Likely cause: a different MonoBehaviour with an OnGUI is calling `Event.current.Use()` on Enter before DevConsolePanel sees it. Or the GameObject containing DevConsolePanel has a higher script execution order than the consumer.

Search for any script that calls `Event.current.Use()` in an OnGUI body. Adjust execution order via `[DefaultExecutionOrder]` attribute on DevConsolePanel to run before others.

### Step T3 — Verify scenario verbs after fix

Once submit works:

1. Open dev console.
2. Run `scenario` (no args). Should list 12 subverbs in the history scroll.
3. Run `scenario kill <some-npc-name>`. Watch:
   - History shows the echo `> scenario kill <name>`
   - Engine: NPC dies; bereavement cascade fires on witnesses
   - History shows success message or ERROR if MutationApi is null
4. Each subverb should produce a visible response (success or error). Silent failures = bug.

---

## Acceptance criteria

### A — Submit fires on Enter

A1. After the fix, pressing Enter in the dev console with non-empty input produces:
- The command echoed to history (`> <command>` line)
- Either the command's success output OR a clear `ERROR: ...` message
- `_savedInput` cleared (input field empty for next command)

A2. Pressing Enter with empty input does nothing (no error, no extra blank line).

### B — Existing commands work

B1. `help` lists all registered commands.
B2. `force-kill <npc>` and `force-faint <npc>` (preserved aliases from before WP-PT.1) still produce the expected NPC state changes.
B3. `inspect <id-or-name>`, `inspect-room <room>`, `lock <door>`, `unlock <door>`, `pause`, `resume` all execute and produce visible output.

### C — All 12 scenario subverbs work

C1. `scenario` (no args) — list of subverbs renders in history.
C2. `scenario help <subverb>` — detailed help for that subverb.
C3. Each subverb (`choke`, `slip`, `faint`, `lockout`, `kill`, `rescue`, `chore-microwave-to`, `throw`, `sound`, `set-time`, `seed-stains`, `seed-bereavement`) produces either:
- Visible engine state change (NPC choking, stain spawned, time jumped, etc.)
- A visible `ERROR:` message describing why it didn't fire (e.g., "NPC not found", "scene has no doors to lock", etc.)

C4. **No silent failures.** Every Enter press produces visible feedback.

### D — RETAIL strip preserved

D1. The dev console + submit path is `#if WARDEN`-gated. Building under non-WARDEN must still produce no DevConsole references in the binary. The `DevConsoleRetailStripTests.cs` test (existing) still passes.

### E — xUnit + build green

E1. `dotnet test` from repo root: green.
E2. Existing `DevConsoleAutocompleteCommandTests`, `DevConsoleHelpCommandTests`, `DevConsoleScenarioRegistrationTests`, etc. all pass.

---

## Files likely to modify

- `ECSUnity/Assets/Scripts/DevConsole/DevConsolePanel.cs` (OnGUI Enter handling, possibly RefreshContext / SubmitCommand)
- Possibly `ECSUnity/Assets/Scripts/DevConsole/DevConsoleCommandDispatcher.cs` if the dispatcher is silently swallowing exceptions
- Possibly `ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/*.cs` if specific subverbs throw NREs that mask the submit-path issue

## Dependencies

- **Hard:** none.
- **Soft:** none — fully disjoint from WP-FIX-BUG-004 / WP-FIX-BUG-005.

## Completion protocol

### Visual verification: REQUIRED

0. Worktree pre-flight at `.claude/worktrees/sonnet-wp-fix-bug-007/` on branch `sonnet-wp-fix-bug-007`.
1. Investigation phase (T1 / T2 / T3). Document findings in worktree.
2. Apply the fix path identified in T1.
3. Verify acceptance §A / §B / §C in PlaytestScene Play mode.
4. `dotnet test` green.
5. Stage / commit / push. Final line: `READY FOR VISUAL VERIFICATION — backtick console, type any command, press Enter`.
6. Talon verifies during PT-002 (or a focused mini-session if Talon wants to check this independently).

### Feel-verified-by-playtest

**YES.** Submit latency, error messages' clarity, scenario-verb outputs all need feel acceptance. PT-002.

### Cost envelope

$0.20–$0.40. Timebox 45–75 minutes. Investigation is the bulk; the actual fix is likely 5–20 lines.

### Self-cleanup

1. Grep dependents.
2. If none: `git rm` this file in same commit.
3. Append Resolution to BUG-007 in `docs/known-bugs.md`.
