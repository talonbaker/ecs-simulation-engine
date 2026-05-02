# Build Verification Recipe — RETAIL Standalone Build

> **Status:** Live as of 2026-05-02 (added in response to Talon's discovery that Standalone Player builds were producing 960+ compile errors during WP-PT.1 playtesting).
> **Authority:** Part of the Playtest Program (`docs/playtest/README.md`). Sibling recipe to the per-session feel-verification flow.
> **Cadence:** Run before any "ship-readiness" milestone, after any packet that touches scripting defines / asmdefs / `#if WARDEN` boundaries / scene build list / `Plugins/`, and at minimum once per phase wave (e.g., post-4.0.x, post-4.1.x).
> **Failures file as `BUG-NNN` per the playtest README severity rubric.**

---

## What this recipe verifies

SRD axiom 8.7 commits the engine to be **host-agnostic** with `#if WARDEN`-gated telemetry that **strips at ship**. The shipped game is a RETAIL Standalone build with WARDEN code completely absent — no dev console, no JSONL stream, no scenario verbs, no AsciiMap projector, no Warden.* references.

This recipe is the verification harness for that commitment. It catches:

- **Scripting-define drift** — WARDEN set on Editor target but not on Standalone target (or vice versa). Causes either compile failures or accidental retail leakage.
- **Test-asmdef leakage** — `Tests/Edit/*.asmdef` or `Tests/Play/*.asmdef` not restricted to Editor platform; pulls `nunit.framework` / `UnityEngine.TestTools` into the Player build, which the build can't resolve.
- **Editor-only API references** — runtime code using `UnityEditor.*` namespace without `#if UNITY_EDITOR` guards.
- **WARDEN-only types referenced from non-WARDEN code** — any non-`#if WARDEN`-guarded code that mentions `DevConsolePanel`, `JsonlStreamEmitter`, `AsciiMapProjector`, `ScenarioCommand`, etc. The reference fails to resolve in RETAIL builds.
- **IL2CPP code-stripping breakage** — reflection targets in the engine bootstrap getting stripped if the build target uses IL2CPP with `Managed Stripping Level >= Low`. (Less likely if the project uses Mono; check Player Settings.)
- **Platform-specific scene-list bleed** — `_Sandbox/*.unity` or `PlaytestScene.unity` accidentally included in the build's scene list, pulling in WARDEN-only dependencies.

xUnit cannot detect any of the above. The Editor's Play mode cannot detect any of the above (the Editor always has WARDEN unless explicitly toggled). The only verification harness is a real Standalone build, run as a real .exe.

---

## Setup (one-time, per dev machine)

1. Confirm Unity Editor is on the project's pinned version (check `ProjectSettings/ProjectVersion.txt`).
2. Confirm Build Support for Standalone Windows (or your ship platform) is installed via Unity Hub.
3. Decide on a build output directory outside the repo — e.g., `C:\BuildOutput\ecs-retail\`. Don't commit build artifacts.

---

## Recipe

### Phase 1 — RETAIL build configuration

1. Open the project in Unity Editor.
2. **File > Build Settings**.
3. **Platform:** select `Windows, Mac, Linux Standalone`. Architecture `x86_64` (Windows). Click **Switch Platform** if not already active.
4. **Scenes In Build:** confirm only `Assets/Scenes/MainScene.unity` is checked. **Uncheck** `PlaytestScene.unity` and any `_Sandbox/*.unity` if present. Sandbox and playtest scenes are dev-time-only.
5. **Player Settings > Other Settings > Scripting Define Symbols:**
   - For Standalone target: **WARDEN must NOT be present.** Remove it if present, click `Apply`.
   - This makes the build a true RETAIL strip.
6. **Player Settings > Other Settings > Scripting Backend:** note the value (Mono or IL2CPP). RETAIL ship builds use IL2CPP; verification can use either, but record which.
7. **Player Settings > Other Settings > Managed Stripping Level:** record the value. `Low` is the standard; `High` aggressively strips and can break reflection (relevant to `SceneBootstrapper.cs`).

### Phase 2 — Build

1. Click **Build** in Build Settings.
2. Choose your build output directory.
3. Watch the build log. **Any compile errors abort the recipe at this step** — file as `BUG-NNN` (severity Critical given §8.7 violation), paste the first ~30 errors into the bug, and stop. Do not proceed to runtime verification with a broken build.
4. If build succeeds with warnings: note the warnings (some are benign — IL2CPP marshaling complaints from third-party DLLs are common). Proceed.
5. Build artifacts produced: `<output>/<ProjectName>.exe`, `<output>/<ProjectName>_Data/`, `<output>/MonoBleedingEdge/` (if Mono) or `<output>/<ProjectName>_Data/il2cpp_data/` (if IL2CPP).

### Phase 3 — Runtime verification

1. **Run the .exe** outside the Editor. Unity's stdout goes to `<ProjectName>_Data/output_log.txt` (Mono) or `%USERPROFILE%\AppData\LocalLow\<Company>\<ProjectName>\Player.log` (IL2CPP). Open it in another window.
2. **First-light: does it boot?** Window opens; main menu / scene appears within 5 seconds; no immediate crash. If crash: file as Critical, attach the Player log.
3. **Engine ticks:** wait 30 seconds; confirm NPCs spawn and move (you'll see them on the silhouette renderer); time-of-day clock advances if you wait longer.
4. **Player surfaces work** (these should all be present in RETAIL):
   - [ ] Camera pans / rotates / zooms.
   - [ ] Click an NPC; selection cue appears; inspector opens.
   - [ ] Press B; build mode toggles; ghost preview works; placement works.
   - [ ] Time HUD cycles pause / ×1 / ×4 / ×16.
   - [ ] Save the game. Quit. Relaunch. Load. Game restores.
   - [ ] Settings reachable. Soften toggle present.
   - [ ] Sound plays (footsteps, ambient hum, NPC speech fragments).
5. **Player surfaces correctly stripped** (these should NOT be present in RETAIL):
   - [ ] **Dev console: pressing backtick (`~`) does NOTHING.** No console appears. (If it does: WARDEN is leaking into RETAIL — Critical bug.)
   - [ ] **JSONL stream: no file written to `worldstate.jsonl`** anywhere on disk. (If written: telemetry is leaking — Critical.)
   - [ ] **Scenario verbs: not reachable** (no console exists; even if a console did exist, `scenario` would be unrecognised).
   - [ ] **PlaytestScene indicator: does NOT appear top-left.** (Should not — PlaytestScene wasn't in the build list. If somehow present: build configuration error.)
   - [ ] **Settings menu doesn't expose dev-time options** (no JSONL cadence slider, no AsciiMap toggle, etc.).
6. **Player log clean:** scroll the Player log. Acceptable: occasional `INFO`-level messages. **Not acceptable:** `Error:`, `Exception:`, `NullReferenceException`, `MissingReferenceException`, `assembly not found`, `type or namespace ... could not be found`. Any of those file as a bug.
7. **Performance:** with 15 NPCs, FPS should hold ≥ 60 (no FrameRateMonitor HUD in RETAIL — observe externally via task manager / RTSS / Unity profiler attached to the Standalone build via `BuildOptions.Development`, but only as a dev-only verification step).

### Phase 4 — Restore Editor configuration

1. **Player Settings > Scripting Define Symbols** for Standalone: **add WARDEN back**. Otherwise next time you Editor-Play-mode test in Standalone-target context, WARDEN code disappears mid-iteration.
2. Switch the Build Settings active platform back to whatever you were on before (typically Standalone with WARDEN, since dev-mode standalone builds DO include WARDEN — only the RETAIL ship build strips it).

> **Important:** the WARDEN-add-back step is operationally necessary. The recipe deliberately strips WARDEN to test the strip; you must restore it after, or the next Editor session inherits the stripped state and feels broken.

---

## Pass / fail

**PASS:** Phases 1–3 complete with all checkboxes green and Player log clean. Recipe takes 15–25 minutes including build time.

**FAIL:** Any compile error in Phase 2, any unboot in Phase 3 step 2, any "should be stripped but isn't" failure in Phase 3 step 5, any error/exception in Phase 3 step 6.

Each failure files as `BUG-NNN` per the severity rubric in `docs/playtest/README.md`. The §8.7 axiom violation makes most failures **Critical**: a broken RETAIL build cannot ship, and we don't know how broken it is until we examine the failure.

---

## Triage hints (when the recipe fails)

| Symptom | Likely cause | First check |
|---|---|---|
| Build fails with "type or namespace could not be found" | Non-WARDEN code references a WARDEN-only type | Search for the type name in non-`#if WARDEN` files |
| Build fails with "assembly not found" referencing `nunit.framework` or `UnityEngine.TestTools` | Test asmdef bleeding into Player build | Open `Tests/Edit/*.asmdef` and `Tests/Play/*.asmdef`; restrict `Include Platforms` to Editor only |
| Build fails with `UnityEditor` namespace errors | Runtime code missing `#if UNITY_EDITOR` guard | Search for `using UnityEditor` in `Assets/Scripts/` and verify every occurrence is inside a `#if UNITY_EDITOR` block |
| Build succeeds but .exe fails to launch | Missing transitive plugin DLL or scripting-backend mismatch | Check `<exe>_Data/Managed/` (Mono) for missing assemblies; check Player log for "could not load" entries |
| .exe runs but dev console DOES open | WARDEN is set on Standalone target | Player Settings > Scripting Define Symbols: confirm WARDEN absent for Standalone |
| .exe runs but no NPCs appear | World-definition path issue or RETAIL strip removed something it shouldn't have | Check Player log for `office-starter.json not found`; check for `MissingReferenceException` |
| Hundreds of compile errors at once (the WP-PT.1 case) | Either WARDEN is set on Standalone but a script can't compile under WARDEN, OR test asmdefs leaked into the build | Look at the *first* error chronologically — usually the rest cascade |

---

## Relationship to PT-NNN sessions

| | PT-NNN feel session | Build-verification recipe |
|---|---|---|
| Where it runs | Unity Editor, Play mode | Standalone .exe, outside Editor |
| What it verifies | Integrated-whole feel; sustained-play emergent behavior; visual perception | RETAIL strip correctness; ship-readiness; SRD §8.7 compliance |
| Cadence | Talon-paced, whenever Talon wants to play | Periodic + before phase merges + after any packet flagged `build-verified-by-recipe` |
| Output | Numbered report (`PT-NNN-<slug>.md`) | Pass/fail + bug entries; no numbered run report (the bugs are the artifact) |
| Failure mode | "It feels off" → BUG with Severity Medium typically | "Build doesn't ship" → BUG with Severity Critical typically |

Both surfaces are owned by the Playtest Program. Both file bugs into the same `BUG-NNN` ledger. Both are bypassed only at the program's peril.

---

## When this recipe must be run (mandatory triggers)

A packet that **must** run this recipe before merge if any of the following is true:

- The packet modifies `ProjectSettings/ProjectSettings.asset` (especially `scriptingDefineSymbols`).
- The packet adds or modifies any `*.asmdef` file.
- The packet introduces new `#if WARDEN` blocks or removes existing ones.
- The packet adds files under `Plugins/` or modifies plugin import settings.
- The packet modifies the build's scene list (`EditorBuildSettings.asset`).
- The packet adds new `using UnityEditor;` references in any runtime script.

Such packets carry `build-verified-by-recipe: YES` in their header (per Rule 7 of `UNITY-PACKET-PROTOCOL.md`).

For packets where none of those triggers apply (most engine-only packets, most pure-feel-tuning packets, doc-only packets), the flag is `NO` and the recipe doesn't gate that packet. The recipe still runs periodically as part of phase-wave hygiene, catching cumulative drift.
