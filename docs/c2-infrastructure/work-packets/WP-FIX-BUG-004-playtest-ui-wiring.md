# WP-FIX-BUG-004 — PlaytestScene UI Panel Wiring

> Fixes BUG-004 (`docs/known-bugs.md`). Surfaced by PT-001 (`docs/playtest/PT-001-baseline.md`).

**Track:** 2 (Unity)
**Phase:** Inline fix — playtest-program-driven
**Author:** Opus, 2026-05-02
**Sonnet executor:** assigned by Talon
**Branch:** `sonnet-wp-fix-bug-004`
**Worktree:** `.claude/worktrees/sonnet-wp-fix-bug-004/`
**Timebox:** 90–150 minutes
**Cost envelope:** $0.50–$0.90
**Feel-verified-by-playtest:** YES — every panel touched by this packet must be re-verified in PT-002.
**Surfaces evaluated by next PT-NNN:** inspector glance/drill/deep, build palette under `B`, event log open/filter, save/load round-trip, settings + soften toggle, selection halo, room/object inspector. Re-run the full first-light recipe.
**Build-verified-by-recipe:** NO (no scripting-define / asmdef changes; pure scene + minor scripting).
**Parallel-safe with:** WP-FIX-BUG-005 (camera + audio), WP-FIX-BUG-007 (dev console submit). Different files; no merge conflicts expected.

---

## Goal

Make every UI panel in `Assets/Scenes/PlaytestScene.unity` actually render. Right now five player-facing surfaces are silently dark because the panel GameObjects are either missing from the scene or wired to a UIDocument with `_document: {fileID: 0}` and the IMGUI fallback isn't firing for reasons we need to diagnose.

After this packet, opening PlaytestScene and pressing Play produces a working integrated player surface: clicking an NPC opens the inspector, pressing `B` opens the build palette, the event log is reachable, save/load works, settings are reachable.

---

## Non-goals

- Do not author UXML or PanelSettings assets. The IMGUI fallback path is the target. UXML wiring is a separate future packet (likely WP-PT.NN polish).
- Do not modify `MainScene.unity` (it's currently a stub; not in scope).
- Do not refactor any panel script's API. If a panel's OnGUI fallback has bugs that prevent rendering, fix them in-place but don't restructure the class.
- Do not modify engine code (`APIFramework/`).
- Do not touch dev-console / scenario-verb code (covered by WP-FIX-BUG-007).

---

## Investigation phase (REQUIRED first step)

Before adding GameObjects, the Sonnet must determine *why* existing panels' IMGUI fallbacks aren't firing. The dev console's IMGUI fallback works; the others' don't. There must be a difference.

### Step T1 — Open PlaytestScene in Editor and snapshot the current GameObject hierarchy

Note which of these GameObjects exist and which are missing:

| Expected GameObject | Component(s) | Currently in scene? |
|---|---|---|
| `EngineHost` | EngineHost, SimConfigAsset ref | YES (verified) |
| `Renderers` | RoomRectangleRenderer, NpcDotRenderer | YES (added by SceneBootstrapper at runtime) |
| `Main Camera` | Camera, CameraController | YES |
| `DevConsole` | DevConsolePanel | YES (working as of `401da01`) |
| `InspectorPanel` | InspectorPanel | likely YES with `_document: {fileID: 0}` |
| `EventLog` | EventLogPanel | likely YES with `_document: {fileID: 0}` |
| `SaveLoadPanel` | SaveLoadPanel | likely YES with `_document: {fileID: 0}` |
| `SettingsPanel` | SettingsPanel | likely YES with `_document: {fileID: 0}` |
| `TimeHudPanel` | TimeHudPanel | YES (working) |
| `BuildModeController` | BuildModeController | YES |
| `BuildPaletteUI` | BuildPaletteUI | **probably MISSING** |
| `BuildOverlay`, `GhostPreview`, `PlacementValidator`, `PickupController`, `DoorLockContextMenu` | individual scripts | **partially missing** |
| `ObjectInspectorPanel` | ObjectInspectorPanel | **MISSING** |
| `RoomInspectorPanel` | RoomInspectorPanel | **MISSING** |
| `SelectionController` | SelectionController | **MISSING** |
| `SelectionHaloRenderer` | SelectionHaloRenderer | **MISSING** |
| `AudioListener` (on camera) | AudioListener | **probably MISSING** (covered by BUG-006 / WP-FIX-BUG-005) |

### Step T2 — Diagnose why existing IMGUI fallbacks don't render

For each panel that IS in the scene but doesn't render (Inspector, EventLog, SaveLoad, Settings):

1. Read the panel's `OnGUI()` method. Note the early-return condition.
2. Add temporary `Debug.Log` lines at the top of OnGUI to confirm whether OnGUI runs at all when `_isVisible` is false vs true.
3. Check whether the panel is *trigger-driven* (only opens on a selection event, button press, etc.) — and whether that trigger fires.

Example: `InspectorPanel.OnGUI()` may early-return if a selected entity is null. If `SelectionController` isn't in the scene, no selection event fires, no entity is selected, panel never sets `_isVisible = true`.

This diagnosis informs the right fix: either add the trigger source (SelectionController) or change the panel's gate condition.

### Step T3 — Map fix shape per panel

Output a brief text file in the worktree (`WP-FIX-BUG-004-investigation.md`) listing per-panel:
- Current scene presence (in / out)
- Why IMGUI fallback doesn't render currently
- Fix path: "add GameObject and component" / "wire serialized field X" / "fix OnGUI early-return condition"

Commit this investigation file as the first commit. Then proceed to fix.

---

## Fix paths (apply per panel)

### Path A — Add missing GameObject

For panels not in the scene at all (ObjectInspectorPanel, RoomInspectorPanel, BuildPaletteUI, SelectionController, SelectionHaloRenderer):

1. In Editor, GameObject → Create Empty → name the new GameObject (e.g., `SelectionController`).
2. Add the component (`Component → Add Component → Selection → SelectionController`, or by dragging the script).
3. Wire serialized fields: typically EngineHost reference → drag from Hierarchy. PlayerUIConfig → drag asset from `Assets/Settings/`.
4. Leave `_document` field at None (UIDocument null) so the IMGUI fallback fires.
5. Save scene.

For SelectionHaloRenderer: per investigation, this script `GetComponent<SelectionController>()` — so SelectionHaloRenderer should be on the same GameObject as SelectionController, or add it to the camera or main UI parent.

### Path B — Wire serialized fields on existing GameObjects

For panels in the scene with broken references (e.g., `_host: {fileID: 0}` when it should reference EngineHost):

1. Click the GameObject in Hierarchy.
2. In Inspector, drag EngineHost from Hierarchy into the `_host` slot (or whatever ref is null).
3. Save scene.

### Path C — Fix OnGUI early-return condition

If diagnosis reveals a panel's OnGUI is gated on an unfulfillable condition (e.g., `_isVisible` set only by a selection event from a controller not in scene):

1. In the panel script, add a developer-time fallback: when running in WARDEN mode AND `_isVisible` has been false for >30s of Editor play, allow a manual key (e.g., F1 for inspector) to toggle visibility.
2. **Alternative:** add the missing controller GameObject (Path A) so the natural trigger chain fires.

Prefer Path A over Path C — keep the panels' API unchanged.

---

## Acceptance criteria

### A — All panel GameObjects present in PlaytestScene

A1. Hierarchy after fix contains at minimum: EngineHost, Renderers, Main Camera, DevConsole, InspectorPanel, ObjectInspectorPanel, RoomInspectorPanel, EventLogPanel, SaveLoadPanel, SettingsPanel, TimeHudPanel, BuildModeController, BuildPaletteUI, SelectionController, SelectionHaloRenderer.

A2. Each GameObject has its primary script component attached. Each script's serialized references (EngineHost, PlayerUIConfig, etc.) point to actual scene-resident GameObjects or asset references — no `{fileID: 0}` for fields that must be wired.

### B — Each panel renders in PlaytestScene Play mode

B1. **Click an NPC:** halo appears under the NPC (cyan or whatever the default per UX bible §2.2); outline appears on the NPC; InspectorPanel renders with glance tier (5 fields).

B2. **Click drill button in InspectorPanel:** drill tier opens (drives, willpower, schedule, task, stress, mask).

B3. **Click again (deep button):** deep tier opens (full vectors, relationships, memory).

B4. **Click off (empty floor):** RoomInspectorPanel renders for the clicked room.

B5. **Click an object (chair, desk, fridge):** ObjectInspectorPanel renders with name, kind, position.

B6. **Press `B`:** world tints; BuildPaletteUI appears on the right; ghost preview functions; place a wall; an NPC re-paths around it.

B7. **Open Event Log** (whatever keybind / icon lives in TimeHudPanel): EventLogPanel renders with reverse-chronological list. Filter by NPC works.

B8. **Open Save/Load** (presumably an icon or hotkey defined in SaveLoadPanel): panel renders with file picker. F5 quick-saves; F9 quick-loads.

B9. **Open Settings** (presumably reachable from TimeHudPanel or pause menu): SettingsPanel renders with soften toggle, color-blind palette, volume sliders.

### C — IMGUI fallbacks render correctly

C1. Each panel's OnGUI fallback uses a coherent layout (no overlapping text, readable at 1280×720 default screen resolution).

C2. Pressing Esc or whatever close-binding the panel uses dismisses it cleanly.

C3. No NullReferenceException in the Console window when interacting with any panel.

### D — xUnit suite green

D1. `dotnet test` from repo root: all existing tests pass. No regressions.

D2. If new GameObjects introduce new test scenarios (e.g., InspectorPanel needs a fresh integration test for the IMGUI path), add them to `Tests/Play/`.

---

## Files likely to modify

- `ECSUnity/Assets/Scenes/PlaytestScene.unity` (primary — adds GameObjects, wires references)
- Possibly minor tweaks to panel scripts for OnGUI gate-condition fixes (Path C)

## Dependencies

- **Hard:** none. Can dispatch immediately against current `staging` (commit `401da01`).
- **Soft:** WP-FIX-BUG-005's AudioListener-on-camera change is in the same scene file; if dispatched in parallel, expect a tiny `PlaytestScene.unity` merge conflict on the camera GameObject's component list. Resolution: keep both component additions.

## Completion protocol

### Visual verification: REQUIRED

This packet's acceptance IS the visual rendering of every panel in PlaytestScene. The Sonnet executor must:

0. **Worktree pre-flight.** Confirm `.claude/worktrees/sonnet-wp-fix-bug-004/` on branch `sonnet-wp-fix-bug-004` based on `origin/staging` tip.
1. Run the investigation phase (T1 / T2 / T3). Commit `WP-FIX-BUG-004-investigation.md` first.
2. Apply fixes per Path A / B / C as appropriate per panel.
3. Open PlaytestScene in Editor; press Play; manually run each acceptance check in §B.
4. Run `dotnet test` — must be green.
5. Stage changes; commit; push.
6. Final commit message line: `READY FOR VISUAL VERIFICATION — re-run docs/playtest/PT-001-baseline.md recipe`.
7. Talon then runs PT-002 evaluating the fixes.

### Feel-verified-by-playtest

**YES.** PT-002 is the formal acceptance — every panel listed above must render and respond as described. Failures file as new BUG-NNN.

### Cost envelope

Target $0.50–$0.90. Timebox 90–150 minutes. Most of the work is Editor scene composition (drag-and-drop GameObjects, wire references) which is mechanical once investigation completes.

### Self-cleanup on merge

After PR merges:
1. `git grep -l "WP-FIX-BUG-004\|BUG-004" docs/c2-infrastructure/work-packets/ | grep -v _completed`
2. If no pending dependents: `git rm docs/c2-infrastructure/work-packets/WP-FIX-BUG-004-playtest-ui-wiring.md` in the same commit. Add `Self-cleanup: spec file deleted, no pending dependents.`
3. Update `docs/known-bugs.md`: append `**Resolution:** Fixed in WP-FIX-BUG-004 (commit <SHA>). <root cause summary>.` to BUG-004's entry.
