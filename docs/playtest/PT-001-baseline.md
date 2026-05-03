# PT-001 — Baseline first-light + WP-PT.1 scenario verbs

**Date:** 2026-05-02
**Duration:** ~30 minutes (recipe + verb block)
**Build:** `401da01` (staging — Fix dev-console API drift: MoveCommand, HistoryCommand, InspectRoomCommand)
**Scene:** `Assets/Scenes/PlaytestScene.unity`
**Focus:** Run the WP-PT.0 first-light recipe end to end; immediately follow with WP-PT.1's scenario-verb block. First real session of the Playtest Program.

---

## Setup

- NPC count: 15
- Pre-session scenario seeds (if any): none
- Time started in-sim: 08:30 (default)
- Soften toggle: not reachable in this session (Settings panel didn't render — see BUG-004)
- Time speed used: ×1, ×4

---

## What I did

Followed `Assets/Scenes/PlaytestScene.md` top to bottom on a fresh Play mode boot, then attempted each scenario verb from the WP-PT.1 spec via the dev console. Took notes on what rendered, what triggered, and what was silent.

---

## What I observed

### Worked as expected

- **Setup + Boot:** scene loads cleanly, indicator visible top-left, 15 NPCs spawn within 2s, FPS holds well above gate.
- **Camera pan / rotate / zoom:** smooth, no overshoot.
- **Time control:** pause / ×1 / ×4 / ×16 cycle works via number keys; space toggles pause; sim speed visibly changes.
- **Dev console open / close:** backtick opens the IMGUI panel, backtick closes it. (After ~6 commits of fighting; finally working as of `401da01`.)
- **Performance gate:** sustained ×4 was very smooth; subjective p95 well above 58.

### Felt off / wrong / surprising

- **Camera double-click recenter** does the *opposite* of what the recipe describes — it returns the camera to origin instead of gliding toward the selected NPC. Reads like the binding routes to the "home" verb.
- **Camera input behavior under pause** is inverted from intent: WASD freezes during sim pause but mouse-wheel zoom continues. Per UX bible §1.5 / §2.1, the design intent is the inverse — *time*-stop should preserve full camera/zoom freedom (the player is reviewing the frozen world); *menu*-pause should disable input. Currently no menu pause exists at all; the sim-pause has menu-pause-like behavior on WASD and time-stop-like behavior on zoom.

### Confirmed bugs (filed below)

- BUG-004: PlaytestScene UI panels are not instantiated/wired — Inspector, BuildPalette, EventLog, SaveLoad, Settings, ObjectInspector, RoomInspector, SelectionHaloRenderer, AudioListener all silently absent.
- BUG-005: Camera double-click recenter returns to origin instead of gliding to selected NPC; sim-pause / menu-pause input semantics reversed.
- BUG-006: No audio at all in PlaytestScene (no AudioListener present).
- BUG-007: Dev console IMGUI submit doesn't execute on Enter — typed commands are visible in the input field but pressing Enter produces no output (or errors that the user cannot read).
- BUG-008: Save / load round-trip fails — likely a side-effect of BUG-004 (SaveLoadPanel UI doesn't render so the load button isn't reachable).

### Open questions for Opus / Talon

- Is PlaytestScene meant to be a fresh-from-scratch composition by a Sonnet, or should it have been authored by porting MainScene's hierarchy? Investigation found `MainScene.unity` is currently a stub (no GameObjects); the ECSUnity project's only real scene is PlaytestScene. That changes the calibration — there's no "production scene" to mirror; PlaytestScene IS the production scene right now.
- Several panels (ObjectInspectorPanel, RoomInspectorPanel, BuildPaletteUI, SelectionController, SelectionHaloRenderer) exist as scripts but are not instantiated in PlaytestScene at all. Was this expected (deferred to a follow-up packet) or a WP-PT.0 regression that the first-light recipe missed? The recipe checks "click a chair → object inspector opens" but didn't verify that ObjectInspectorPanel was even *in* the scene to render.

---

## Bugs filed this session

> Each entry below has been appended to `docs/known-bugs.md` with the next monotonic id.

### BUG-004: PlaytestScene UI panels are not instantiated/wired — multiple surfaces silently absent

**Symptom:** Several player-facing surfaces don't render at all in PlaytestScene:
- Selection halo (outline appears but no ground halo)
- Inspector glance / drill / deep on NPC click
- Object inspector on chair click
- Room inspector on empty-floor click
- Build palette on `B` toggle (toggle state changes; nothing visible)
- Event log (no key opens it)
- Save/load panel (no UI; quick-save/load also untestable)
- Settings panel (no soften toggle reachable)

**Repro:** Open PlaytestScene → Play → click an NPC. No inspector. Press `B`. No build palette. Press whatever EventLog binding is meant to be — nothing. Etc.

**Severity:** **High** — the entire player-surface side of the integrated whole is dark; the program's first-light recipe fails on five of ten sections.

**Files relevant (if known):**
- `ECSUnity/Assets/Scenes/PlaytestScene.unity` — missing GameObjects + every wired UIDocument has `_document: {fileID: 0}`
- `ECSUnity/Assets/Scripts/UI/InspectorPanel.cs`, `ObjectInspectorPanel.cs`, `RoomInspectorPanel.cs`, `SaveLoadPanel.cs`, `SettingsPanel.cs`, `TimeHudPanel.cs`, `UI/EventLog/EventLogPanel.cs`, `BuildMode/BuildPaletteUI.cs`
- `ECSUnity/Assets/Scripts/UI/SelectionController.cs`, `UI/SelectionHaloRenderer.cs` — not present in scene
- `ECSUnity/Assets/Scripts/Selection/SelectionManager.cs` — present but selection-event chain to inspector is broken because SelectionController is missing

**Suggested fix wave:** Inline. `WP-FIX-BUG-004-playtest-ui-wiring.md` is the dispatchable spec.

**Workaround:** None — these are core player verbs.

**Discovered in:** PT-001

### BUG-005: Camera double-click recenter returns to origin; pause/zoom semantics inverted

**Symptom:** Two related camera issues:

- **Double-click on an NPC** moves the camera to world origin, not toward the clicked NPC. Recipe expected "camera glides smoothly toward target."
- **Sim-paused state** disables WASD camera pan but allows mouse-wheel zoom. Per UX bible §1.5 (time-stop preserves player's review surface) and §2.1, the intent is full camera+zoom availability under sim-stop; only a *menu* pause (Esc, save dialog open, etc.) should disable camera input.

**Repro:** double-click an NPC → camera flies to origin instead of the NPC. Press space → sim pauses → press WASD → no camera movement; scroll wheel still zooms (inconsistent behavior).

**Severity:** **Medium** — both reduce camera UX but neither breaks core verbs.

**Files relevant (if known):**
- `ECSUnity/Assets/Scripts/Camera/CameraController.cs`
- `ECSUnity/Assets/Scripts/Camera/CameraInputBindings.cs`
- `ECSUnity/Assets/Scripts/Camera/CameraConstraints.cs`

**Suggested fix wave:** Inline. `WP-FIX-BUG-005-camera-pause-and-recenter.md` is the dispatchable spec.

**Workaround:** Camera-pan via single-click-and-drag still works (per recipe) — just don't double-click.

**Discovered in:** PT-001

### BUG-006: No audio at all in PlaytestScene

**Symptom:** With audio on at moderate volume, no footsteps, no chair squeaks, no ambient hum, no NPC speech — total silence. Recipe expects at least 3 distinct sound triggers audible within 30 seconds.

**Severity:** **High** — the SoundTriggerBus shipped in WP-3.2.1 is a load-bearing axiom-level surface (per SRD §8.7 the engine emits triggers, host synthesizes); in PlaytestScene the host-side audio path is entirely silent.

**Repro:** Open PlaytestScene → Play → focus camera on any NPC → wait 30s. No audio.

**Root cause (suspected):** PlaytestScene has no `AudioListener` component on any GameObject. Without an AudioListener (typically on the camera) Unity plays no audio regardless of how many AudioSources exist. The investigation also found the host-side synth that was added in WP-3.2.1 may not be wired to a GameObject in PlaytestScene — needs Sonnet investigation.

**Files relevant (if known):**
- `ECSUnity/Assets/Scenes/PlaytestScene.unity` (camera GameObject lacks AudioListener)
- Whatever script consumes the SoundTriggerBus (search APIFramework for `SoundTriggerBus` consumers)

**Suggested fix wave:** Inline. Bundled into `WP-FIX-BUG-005-camera-pause-and-recenter.md` since both fixes are camera-GameObject scoped.

**Workaround:** None.

**Discovered in:** PT-001

### BUG-007: Dev console IMGUI submit doesn't execute commands on Enter

**Symptom:** Backtick correctly opens the IMGUI dev console panel. Typing produces visible characters in the input field. Pressing Enter does *not* submit the command — no output appears in the history scroll, the command isn't echoed, no ERROR is shown either. Scenario verbs (`scenario kill <name>`, etc.) all silently fail to execute.

**Severity:** **High** — without a working submit, the dev console is read-only-text-entry, which is no console at all. WP-PT.1's entire scenario-verb surface is unreachable.

**Repro:** Press backtick → console opens → type `help` → press Enter → nothing happens. Same for any other command.

**Root cause (suspected):** Two candidate causes that need Sonnet investigation:

1. **IMGUI input focus race.** The OnGUI fallback uses `GUI.SetNextControlName("warden-console-input")` + `GUI.FocusControl(...)` to keep focus, but Unity's IMGUI re-initializes focus per repaint. Enter keystrokes may be consumed by Unity's keyboard control before the panel's KeyDown handler sees them, OR the `Event.current.keyCode == KeyCode.Return` branch may fire correctly but `_savedInput` is empty because the TextField hadn't yet committed its value to the variable on the same event tick.
2. **DevCommandContext not finalised on first submit.** `RefreshContext()` runs on Awake and SetVisible(true). If the first refresh-context happens before EngineHost's MutationApi is non-null, the dispatcher's context has `MutationApi = null` and any command using it returns silently rather than reporting the missing reference.

**Files relevant (if known):**
- `ECSUnity/Assets/Scripts/DevConsole/DevConsolePanel.cs` (OnGUI fallback, lines 380-485)
- `ECSUnity/Assets/Scripts/DevConsole/DevConsoleCommandDispatcher.cs`
- `ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/*.cs` (verb handlers — may swallow exceptions)

**Suggested fix wave:** Inline. `WP-FIX-BUG-007-devconsole-imgui-submit.md` is the dispatchable spec.

**Workaround:** None.

**Discovered in:** PT-001

### BUG-008: Save / load round-trip not testable in PlaytestScene

**Symptom:** Recipe step "save mid-day, load most recent autosave, verify identity" cannot complete because the SaveLoadPanel UI doesn't render (BUG-004) and quick-save / quick-load keybindings (F5/F9 per code) appear to have no effect when pressed.

**Severity:** **High** — save/load is an SRD §8.2 axiom-level commitment.

**Files relevant:** rolls up under BUG-004's UI panel wiring fix; if F5/F9 still don't work after the panel is wired, file as a follow-up bug.

**Suggested fix wave:** Verified as part of `WP-FIX-BUG-004` acceptance.

**Discovered in:** PT-001

---

## Performance notes

- FPS observed: subjectively 58–62, holds even at ×4 sustained.
- Frame stutters: none observed.
- Audio glitches: n/a — no audio at all (BUG-006).
- Memory pressure (subjective): Editor responsive throughout.

---

## Phase 3 surface coverage this session

Tick what was actually exercised. Untouched surfaces are fine — sessions have focus. Adapted symbols: ✅ pass, ❌ fail, ⚠ partial, — not exercised.

**Engine systems:**

- ❌ Life-state — `Choked` (couldn't trigger via console; BUG-007)
- — Life-state — `SlippedAndFell`
- — Life-state — `StarvedAlone`
- ❌ Life-state — `Died` (general) — `scenario kill` doesn't execute (BUG-007)
- — Fainting (Incapacitated → Alive recovery alone)
- — Rescue — Heimlich / CPR / door-unlock (gated on choke working)
- — Bereavement cascade (gated on death triggering)
- ❌ Live mutation — build placement triggers re-pathing (BUG-004 — palette doesn't render)
- ❌ Sound triggers — every kind (BUG-006)
- ❌ Physics — break event (no audio confirmation; throw subverb gated on BUG-007)
- — Chore rotation — refusal observed (would need full sim-day; not exercised)
- — Animation — Eating / Drinking / Working / Crying / CoughingFit / Heimlich
- ❌ Save mid-state, load, verify identity (BUG-008)

**Unity host surface:**

- ✅ Camera — pan / rotate / zoom
- ❌ Camera — double-click recenter (BUG-005)
- — Camera — wall-fade-on-occlusion
- ❌ Selection — single-click glance (BUG-004)
- ❌ Selection — drill (BUG-004)
- ❌ Selection — deep (BUG-004)
- ⚠ Build mode — `B` toggle reflects in code state but no palette renders (BUG-004)
- — Build mode — place wall / door / prop (gated)
- — Build mode — ghost preview valid / invalid tints (gated)
- ✅ Time HUD — pause / ×1 / ×4 / ×16 cycle
- ❌ Event log — open, filter (BUG-004)
- ✅ Dev console — open with backtick
- ❌ Dev console — scenario verbs reachable (BUG-007)
- ❌ Soften toggle — verified reachable (BUG-004)

**Performance gate:**

- ✅ 30-NPCs-at-60-FPS holds — subjectively held above gate at all speeds tested.

---

## Next session focus suggestion

PT-002 should run after `WP-FIX-BUG-004`, `WP-FIX-BUG-005`, and `WP-FIX-BUG-007` merge. Focus: re-run the full first-light recipe to confirm every section now passes; then move to organic gameplay (5 minutes of unscripted play to look for emergent bugs the recipe doesn't cover).

---

## Packets evaluated by this session

- **WP-PT.0** (PlaytestScene composition) — ⚠ PARTIAL ACCEPTANCE: scene boots, perf gate holds, but multiple surfaces missing per BUG-004. The packet's first-light recipe checked structure but not the wiring depth needed to actually render the panels. Recipe revision suggested: each panel section should explicitly verify the panel's GameObject is in the scene hierarchy AND its IMGUI fallback path renders.
- **WP-PT.1** (dev-console scenario verbs) — ❌ RETURN FOR FIX (via BUG-007): the scenario verbs may be correctly implemented but cannot be exercised because the IMGUI submit path doesn't actually dispatch.

---

## Iter 2 notes (after `afd7172` fix bundle)

After Talon pulled `afd7172` (BUG-004/005a/007 fix) and ran the recipe again:

**What improved:**
- Dev console: backtick opens, commands execute, history scrolls. **BUG-007 verified.**
- All earlier "still no console" pain resolved.

**What's still broken (filed below as new BUGs in `known-bugs.md`):**
- **BUG-010** — Inspector + camera glide still don't fire. Outline appears (legacy SelectionManager works); but newer SelectionController never fires events. Root cause: NpcDotRenderer adds `NpcSelectableTag` only, not the unified `SelectableTag` that SelectionController raycasts against. **Fixed in iter-2 commit (this same one).**
- **BUG-011** — Keyboard bleed. Pressing space while typing a command pauses the sim. **Fixed in iter-2 commit** by adding `DevConsolePanel.AnyVisible` static + 4 gate sites (TimeHudPanel, CameraInputBindings, BuildModeController, SaveLoadPanel).
- **BUG-012** — Build mode toggles in code but renders nothing. Deferred to a follow-up packet alongside BUG-001's build-mode-v2 work.
- **BUG-013** — `scenario kill <name>` works but witnesses show no chibi-emotion cues. ChibiEmotionPopulator likely missing from scene. Deferred.

**Iter-2 quality-of-life adds (in this same commit):**
- New `scenario list-npcs` subverb so Talon can discover names without an inspector.
- Save/load commands now print the on-disk path in the response (`%USERPROFILE%\AppData\LocalLow\<Company>\<ProjectName>\Saves\<slot>.json`).
- New `docs/wiki/dev-console-commands.md` — single-page reference Talon can skim mid-session.
- Audio: re-confirmed silent (BUG-009 — host synth listener missing; out of fix scope).

**Pass criteria after iter-2 fix:** still <80% pre-test until Talon retests. Predicted improvements:
- Inspector should now open on click (BUG-010 fix).
- Camera should now glide on double-click (BUG-010 fix unlocks the GlideRequested chain).
- Typing space in console should NOT pause sim (BUG-011 fix).
- `scenario list-npcs` reveals NPC names; subsequent `scenario kill Donna` etc. should work as before.
- `save mytest` prints the file path so Talon knows where saves land.
- Build mode + audio + chibi cues remain dark (deferred).
