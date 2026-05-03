# WP-FIX-BUG-005 — Camera Recenter, Pause Semantics, and Audio Listener

> Fixes BUG-005 + BUG-006 (`docs/known-bugs.md`). Surfaced by PT-001 (`docs/playtest/PT-001-baseline.md`).

**Track:** 2 (Unity)
**Phase:** Inline fix — playtest-program-driven
**Author:** Opus, 2026-05-02
**Sonnet executor:** assigned by Talon
**Branch:** `sonnet-wp-fix-bug-005`
**Worktree:** `.claude/worktrees/sonnet-wp-fix-bug-005/`
**Timebox:** 60–90 minutes
**Cost envelope:** $0.30–$0.50
**Feel-verified-by-playtest:** YES — camera pan/recenter feel and audio mix are both feel-level acceptance.
**Surfaces evaluated by next PT-NNN:** double-click camera glide; WASD+zoom behavior under sim-pause; audible footsteps / chair squeaks / ambient hum / NPC speech in PT-002.
**Build-verified-by-recipe:** NO.
**Parallel-safe with:** WP-FIX-BUG-004 (UI wiring), WP-FIX-BUG-007 (dev-console submit). Disjoint surfaces.

---

## Goal

Two scoped fixes both centered on the camera GameObject in `Assets/Scenes/PlaytestScene.unity`:

1. **Camera recenter glides to target.** Double-clicking an NPC currently sends the camera to world origin. It should glide smoothly toward the clicked NPC (per UX bible §2.2).
2. **Pause-state input semantics inverted.** Sim-pause should preserve full camera/zoom freedom (the player is reviewing a frozen world per UX bible §1.5). Currently sim-pause disables WASD pan but leaves zoom active — exactly inverted from the design.
3. **Audio listener missing.** No GameObject in PlaytestScene carries an `AudioListener` component. Without one, Unity plays nothing regardless of how the SoundTriggerBus is wired.

---

## Non-goals

- Do not implement a menu-pause / Esc menu in this packet. The pause-semantics fix should treat the existing space-bar pause as a **time-stop** (camera stays interactive) and leave a future packet to add a real menu pause that disables camera input.
- Do not author voice profiles or per-archetype audio synthesis (Phase 4.1.0 territory).
- Do not refactor `CameraController` beyond the targeted fixes.

---

## Investigation phase

### Step T1 — Trace the double-click recenter chain

Read the camera input chain end to end:
1. `CameraController.cs` — does it expose a `RecenterOnTarget(Transform t)` or `GlideTo(Vector3 pos)` method?
2. `CameraInputBindings.cs` — does it map double-click to that method? Or does it map to a "go home" verb?
3. The selection chain: when an NPC is double-clicked, which event fires? `SelectionController.GlideRequested`? Or is the camera itself listening for double-click events independently?

The bug is somewhere in this chain — likely either:
- The camera listens for double-click but ignores the selected target (always uses origin)
- The selection chain doesn't notify the camera at all; camera falls back to a default home position

### Step T2 — Audit pause-state input gating

Find where camera input is currently gated on pause state:
1. Grep for `IsPaused`, `Time.timeScale`, or similar in CameraController/CameraInputBindings.
2. Read the gate condition. Likely something like `if (Time.timeScale > 0) ProcessInput()` — which disables ALL camera input on pause; that's why WASD freezes.
3. Note that mouse-wheel zoom appears to bypass the gate (which is a separate inconsistency).

### Step T3 — Audio listener verification

Open PlaytestScene in Editor. Click the Main Camera GameObject. Look at its components in Inspector. Confirm: is there an AudioListener component? If absent, that's BUG-006's primary cause. If present but we still hear nothing, the SoundTriggerBus → host-synth chain is the deeper issue (see "Stretch goal" below).

---

## Fix paths

### Fix A — Camera double-click recenter

Per UX bible §2.2: "Double-click recenters the camera with a smooth glide toward the target."

The fix shape (per investigation):
- If `SelectionController` exposes a `GlideRequested` event (per the agent's earlier finding it does), have CameraController subscribe and call its glide-to-target method on the event.
- If CameraController already has a glide-to-target method but it's not being driven by selection events, wire the subscription.
- If the camera has its own double-click handler that ignores selection, replace its target-resolution to use the currently-selected entity's position (via `SelectionController.SelectedEntity` or similar accessor).

The glide should:
- Take ~250–350ms (smooth-curve, not instant)
- End with the camera centered on the target's world position at the camera's current altitude
- Honor camera constraints (don't dive under cubicles, don't rise above ceiling — per `CameraConstraints.cs`)

### Fix B — Pause-state input semantics

The intent is **time-stop preserves camera; menu-pause disables camera**. There is no menu-pause yet, so:

1. **Remove** the pause gate from camera WASD pan input. Camera input runs always.
2. **Mouse-wheel zoom** should also run always — already does, per Talon's report. No change.
3. Leave a TODO comment noting that a future menu-pause packet will introduce a real `IsMenuPaused` flag; camera input will gate on that, not on `Time.timeScale`.

Concrete: in CameraInputBindings.cs (or wherever the gate lives), find the `if (paused) return` pattern and either remove it or replace with `if (IsMenuPaused) return` where `IsMenuPaused` is a stub that always returns false for now.

### Fix C — Add AudioListener to camera

In Editor:
1. Open PlaytestScene.
2. Click Main Camera in Hierarchy.
3. Add Component → Audio → Audio Listener.
4. Save scene.

The AudioListener is a single component with no fields. One per scene. Adding it doesn't break anything.

After the AudioListener is in place, audio MAY still be silent if the SoundTriggerBus → host-synth chain isn't wired in PlaytestScene. Verify in Play mode by:
1. Selecting an active NPC.
2. Listening for footsteps. They should be audible if a SoundTriggerBus consumer + AudioSource exists.

### Stretch — Sound bus consumer (only if AudioListener alone doesn't fix it)

If AudioListener is present but PlaytestScene still has no audio:
1. Search for `SoundTriggerBus` consumers under `ECSUnity/Assets/Scripts/`. Likely a `SoundTriggerSynth.cs` or `SoundTriggerHost.cs` MonoBehaviour.
2. If such a script exists but isn't on a GameObject in PlaytestScene, add a GameObject with the component.
3. If no such consumer script exists, the host-side synth from WP-3.2.1 was never built. **Do not implement it in this packet** — file a follow-up `BUG-009` and stop. WP-FIX-BUG-005 closes with "AudioListener added; sound-bus consumer absent" if this is the case.

---

## Acceptance criteria

### A — Camera recenter

A1. Click an NPC; selection registers (visible halo+outline once BUG-004 is also fixed; or visible outline alone if BUG-004 hasn't merged yet).
A2. Double-click the same NPC. Camera glides smoothly (~300ms) toward that NPC's world position. End state: camera is centered on the NPC.
A3. Glide respects camera altitude constraints — no clipping into floor, no flying above ceiling.

### B — Pause-state input

B1. Press space → sim pauses.
B2. While paused: WASD pans the camera around the office. Mouse-wheel still zooms. Right-click drag still rotates.
B3. Press space → sim resumes. Camera input continues to work.

### C — Audio

C1. PlaytestScene's Main Camera GameObject has an `AudioListener` component visible in Inspector after fix.
C2. **Stretch (depends on Stretch goal):** play mode produces audible footsteps and at least one ambient sound when camera is focused on an active NPC. If absent, BUG-009 is filed describing exactly which part of the sound chain is missing.

### D — xUnit + build green

D1. `dotnet test` from repo root: all green.
D2. `dotnet build` clean.

---

## Files likely to modify

- `ECSUnity/Assets/Scenes/PlaytestScene.unity` (AudioListener on Main Camera; possibly CameraController serialized fields)
- `ECSUnity/Assets/Scripts/Camera/CameraController.cs` (recenter / glide logic)
- `ECSUnity/Assets/Scripts/Camera/CameraInputBindings.cs` (pause gate removal)

## Dependencies

- **Hard:** none.
- **Soft:** WP-FIX-BUG-004 also touches PlaytestScene.unity. If both packets dispatch in parallel and both add components to the camera GameObject, expect a small merge conflict in that GameObject's component list — resolution is "keep both."

## Completion protocol

### Visual verification: REQUIRED

0. Worktree pre-flight on `.claude/worktrees/sonnet-wp-fix-bug-005/` branch `sonnet-wp-fix-bug-005`.
1. Investigation phase (T1/T2/T3). Commit a short note documenting findings.
2. Apply fixes A / B / C in order.
3. Run PlaytestScene; manually verify acceptance §A / §B / §C.
4. `dotnet test` green.
5. Stage / commit / push. Final line: `READY FOR VISUAL VERIFICATION — camera glide, pause-pan, audio listener`.
6. Talon verifies in PT-002.

### Feel-verified-by-playtest

**YES.** The 300ms glide duration, the zoom-while-paused feel, the audio mix volume — all subjective. PT-002 is the formal acceptance. Adjust glide duration if the 300ms target feels wrong.

### Cost envelope

$0.30–$0.50. Timebox 60–90 minutes. Investigation is fast; fix B is a one-line removal; fix A is the bulk of the work (selection-event subscription); fix C is a single Editor click.

### Self-cleanup

Same shape as WP-FIX-BUG-004's footer:
1. Grep dependents.
2. If none: `git rm` this file in the same commit.
3. Append Resolution to BUG-005 + BUG-006 entries in `docs/known-bugs.md`.
