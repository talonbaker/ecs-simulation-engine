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

## RETAIL Build — Standalone Strip Broken

### BUG-002: Standalone Player build produces 960+ compile errors with WARDEN removed

**Symptom:** Running a Unity Standalone Player build (Windows x86_64, WARDEN scripting define removed for RETAIL strip per SRD §8.7) produces 960+ compile errors before the build can complete. Build never reaches runtime; the .exe is never produced.

**Repro:**
1. Open Unity on the project at any commit on `staging` from 2026-05-02 forward (regression likely predates this; not yet bisected).
2. **File > Build Settings**: switch platform to Windows Standalone.
3. **Player Settings > Other Settings > Scripting Define Symbols**: remove `WARDEN`. Apply.
4. Click **Build**.
5. Build aborts with a flood of compile errors (observed 960+).
6. Expected: clean RETAIL build per axiom 8.7; observed: catastrophic compile failure.

**Severity:** **Critical**

> **Why Critical:** SRD §8.7 commits the engine to be host-agnostic with `#if WARDEN` code stripping clean at ship. A broken RETAIL build means the project cannot ship in its current state and we don't know how broken it actually is until someone triages the 960 errors. This is a load-bearing axiom violation regardless of feature completion elsewhere.

**Root cause (investigated):** Not yet triaged. Likely candidates ranked by probability:

1. **Test asmdef leakage** — `ECSUnity/Assets/Tests/Edit/ECSUnity.Tests.Edit.asmdef` and/or `ECSUnity/Assets/Tests/Play/ECSUnity.Tests.Play.asmdef` not restricted to `Editor` platform; pulls `nunit.framework` / `UnityEngine.TestTools` into the Player build, which the build cannot resolve. The 960 cascade is consistent with *every* test class failing to compile against missing assemblies.
2. **Non-WARDEN code referencing WARDEN-only types** — at least one runtime script outside any `#if WARDEN` guard mentions a WARDEN-only type (likely `DevConsolePanel`, `JsonlStreamEmitter`, `AsciiMapProjector`, `ScenarioCommand`, or one of their dependencies). The WARDEN strip removes the type's definition; every reference cascades into "type or namespace could not be found."
3. **`UnityEditor.*` references in runtime code** without `#if UNITY_EDITOR` guards.
4. **Project-wide regression** introduced sometime after the Phase 3.1.x bundle's last clean Standalone build (which presumably worked since the bundle shipped). Cumulative drift across multiple Phase 3 packets is plausible.

**Discovered in:** WP-PT.1 first-light testing (2026-05-02). Talon attempted a Standalone build to playtest WP-PT.1's scenario verbs in a non-Editor context and hit the 960 errors before the build could run.

**Files relevant to fix:** Pending triage. Likely:
- `ECSUnity/Assets/Tests/Edit/ECSUnity.Tests.Edit.asmdef` (Include Platforms field)
- `ECSUnity/Assets/Tests/Play/ECSUnity.Tests.Play.asmdef` (Include Platforms field)
- `ECSUnity/ProjectSettings/ProjectSettings.asset` (per-target scripting defines)
- Any runtime script under `ECSUnity/Assets/Scripts/` mentioning `using UnityEditor;` or referencing WARDEN-only types outside a `#if WARDEN` block

**Suggested fix wave:** Inline. `WP-FIX-BUG-002-retail-build-restore.md` (drafted 2026-05-02) is the dispatchable fix spec.

**Workaround (for development only):** Continue testing in Editor Play mode; do not expect Standalone builds to function until BUG-002 is fixed. Editor Play mode is unaffected because WARDEN is defined for the Editor target.

**Surfaced by:** Build Verification Recipe (`docs/playtest/BUILD-VERIFICATION-RECIPE.md`). The recipe was authored in response to this discovery — see the kickoff-brief calibration update for 2026-05-02. Going forward, packets that touch scripting defines / asmdefs / `#if WARDEN` boundaries / `Plugins/` carry the `build-verified-by-recipe: YES` flag per Rule 7 of `docs/UNITY-PACKET-PROTOCOL.md`, and the recipe runs periodically as phase-wave hygiene.

---
