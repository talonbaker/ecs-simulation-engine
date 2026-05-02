# WP-PT.0 — Unified Playtest Scene

**Track:** 2 (Unity)
**Phase:** Playtest Program (parallel to phase development; see `docs/PLAYTEST-PROGRAM-KICKOFF-BRIEF.md`)
**Author:** Opus, 2026-05-01
**Sonnet executor:** assigned by Talon
**Branch:** `sonnet-wp-pt.0`
**Worktree:** `.claude/worktrees/sonnet-wp-pt.0/`
**Timebox:** 90–150 minutes
**Cost envelope:** $0.40–$0.70
**Feel-verified-by-playtest:** YES — this packet *is* the substrate the program runs on; the first PT-NNN session (PT-001) IS its feel acceptance.
**Surfaces evaluated by PT-001:** every Phase 3 surface listed under "Engine systems exercised" + "Unity host surface exercised" below.

---

## Goal

Ship `ECSUnity/Assets/Scenes/PlaytestScene.unity` — a single playable scene that exercises every Phase 3 surface through normal play. Talon opens the scene, presses play, and uses the sim as a player would in a final-shipped state-of-development build. **Not a feature demo. Not a test harness with twelve buttons. A playable office.**

The scene is the substrate the Playtest Program (`docs/playtest/README.md`) runs on, going forward.

---

## Non-goals

- Do not modify `Assets/Scenes/MainScene.unity`. MainScene is the production / phase-development scene; PlaytestScene is the verification surface. They evolve on separate clocks.
- Do not author new visual primitives. Every renderer, controller, prefab, and panel composed here has already shipped through the sandbox protocol or the 3.1.x scaffold + fixes.
- Do not add new dev-console commands. Scenario verbs ship in WP-PT.1, gated on this packet merging.
- Do not edit the existing `office-starter.json` world definition. Author a new `playtest-office.json` (or equivalent) if a different seeded layout is needed.
- Do not modify the `SoundTriggerBus` or any engine-side substrate. Compose; don't deepen.

---

## Rationale for protocol exception (Rule 3 of UNITY-PACKET-PROTOCOL.md)

Rule 3 forbids modifying live engine scenes without packet-level rationale. This packet creates a **new** live engine scene (`PlaytestScene.unity`) — the rationale follows:

1. The unified playtest surface cannot exist as a sandbox scene (Rule 2's Phase A) — sandbox scenes by definition contain "no engine, no NPCs, no `WorldStateDto`" and exercise one primitive in isolation. The Playtest Program is precisely the integrated-whole verification surface that sandbox scenes can't be.
2. The protocol's anti-pattern was *bundling* eight unrelated visual primitives into MainScene without atomic verification. This packet does not do that — every primitive composed here is already validated. The composition itself is the new surface.
3. Creating a separate scene (vs extending MainScene) preserves Rule 3's spirit: MainScene stays clean for Phase 4 development; the playtest seeding profile (pre-loaded stains, biased chore rotations, faked bereavement histories) does not contaminate production-scene work.
4. Talon's pre-merge first-light recipe (below) is more rigorous than a typical Track 2 recipe — every Phase 3 surface gets a 30-second-per-item check. Failures file as `BUG-NNN` against the appropriate fix wave; they do not require Sonnet rework.

---

## Acceptance criteria

### A — Scene exists, boots, and runs

A1. New file at `ECSUnity/Assets/Scenes/PlaytestScene.unity`. Listed in `ProjectSettings/EditorBuildSettings.asset` after MainScene.

A2. Opening the scene and pressing Play in the Editor:
- Loads without compile errors, NullReferenceExceptions, or missing-script warnings.
- The engine ticks (`EngineHost` is wired and active).
- The `WorldStateProjectorAdapter` produces frames; `RoomRectangleRenderer` and `NpcDotRenderer` (or silhouette renderer) draw.
- A `PlaytestSceneIndicator` HUD overlay (top-left, dim) shows the literal text `PLAYTEST SCENE — WARDEN BUILD`. (Compiled out under non-WARDEN.)
- FPS gauge (FrameRateMonitor existing component) holds ≥ 60 with the seeded NPC count.

A3. Quitting Play mode returns to Edit mode cleanly with no leftover state, no stuck dispatchers, no orphaned coroutines.

### B — Composes existing surfaces (do not reimplement)

B1. **Camera:** `CameraRig.prefab` placed in scene; `CameraController` Inspector-tuned per the values that `WP-3.1.S.0-INT` settled on for MainScene.

B2. **Selection + inspector:** `SelectionManager` + `SelectionHaloRenderer` (default cue per UX bible §2.2 v0.1) + `InspectorPanel` (three-tier glance / drill / deep) all wired. Single-click an NPC opens glance; click drill button opens drill; click again opens deep. Click off closes.

B3. **Build mode:** `BuildModeController` + `BuildPaletteUI` + `GhostPreview` + `PlacementValidator` wired. `B` toggles into build mode; world tints; palette appears; placement works; `Esc`/`B` exits.

B4. **Time HUD:** `TimeHudPanel` wired. Pause / ×1 / ×4 / ×16 cycle works via clicks and via number keys 1–4 (+ space for pause).

B5. **Event log:** `EventLogPanel` reachable via icon (or a default keybind — pick one and document in the recipe). CDDA-style, filterable per UX bible §3.3.

B6. **Dev console:** `DevConsolePanel` wired, opens with backtick (`~`). Existing commands available. Scenario verbs not yet present (WP-PT.1 ships them).

B7. **Sound bus:** `SoundTriggerBus` consumer hooked up; engine-emitted triggers play through the existing host synthesiser (or stub synth — whichever 3.2.1 left in place).

B8. **Save/load panel:** `SaveLoadPanel` reachable. Save names default to `<weekday> Day N` per UX bible §3.4.

B9. **Settings / soften toggle:** `SettingsPanel` reachable; soften toggle present and functional per UX bible §4.6.

### C — Seeded content exercises every Phase 3 surface

C1. **NPC roster:** 15 NPCs spawned across all seven archetypes (per `archetypes/archetype-*.json`). The roster must include at least one NPC each of: Old Hand, Newbie, Climber, Cynic, Vent, Burnout, Dreamer (or whichever names match the actual seven archetype JSONs in `docs/c2-content/archetypes/`). Names assigned per Phase 2's per-NPC name surface (Donna, Frank, etc.).

C2. **Office layout:** loads via `playtest-office.json` (new, sibling to `office-starter.json` in `ECSUnity/Assets/StreamingAssets/` and `docs/c2-content/world-definitions/`) — single-floor, ~30×20 tiles, with at minimum:
- Common area with desks (≥ 8 cubicles)
- Kitchen with microwave + fridge
- Bathroom (men's + women's, lockable doors)
- Manager's-office room (per UX bible §1.1 / §3.4 — the candidate notification carrier home)
- Storage / supply closet (the basement-stand-in for hazard ambient)

C3. **Hazards / objects pre-seeded** so all four narrative deaths are *reachable* through normal play (not pre-triggered):
- Food items in kitchen (microwave-cooked candidates) — choking-on-food substrate.
- Stains on at least 3 floor tiles (one in kitchen, one in bathroom, one in main aisle) — slip substrate. (Scenario verbs in WP-PT.1 will let Talon seed more on demand.)
- A door in the bathroom that locks per `LockedInComponent` — lockout substrate.
- Several `BreakableComponent`-tagged props on desks (a coffee mug, a glass, a ceramic photo frame) — physics substrate.

C4. **Chore rotation pre-armed:** `ChoreRotationInitializerSystem` runs at spawn; week-counter starts on day 1; per-archetype acceptance bias seeded from the existing `archetype-chore-acceptance.json`. (Donna is biased against microwave duty; the system will produce her grumble-and-refuse organically over a few in-game days.)

C5. **Per-archetype tuning JSONs all loaded:** verify in Editor that every JSON under `docs/c2-content/archetypes/` is present and ingested. (Existing engine bootstrap should do this — the test is that nothing complains at scene load.)

C6. **Save/load round-trip works** in this scene: pause mid-day, save, reload, scene state matches.

### D — First-light test recipe

The recipe document ships at `Assets/Scenes/PlaytestScene.md` (sibling to the .unity file, like sandbox scenes have their `.md` siblings in `_Sandbox/`). Talon runs it before merge. The recipe is rigorous — every Phase 3 surface gets a 30-second-per-item check.

The recipe content is specified in §"First-light recipe (ships as Assets/Scenes/PlaytestScene.md)" below; Sonnet authors that file verbatim with the listed checks.

### E — xUnit tests where applicable

E1. New test file `ECSUnity/Assets/Tests/Edit/PlaytestSceneSeederTests.cs`:
- Verifies the playtest world-definition JSON parses without error and produces the expected NPC count, room count, hazard count.
- Verifies the seeder script's serialized `[SerializeField]` defaults are sensible (NPC count > 0, all archetypes represented).

E2. New test file `ECSUnity/Assets/Tests/Play/PlaytestSceneSmokeTests.cs`:
- Loads PlaytestScene in test mode.
- Ticks the engine for 60 simulated seconds.
- Asserts: no exceptions, NPC count stays at C1's value, FPS gauge ≥ 60 at p95, all renderers active.

E3. Existing xUnit suite `dotnet test` from repo root must still be green. No regressions.

---

## Files to author / modify

### New files

```
ECSUnity/Assets/Scenes/PlaytestScene.unity                         (the scene itself)
ECSUnity/Assets/Scenes/PlaytestScene.md                            (first-light recipe)
ECSUnity/Assets/Scripts/Playtest/PlaytestSceneSeeder.cs            (MonoBehaviour, [SerializeField] knobs)
ECSUnity/Assets/Scripts/Playtest/PlaytestSceneIndicator.cs         (#if WARDEN HUD overlay)
ECSUnity/Assets/StreamingAssets/playtest-office.json               (the world-definition seed)
docs/c2-content/world-definitions/playtest-office.json             (canonical copy)
ECSUnity/Assets/Tests/Edit/PlaytestSceneSeederTests.cs
ECSUnity/Assets/Tests/Play/PlaytestSceneSmokeTests.cs
```

### Modified files

```
ProjectSettings/EditorBuildSettings.asset                          (add PlaytestScene to scene list)
```

### Files NOT to modify

- `ECSUnity/Assets/Scenes/MainScene.unity` — production scene; do not touch.
- Any prefab under `ECSUnity/Assets/Prefabs/` — composition only, no edits.
- Any script under `ECSUnity/Assets/Scripts/` outside `Playtest/` — composition only, no edits.
- Any engine-side file under `APIFramework/` — engine substrate is closed for this packet.

---

## Inspector contracts (Rule 5 of UNITY-PACKET-PROTOCOL.md)

`PlaytestSceneSeeder.cs` exposes the following `[SerializeField]` fields, each with `[Tooltip]` and `[Range]` where applicable:

| Field | Type | Default | Range | Tooltip |
|---|---|---|---|---|
| `_npcCount` | int | 15 | 1–30 | "How many NPCs to spawn at scene boot. 15 is the bibles' default office; up to 30 for FPS gate verification." |
| `_worldDefinitionPath` | string | `"playtest-office.json"` | — | "StreamingAssets-relative path to the world-definition JSON for this scene." |
| `_archetypeBalanceMode` | enum | `EvenAcrossAll` | — | "EvenAcrossAll = 1+ of each archetype filling up to npcCount; CustomFromJson = read distribution from world-definition." |
| `_seedStainsAtBoot` | int | 3 | 0–20 | "Initial slip-hazard stains placed at boot. Scenario verbs (WP-PT.1) let Talon seed more during play." |
| `_seedBreakablesAtBoot` | int | 6 | 0–30 | "Initial BreakableComponent props placed on desks at boot." |
| `_startWallTimeHHMM` | string | `"08:30"` | — | "Sim wall-clock time at scene boot. Default = 8:30 AM (workers arriving)." |

`PlaytestSceneIndicator.cs` exposes:

| Field | Type | Default | Tooltip |
|---|---|---|---|
| `_label` | string | `"PLAYTEST SCENE — WARDEN BUILD"` | "Top-left HUD overlay text. Compiled out under non-WARDEN." |
| `_fontSize` | int | 12 | "Pixel size of the indicator text." |
| `_alpha` | float | 0.5 | "Indicator opacity. 0.5 keeps it dim and out of the way." |

---

## First-light recipe (ships as `Assets/Scenes/PlaytestScene.md`)

Sonnet authors this file verbatim. Talon runs it before merging.

```markdown
# PlaytestScene — First-Light Recipe

> Run before merging WP-PT.0. Time budget: 10–15 minutes. Goal: verify every Phase 3 surface lights up.
> If any check fails: file as BUG-NNN per docs/playtest/README.md severity rubric and route per the bug-fix wave. Do NOT ask Sonnet to iterate ad-hoc on this packet — failed first-light items are normal program intake.

## Setup (one-time)

1. Open Unity Editor on the `sonnet-wp-pt.0` worktree branch.
2. Open `Assets/Scenes/PlaytestScene.unity`.
3. Confirm the PLAYTEST SCENE indicator is visible top-left (dim, 12pt).

## Boot check (1 minute)

1. Press Play.
2. Observe: no compile errors, no NullRefs in console, no pink magenta-missing-shader meshes.
3. Confirm 15 NPCs spawn within 2 seconds.
4. Confirm FPS gauge (top-right, FrameRateMonitor) reads ≥ 58.

## Camera (1 minute)

1. Pan with arrow keys / left-mouse-drag. Smooth, no overshoot.
2. Rotate with Q / E. Lazy-susan rotation works.
3. Zoom with mouse wheel. Bounded — can't zoom under cubicles or above ceiling.
4. Double-click an NPC. Camera glides toward them.

## Selection + inspector (2 minutes)

1. Single-click an NPC. Halo + outline appear under/on them. Inspector glance opens (5 fields).
2. Click drill button. Drill layer opens (drives, willpower, schedule, task, stress, mask).
3. Click again. Deep layer opens (full vectors, relationships, memory).
4. Click off. Halo + outline disappear; inspector closes.
5. Click a chair. Object inspector opens (named anchor, current state, interactors).
6. Click an empty floor tile. Room inspector opens.

## Build mode (2 minutes)

1. Press B. World tints beige-blue. Build palette appears on the right.
2. Drag a wall ghost into the world. Red tint where invalid (overlapping NPC); green where valid. Click to place.
3. Confirm an NPC re-paths around the new wall on next tick.
4. Drag a door, place it. Right-click → lock.
5. Press B again. Tint clears; palette closes.

## Time control (1 minute)

1. Cycle pause / ×1 / ×4 / ×16 via number keys.
2. Confirm sim speed visibly changes; FPS holds ≥ 58 at all speeds.
3. Press space — pauses. Press space — resumes at last speed.

## Event log (1 minute)

1. Open the event log (icon or default keybind — see TimeHudPanel for binding).
2. Confirm reverse-chronological list shows recent events.
3. Filter by NPC = (any selected NPC name); filter narrows. Clear.
4. Click an event entry; inspector opens pinned to that NPC.

## Dev console (1 minute)

1. Press backtick (`~`). Console opens.
2. Type `help`. List of existing commands appears (no `scenario *` yet — that ships in WP-PT.1).
3. Type `force-faint <name>` for a visible NPC. They drop.
4. Type `force-kill <name>` for the same NPC. They die. Bereavement cascade fires on witness NPCs (you'll see chibi-emotion cues + log entries).
5. Close the console.

## Sound (1 minute)

1. With audio on, focus camera on an active NPC.
2. Sit for 30 seconds. Confirm you hear at least: footsteps, chair-squeaks, ambient hum.
3. If no audio at all: file BUG with `Severity: High`. If audio present but specific triggers missing: note in PT-001.

## Save/load round-trip (1 minute)

1. Pause sim mid-day.
2. Save (default name "Tuesday Day N" or whatever the day is).
3. Click "Load most recent autosave."
4. Sim restores to identical state. Selected NPC is the same; clock matches; build edits persist.

## Performance gate (sustained — 2 minutes)

1. Resume sim at ×4.
2. Watch the FPS gauge for 2 minutes. p95 should be ≥ 58.
3. Note any frame stutters (camera jerks, audio glitches) with timestamps.

## Pass criteria

- All Boot, Camera, Selection, Build, Time, Event log, Dev console items pass without exception.
- Audio: at least 3 distinct sound triggers heard.
- Save/load: round-trip preserves state visually.
- Performance: p95 FPS ≥ 58 at ×4 with 15 NPCs.

If 80%+ of the above passes, the scene is mergeable; remaining items become BUG-NNN entries in known-bugs.md (severity per rubric) and feed PT-001's session focus.

If less than 80% passes, return to Sonnet for a fix wave (the spec was incomplete or a primitive regressed).
```

---

## Suggested implementation order (for the Sonnet executor)

1. **Worktree pre-flight** per the dispatch protocol below.
2. Author `playtest-office.json` modeled on `office-starter.json` — copy and extend with extra rooms and hazards. Keep tile counts modest (FPS gate).
3. Author `PlaytestSceneSeeder.cs` and `PlaytestSceneIndicator.cs`.
4. Build `PlaytestScene.unity` in the Editor: drag in `EngineHost` GameObject, wire to seeder + world-definition path, add the renderer GameObjects, drop in `CameraRig.prefab`, add the UI panels (selection, inspector, time HUD, event log, build palette, save/load, settings, dev console). Save the scene.
5. Author the first-light recipe (`Assets/Scenes/PlaytestScene.md`) per the verbatim text above.
6. Author the xUnit tests (Edit + Play modes).
7. Run `dotnet test` and `dotnet build`. Both green.
8. Add PlaytestScene to `EditorBuildSettings.asset`.
9. Self-cleanup grep + commit per protocol footer below.
10. Push, notify Talon for visual verification.

---

## Dependencies

- **Engine-side:** none new. All Phase 3 substrate is shipped and stable.
- **Unity-side:** all four Phase 3 sandbox prefabs (`CameraRig`, `Selectable`, `Table`, `Banana`) and the existing scripts under `ECSUnity/Assets/Scripts/` are stable.
- **Content-side:** all seven `docs/c2-content/archetypes/archetype-*.json` files are stable.

No `DO NOT DISPATCH UNTIL X IS MERGED` line — this packet is dispatch-ready against current `staging`.

---

## Downstream dependents

- **WP-PT.1 — Dev-console scenario verbs.** Carries `DO NOT DISPATCH UNTIL WP-PT.0 IS MERGED` because the verbs are only meaningfully testable inside PlaytestScene.
- **PT-001 — first session.** Cannot run until this packet merges.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

This is a Track 2 (Unity) packet. xUnit tests are necessary but **not sufficient** — the visual layer must be verified by Talon in Unity Editor before PR is mergeable.

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm you are in a dedicated worktree at `.claude/worktrees/sonnet-wp-pt.0/` on branch `sonnet-wp-pt.0` based on recent `origin/staging`. If anything is wrong, stop and notify Talon.
1. Implement the spec — write scripts, build the scene, seed content, ship the recipe.
2. Add or update xUnit tests per §E.
3. Run `dotnet test` and `dotnet build`. Must be green.
4. Stage all changes including the self-cleanup deletion (see below).
5. Commit on the worktree's feature branch.
6. Push the branch.
7. Stop. Do **not** open a PR yet. Do **not** merge.
8. Notify Talon (final commit message line: `READY FOR VISUAL VERIFICATION — run Assets/Scenes/PlaytestScene.md`).

Talon's pipeline (after Sonnet's push):

1. Open the Unity Editor on the feature branch.
2. Run the first-light recipe (`Assets/Scenes/PlaytestScene.md`).
3. If the recipe passes (≥ 80%): open the PR, merge to `staging`. File any sub-80% items as `BUG-NNN` entries.
4. If the recipe fails (< 80%): file the failure in a follow-up packet or as PR review comments. **Do not** ask the original Sonnet to iterate ad-hoc — failed first-light recipes mean the spec was incomplete or the implementation diverged.

### Feel-verified-by-playtest acceptance flag

**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN:** every Phase 3 surface listed in §B and §C above. PT-001 IS this packet's formal feel acceptance.

### Cost envelope (1-5-25 Claude army)

Target: **$0.40–$0.70** per packet wall-time on the orchestrator. Timebox is 90–150 minutes. If costs approach the upper bound without acceptance criteria nearing completion, **escalate to Talon** by stopping work and committing a `WP-PT.0-blocker.md` note to the worktree explaining what burned the budget.

Unity-specific cost-discipline:
- Don't open and close prefabs in the Editor repeatedly — most of the work here is scene-file authorship + script authorship; minimize Editor round-trips.
- Author the world-definition JSON by hand (it's structured data) rather than via a prefab pipeline.
- The first-light recipe ships verbatim from §"First-light recipe" above; do not freelance the recipe.

### Self-cleanup on merge

After Talon's visual verification passes, before opening the PR:

1. Check downstream dependents:
   ```bash
   git grep -l "WP-PT.0" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```

2. The grep will return `WP-PT.1-dev-console-scenario-verbs.md` (it depends on this packet). Therefore: **leave this spec file in place**. Add the following status header at the top of this file (immediately under the H1):
   ```markdown
   > **STATUS:** SHIPPED to staging YYYY-MM-DD. Retained because pending packets depend on this spec: WP-PT.1.
   ```
   Add `Self-cleanup: spec retained, dependents: WP-PT.1.` to the commit message.

3. **Do not touch** files under `_completed/` or `_completed-specs/`.

4. The scene file (`PlaytestScene.unity`), recipe (`PlaytestScene.md`), seeder scripts, world-definition JSON, and tests are all permanent — none are deleted by this protocol.
