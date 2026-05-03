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

## PlaytestScene — UI Wiring

### BUG-004: PlaytestScene UI panels are not instantiated/wired — multiple surfaces silently absent

**Symptom:** Several player-facing surfaces don't render at all in `Assets/Scenes/PlaytestScene.unity`:
- Selection halo (outline appears but no ground halo)
- Inspector glance / drill / deep on NPC click
- Object inspector on chair click
- Room inspector on empty-floor click
- Build palette on `B` toggle (toggle state changes; nothing visible)
- Event log (no key opens it; no panel renders)
- Save/load panel (no UI; quick-save/load also untestable)
- Settings panel (soften toggle not reachable)

**Severity:** **High** — the entire player-surface side of the integrated whole is dark.

**Repro:**
1. Open PlaytestScene → Play.
2. Click an NPC. No inspector appears. Outline shows on the NPC but no halo on the ground tile beneath them.
3. Press `B`. Build mode toggles in code (visible in Inspector → BuildModeController) but no palette renders.
4. Click an empty floor tile. No room inspector.
5. Press whatever EventLog binding is meant to be — nothing opens.
6. Try to save the game — no UI; F5 quick-save also produces no observable feedback.

**Root cause (investigated 2026-05-02):** Two stacked issues in PlaytestScene's composition:

1. **Several panel GameObjects are simply not in the scene.** `ObjectInspectorPanel`, `RoomInspectorPanel`, `BuildPaletteUI`, `SelectionController`, and `SelectionHaloRenderer` exist as scripts in `Assets/Scripts/` but no GameObject in PlaytestScene carries those components. The dev console works because its GameObject (`DevConsole`, fileID 900000001) IS in the scene; the others were never added during WP-PT.0's scene composition.
2. **Where panels ARE in the scene** (InspectorPanel, EventLogPanel, SaveLoadPanel, SettingsPanel, TimeHudPanel, BuildModeController), the `_document` UIDocument reference is `{fileID: 0}` (unwired). Every panel script *does* have an IMGUI OnGUI() fallback for this case (verified: each has a 100-200-line OnGUI block that fires when `_document == null`). The fallbacks should activate but in PlaytestScene they don't render, suggesting either the GameObject is inactive at runtime or the OnGUI fallback's gate-condition isn't being met.

The selection-cue cascade is illustrative: `OutlineRenderer` is wired (outline appears on click) but `SelectionHaloRenderer` isn't in the scene at all (no halo). `InspectorPanel` is in the scene but its OnGUI fallback only fires when `SelectionController.SelectionChanged` event is observed — and `SelectionController` isn't in the scene to fire it. Each missing piece breaks the chain.

**Suggested fix wave:** Inline. `WP-FIX-BUG-004-playtest-ui-wiring.md` (drafted 2026-05-02) is the dispatchable spec — add the missing GameObjects, wire EngineHost references, confirm IMGUI fallback paths fire.

**Workaround:** None — these are core player verbs. Use Editor Inspector to manually trigger systems for now (extremely limited).

**Discovered in:** PT-001 (`docs/playtest/PT-001-baseline.md`).

**Resolution:** Fixed 2026-05-02 by Opus directly (no Sonnet dispatch needed). Five sub-fixes in this commit:
1. Created `Assets/Settings/DefaultPlayerUIConfig.asset` (the missing ScriptableObject every panel's `_uiConfig` field needed) with GUID `2d6a716699994ddba9740130bd462600`.
2. Replaced stale serialized fields on the existing `SelectionController` (PlaytestScene fileID 1300000004) and `SelectionHaloRenderer` (fileID 1300000003) with the current field shape (`_host`, `_uiConfig`, `_camera`, `_selectableLayer`, `DoubleClickInterval`).
3. Added new `ObjectInspectorPanel` (fileID 1700000001) and `RoomInspectorPanel` (fileID 1800000001) GameObjects to PlaytestScene with `_host` wired and `_document: {fileID: 0}` (forces IMGUI fallback path).
4. Patched all four other `_uiConfig: {fileID: 0}` references (TimeHudPanel, SettingsPanel, InspectorPanel, NotificationPanel) to point at the new asset.
5. Added runtime `FindObjectOfType<SelectionController>()` subscription in `Start()` of `InspectorPanel`, `ObjectInspectorPanel`, `RoomInspectorPanel` so each receives `SelectionChanged` events without needing a scene-authored UnityEvent hook (which is hard to express in hand-authored YAML).

---

## Camera

### BUG-005: Camera double-click recenter goes to origin; sim-pause / menu-pause input semantics inverted

**Symptom:** Two related camera behaviors are wrong:

- **Double-click on an NPC** moves the camera to world origin instead of gliding toward the clicked NPC. The recipe expected "camera glides smoothly toward target" per UX bible §2.2 ("Double-click recenters the camera with a smooth glide toward the target").
- **Under sim-pause** (space bar), WASD camera pan is disabled but mouse-wheel zoom still works. This is inverted from the UX bible's intent: time-stop should preserve full camera/zoom freedom (the player is reviewing a frozen world), and only a *menu* pause (Esc menu, save dialog open, etc.) should disable camera input. Currently no menu pause exists; the sim-pause has menu-pause-like behavior on WASD and time-stop-like behavior on zoom.

**Severity:** **Medium** — both reduce camera UX but neither breaks a core verb.

**Repro:**
1. Click an NPC. Selection registers (outline appears). Double-click that NPC. Camera flies to (0, 0, 0), not toward them.
2. Press space → sim pauses → press WASD → no camera movement. Scroll wheel → still zooms (inconsistent).

**Files relevant (if known):**
- `ECSUnity/Assets/Scripts/Camera/CameraController.cs`
- `ECSUnity/Assets/Scripts/Camera/CameraInputBindings.cs`
- `ECSUnity/Assets/Scripts/Camera/CameraConstraints.cs`
- Whatever provides the "glide-to-target" verb — search for `GlideRequested`, `GlideTo`, `RecenterOn`, etc.

**Suggested fix wave:** Inline. `WP-FIX-BUG-005-camera-pause-and-recenter.md` (drafted 2026-05-02) is the dispatchable spec.

**Workaround:** Single-click drag still pans; manual zoom and pan reach any NPC.

**Discovered in:** PT-001.

**Resolution (BUG-005a — double-click recenter):** Fixed 2026-05-02. `CameraController.cs` now exposes a public `GlideTo(Vector3)` method and subscribes to `SelectionController.GlideRequested` in `OnEnable()` (with `FindObjectOfType<SelectionController>()` fallback for prefab-instance scenes). The polling double-click in `HandleRecenter()` was deleted; F-key recenter to office centre (15, 0, 11) preserved.

**Resolution (BUG-005b — pause input semantics):** Closed as N/A 2026-05-02. Verified by direct inspection: zero matches for `Time.timeScale`, `IsPaused`, or `paused` in `Assets/Scripts/Camera/`. No pause gate exists in the codebase. Talon's PT-001 observation may have been a different symptom. Will reassess if PT-002 surfaces it again.

---

## Audio

### BUG-006: No audio at all in PlaytestScene

**Symptom:** With audio on at moderate volume, focusing the camera on any NPC produces no footsteps, no chair squeaks, no fluorescent buzz, no ambient hum, no NPC speech fragments — total silence. The recipe's expectation is "at least 3 distinct sound triggers heard" within 30 seconds; observed: zero.

**Severity:** **High** — `SoundTriggerBus` is a Phase 3.2.1 axiom-level surface. Per SRD §8.7 the engine emits triggers, the host synthesises. The host-side path in PlaytestScene is silent.

**Repro:** Open PlaytestScene → Play → focus camera on any NPC → wait 30s. No audio.

**Root cause (suspected — needs Sonnet investigation):** PlaytestScene almost certainly lacks an `AudioListener` component on any GameObject. Without an AudioListener (typically attached to the camera) Unity plays no audio regardless of how many AudioSources exist or how the SoundTriggerBus is wired. The investigation also surfaced no obvious script that subscribes to `SoundTriggerBus` and synthesises sounds in PlaytestScene; the host-side synth from WP-3.2.1 may not have a scene-resident GameObject.

**Files relevant (if known):**
- `ECSUnity/Assets/Scenes/PlaytestScene.unity` (camera GameObject lacks AudioListener)
- Whatever script consumes `SoundTriggerBus` and produces AudioSource clips — search APIFramework for `SoundTriggerBus` consumers and ECSUnity for any host-side `SoundTriggerSynth*.cs` or similar.

**Suggested fix wave:** Inline. Bundled into `WP-FIX-BUG-005-camera-pause-and-recenter.md` since both fixes touch the camera GameObject (AudioListener typically lives there).

**Workaround:** None.

**Discovered in:** PT-001.

**Resolution:** Rescoped 2026-05-02. Investigation found `CameraRig.prefab:68` already has an `AudioListener` component, so the AudioListener-on-camera assumption was wrong. The actual issue is that **no MonoBehaviour anywhere in `Assets/Scripts/` subscribes to `SoundTriggerBus.Subscribe()`** — the engine emits sound triggers to a registry that is permanently empty on the host side. The host-side audio synthesis layer (SoundTriggerKind → AudioClip mapping, pooled AudioSources, falloff model) was never built. This is a missing feature, not a wiring bug. Refiled as **BUG-009** (host-side audio synthesis listener) in this same ledger; out of PT-001 fix scope.

---

## Dev Console

### BUG-007: Dev console IMGUI submit doesn't execute commands on Enter

**Symptom:** Backtick correctly opens the IMGUI dev console panel (after BUG-002/BUG-003's resolutions). Typing produces visible characters in the input field. Pressing Enter does not submit the command — no echo in history, no output, no ERROR. The console becomes effectively read-only-text-entry.

**Severity:** **High** — without working command submit, WP-PT.1's entire scenario-verb surface is unreachable.

**Repro:**
1. Press Play.
2. Press backtick. Console panel opens at bottom of Game view.
3. Type `help` and press Enter. Nothing appears in the history scroll. The input field clears (or doesn't — variable across attempts).
4. Same for any other command including `scenario`, `force-kill <name>`, etc.

**Root cause (suspected — needs Sonnet investigation):** Two candidate causes:

1. **IMGUI input focus / event race.** OnGUI's KeyDown handler fires before the TextField's value is committed to `_savedInput` for the same event tick. Or Unity's IMGUI keyboard control is consuming Enter before the panel's switch-statement reaches it. Solution likely involves moving the Enter-handling outside the GUILayout.BeginHorizontal block, or using `Event.current.character == '\n'` instead of `Event.current.keyCode == KeyCode.Return`.
2. **DevCommandContext race.** `RefreshContext()` runs on Awake and SetVisible(true). If MutationApi is null on the first refresh (EngineHost may not have published it yet), the dispatcher's context has `MutationApi = null` and any command that touches it returns silently rather than reporting the missing reference. Specifically the scenario subverbs (which all need MutationApi) would fail this way without producing visible error output.

**Files relevant:**
- `ECSUnity/Assets/Scripts/DevConsole/DevConsolePanel.cs` (OnGUI fallback, lines ~380-485)
- `ECSUnity/Assets/Scripts/DevConsole/DevConsoleCommandDispatcher.cs`
- `ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/*.cs` (especially the `MutationApi` accesses; verify they fail loudly, not silently)

**Suggested fix wave:** Inline. `WP-FIX-BUG-007-devconsole-imgui-submit.md` (drafted 2026-05-02) is the dispatchable spec.

**Workaround:** None.

**Discovered in:** PT-001.

**Resolution:** Fixed 2026-05-02 by Opus directly. Root cause confirmed: the focused IMGUI `TextField` consumes `KeyCode.Return` on the same OnGUI tick for its own internal newline handling, so the `case KeyCode.Return:` branch in `DevConsolePanel.OnGUI()` never fired for unmodified Enter. Standard IMGUI fix applied: switched the submit branch to `Event.current.character == '\n'` (which arrives in a follow-up KeyDown event after the TextField commits its value to `_savedInput`). KeypadEnter is preserved via `keyCode` because it isn't consumed by the TextField the same way. See `DevConsolePanel.cs:451-484`.

---

## Save / Load

### BUG-008: Save / load round-trip not testable in PlaytestScene

**Symptom:** Recipe step "save mid-day, load most recent autosave, verify identity" cannot complete because the SaveLoadPanel UI doesn't render. Quick-save / quick-load via F5 / F9 keybindings appear to have no effect when pressed.

**Severity:** **High** — save/load is an SRD §8.2 axiom commitment. Currently untestable, not necessarily broken.

**Repro:** Open PlaytestScene → Play → press F5. No observable confirmation. No file produced (verify by checking save directory). Press F9. No load.

**Root cause:** Probably rolls up under BUG-004 (SaveLoadPanel's OnGUI fallback should render the dialog but doesn't). If F5/F9 keybindings still don't work after BUG-004's UI fix lands, that indicates a separate issue with the SaveLoadPanel input handlers.

**Suggested fix wave:** Verified as part of `WP-FIX-BUG-004` acceptance. If keybinding issue persists post-fix, file as a follow-up bug.

**Discovered in:** PT-001.

---

## Audio (Feature)

### BUG-009: Host-side audio synthesis listener missing — engine emits sounds to a void

**Symptom:** PlaytestScene is silent during gameplay — no footsteps, chair squeaks, fluorescent buzz, ambient hum, or NPC speech fragments. (This was originally reported as BUG-006 "no audio at all"; investigation showed the AudioListener is correctly present on `CameraRig.prefab` so this is not a wiring bug.)

**Severity:** **Medium** — game is fully playable visually; audio is a feel-level surface that the bibles commit to (UX bible §3.7) but that doesn't gate any verification recipe. Promote to High when shipping anything player-facing.

**Repro:** Open PlaytestScene → Play → focus camera on any NPC → wait 30s. Total silence.

**Root cause (verified):** `APIFramework/Systems/Audio/SoundTriggerBus.cs:24` iterates a `_subscribers` list. The engine emits `SoundTriggerKind` events (Cough, ChairSqueak, BulbBuzz, Footstep, SpeechFragment, etc.) into the bus correctly — but no MonoBehaviour anywhere in `ECSUnity/Assets/Scripts/` calls `SoundTriggerBus.Subscribe()`. The subscriber registry is permanently empty. The engine emits to a void.

**What's missing:**
- A host-side MonoBehaviour (suggested name: `SoundTriggerHost` or `AudioSynthHost`) that subscribes to `SoundTriggerBus` on Awake, holds a `SoundTriggerKind → AudioClip` lookup table (likely a ScriptableObject catalog like `Assets/Settings/DefaultSoundCatalog.asset`), and on each event resolves the clip + spawns a pooled `AudioSource` at the trigger's world position to play it.
- Per-archetype voice profiles for `SpeechFragment` (Phase 4.1.0 territory — defer).
- Falloff model: trigger volume should attenuate with distance from camera focus per UX bible §3.7 ("camera-proximity attenuation").
- Pool of ~16 AudioSources that round-robin to avoid GameObject churn.

**Files relevant:**
- `APIFramework/Systems/Audio/SoundTriggerBus.cs` — subscription API exists; no consumer
- `ECSUnity/Assets/Scripts/Animation/AnimationSoundTriggerEmitter.cs` — engine-side emitter (works correctly; no fix needed here)
- New file expected: `ECSUnity/Assets/Scripts/Audio/SoundTriggerHost.cs` (or similar)
- New asset expected: `ECSUnity/Assets/Settings/DefaultSoundCatalog.asset` (SoundTriggerKind → AudioClip map)

**Suggested fix wave:** Future feature packet `WP-PT.NN-audio-host-listener` or fold into Phase 4.1.0 (per-archetype voice profiles), since the catalog and pooling work overlap. Out of PT-001 fix scope.

**Workaround:** None.

**Discovered in:** PT-001 (originally as BUG-006); rescoped to BUG-009 on 2026-05-02 after investigation surfaced the missing-feature shape.

---

## PlaytestScene — Iter 2 findings

> Surfaced by PT-001-iter-2 (after the BUG-004/005a/007 commit `afd7172`). Talon re-ran the recipe; console worked but selection chain was still broken.

### BUG-010: NPCs have NpcSelectableTag, not SelectableTag — SelectionController can't find them

**Symptom:** After BUG-004's wiring fix landed, clicking an NPC produced an outline (legacy SelectionManager fired) but no inspector opened, and double-clicking still didn't glide the camera. The newer SelectionController (and everything that subscribes to it: InspectorPanel, ObjectInspectorPanel, RoomInspectorPanel, CameraController) never received SelectionChanged or GlideRequested events.

**Severity:** **High** — selection is the primary player verb; without it the entire inspector and camera-glide UX is dark.

**Root cause:** `NpcDotRenderer.CreateNpcView()` adds `NpcSelectableTag` (older API used by `SelectionManager`) but **not** `SelectableTag` (newer unified API used by `SelectionController`, `BuildModeController`, `DoorLockContextMenu`, `ChibiEmotionPopulator`, etc.). They're two separate MonoBehaviour classes with no inheritance. SelectionController.Update raycasts and calls `hit.collider.GetComponentInParent<SelectableTag>()` — returns null for NPCs because the NPC GameObject only carries `NpcSelectableTag`. The OutlineRenderer subscribes to SelectionManager (via NpcSelectableTag) so the outline still appears, masking the deeper failure.

**Repro:**
1. Open PlaytestScene → Play → click an NPC dot.
2. Outline appears on the NPC (legacy path works).
3. Inspector does NOT slide in (newer SelectionController never fires SelectionChanged).
4. Double-click NPC: camera does not glide (CameraController subscribed to SelectionController.GlideRequested — same chain).

**Files relevant:**
- `ECSUnity/Assets/Scripts/Render/NpcDotRenderer.cs:167` — adds NpcSelectableTag only
- `ECSUnity/Assets/Scripts/UI/SelectionController.cs:72` — looks for SelectableTag via GetComponentInParent
- `ECSUnity/Assets/Scripts/BuildMode/SelectableTag.cs` — the unified class
- `ECSUnity/Assets/Scripts/Selection/NpcSelectableTag.cs` — the older NPC-specific class

**Resolution:** Fixed in this commit (PT-001-iter-2 fix bundle). `NpcDotRenderer.CreateNpcView` now adds **both** `NpcSelectableTag` (for SelectionManager / OutlineRenderer compat) AND `SelectableTag` (for SelectionController + downstream subscribers). Each carries the same EntityId; SelectableTag also carries the DisplayName. Both selection systems work in parallel until a future cleanup unifies them.

**Discovered in:** PT-001-iter-2 (`docs/playtest/PT-001-baseline.md` — to be appended with iter-2 notes).

---

### BUG-011: Console keyboard bleed — game hotkeys still fire while console is open

**Symptom:** With the dev console open, pressing space pauses the sim; pressing F recenters the camera; pressing 1/2/3 changes time scale; pressing F5/F9 quick-saves/loads. The keys also reach the IMGUI TextField correctly, but the parallel keybindings in TimeHudPanel / CameraInputBindings / SaveLoadPanel fire too.

**Severity:** **Medium** — annoying but workaround-able (don't use those keys while typing). Becomes High during scenario testing because typing `set-time dusk` includes a space which pauses the sim mid-command.

**Root cause:** Each keyboard handler polls Input.GetKeyDown / Keyboard.current.* independently. Nothing tells them "the console has focus, suppress your hotkeys."

**Resolution:** Fixed in this commit. `DevConsolePanel` now exposes a static `AnyVisible` property that flips to true on `SetVisible(true)`. Five sites added an early-return gate on `DevConsolePanel.AnyVisible` (each guarded with `#if WARDEN` so RETAIL builds don't reference the WARDEN-only DevConsolePanel type):

- `TimeHudPanel.Update` — gates space-pause + 1/2/3 time-scale
- `CameraInputBindings.RecenterPressed` — gates F-recenter (also wraps Input.GetKeyDown in try/catch for activeInputHandler=1 projects)
- `BuildModeController.Update` — gates B-toggle and all build-mode interaction
- `SaveLoadPanel.Update` — gates F5/F9

**Mouse interactions still pass through** intentionally — clicking outside the console panel area should still select / interact. If that proves problematic later, file as a follow-up.

**Discovered in:** PT-001-iter-2.

---

### BUG-012: Build mode toggles state but no visible palette

**Symptom:** Pressing `B` flips the BuildModeController's `_isBuildMode` flag (visible in Inspector at runtime), but no build palette appears, no world tint, no ghost preview. The build-mode UX is functionally unreachable.

**Severity:** **High** — build mode is one of the four core player verbs.

**Root cause (suspected — needs investigation packet):** BuildModeController has six unwired serialized references in PlaytestScene: `_pickup`, `_doorLock`, `_config`, `_camera`, `_dragHandler`, `_catalog` (verified via direct scene-file inspection during the BUG-004 work). When `SetBuildMode(true)` runs, it tries to call `_palette?.SetVisible(true)` etc. — most calls are null-safe but the cascade of unwired refs means no actual UI activates. Possibly also missing GameObjects (BuildOverlay, GhostPreview, PlacementValidator may be partially present, partially absent).

**Suggested fix wave:** Defer to a follow-up packet `WP-FIX-BUG-012-buildmode-wiring.md` (to be authored). Build mode polish is intentionally out of scope for the BUG-004 fix per the kickoff plan; bundle into the future build-mode-v2 packet alongside BUG-001's stacking-prop fix.

**Discovered in:** PT-001-iter-2.

---

### BUG-013: Bereavement cascade fires but no chibi-emotion cues render on witnesses

**Symptom:** `scenario kill <npc>` correctly transitions the NPC to Deceased and the engine emits `BereavementWitnessed` events for nearby NPCs. But witnesses show no visual response — no chibi-emotion cues (sweat drop / tear / red face) appear above their heads. No log entries scroll into the event log either (gated on EventLogPanel rendering).

**Severity:** **Medium** — engine logic is correct (verifiable via `inspect <witness>`'s drive vector); only the visualization is missing.

**Root cause (suspected):** `ChibiEmotionPopulator` (`Assets/Scripts/Render/ChibiEmotionPopulator.cs`) is responsible for rendering chibi cues by iterating `ChibiEmotionSlot` GameObjects and reading the engine's mood/emotion state. PlaytestScene likely doesn't have a ChibiEmotionPopulator GameObject — it was missing from the scene-composition pass that landed PlaytestScene initially.

**Suggested fix wave:** Defer. Bundle into the same future build-mode-v2 / panel-polish packet that addresses BUG-012, OR roll up under a follow-up "PlaytestScene complete render-chain" packet.

**Discovered in:** PT-001-iter-2.

---
