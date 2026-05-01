# Unity Packet Protocol

> **Authority:** Load-bearing for all Unity-side work from 2026-04-30 forward. Future Unity packets that violate this protocol should be revised before dispatch.
> **Author:** Opus General, 2026-04-30.
> **Status:** Live.

---

## Why this protocol exists

The Phase 3.1.x bundle (WP-3.1.A through WP-3.1.H) shipped as a single multi-packet bundle. Tests passed. The FPS gate held. The compiled Unity build ran without errors. **And nothing rendered the way it was supposed to.** Six follow-up "Phase 3.1.x fix" commits patched it: missing renderers in the committed scene, axis confusion (`Position.Y` vs `Position.Z`), sub-pixel dot sizing, camera altitude wrong for the 60-tile world, missing transitive dependencies.

The systemic mismatch: xUnit tests verify ECS contracts. They cannot see Inspector wiring, scene file contents, axis conventions, world-unit scale, camera framing, or whether anything is on screen. The Sonnet shipped against contracts that couldn't catch the divergence, and Talon — the only one who can actually see Unity output — became the single-threaded debugger.

This protocol rewires Unity work to match how a professional Unity team operates: sandbox-first, atomic, prefab-driven, Inspector-wired by the human, with each packet verifiable in a 5-minute solo pass.

---

## The five rules

### Rule 1 — One feature, one packet

Every Unity packet ships **exactly one** new visual or interactive feature. No bundles, no "while we're at it" additions. If a packet description starts using the word *and*, split it.

A feature is bounded if Talon can describe what passes/fails in a single sentence: "the camera pans smoothly from corner to corner without overshoot," "the outline appears around the clicked cube and disappears when something else is clicked," "the table prefab snaps to the nearest tile when dropped."

### Rule 2 — Sandbox before integration

Every interactive or visual feature ships in **two phases**, two separate packets:

**Phase A — Sandbox packet (`WP-3.1.S.NN`):** Sonnet ships a `Assets/_Sandbox/<feature>.scene` containing the simplest possible test rig — usually a primitive (cube, sphere, capsule), a reference grid, and the new component or prefab. **No engine. No NPCs. No `WorldStateDto`.** Talon opens the scene, presses play, validates the feel, tunes Inspector values on the prefab. Iteration is fast because the feature is isolated.

**Phase B — Integration packet (`WP-3.1.S.NN-INT`):** Wires the validated prefab/script into the live engine scene (`MainScene.unity` or wherever the engine renders). One-line wiring change in most cases, because the hard work was done in Phase A.

The cost of two packets is much lower than the cost of debugging an integrated feature against six other moving parts.

### Rule 3 — Sonnet writes scripts and prefabs; Talon owns scene composition

A Unity packet may ship:

- One or more `.cs` MonoBehaviour scripts.
- One or more `.prefab` files (created via Unity's prefab system, with `[SerializeField]` Inspector contracts documented in the packet).
- One sandbox scene `Assets/_Sandbox/<feature>.scene` that demonstrates the prefab in isolation.
- One sandbox-scene README in `Assets/_Sandbox/<feature>.md` with the 5-minute test recipe.

A Unity packet may **not**, without explicit packet-level rationale:

- Modify `Assets/Scenes/MainScene.unity` or any other live engine scene.
- Modify scene files outside `Assets/_Sandbox/`.
- Use `RuntimeInitializeOnLoadMethod` reflection tricks to inject objects at runtime — that pattern hid the missing-renderer bug for the entire 3.1.x bundle. If a script needs to live in the live scene, the packet ships a prefab and the integration packet adds it via Talon's Inspector hands.

The contract is: Sonnet hands Talon a thing that *works in isolation*. Talon decides where it goes in the live scene.

### Rule 4 — Every packet ships a 5-minute test recipe

Every sandbox packet must include `Assets/_Sandbox/<feature>.md` with:

```markdown
# <Feature> Sandbox

## Setup (one-time)
1. <Drag this prefab onto that GameObject, etc.>

## Test (every run)
1. Open Assets/_Sandbox/<feature>.scene.
2. Press Play.
3. <Specific action: click the cube, hold WASD, etc.>

## Expected
- <Concrete observable: "outline appears in cyan, ~3 pixels wide, follows mouse pickup">
- <Specific Inspector value to confirm or tune>

## If it fails
- <Top-3 likely causes ranked by frequency>
```

If Talon can't run the recipe in 5 minutes from a cold open, the packet is too big. Split it.

### Rule 5 — Inspector contracts are part of the packet spec

Every `[SerializeField]` field that Talon will tune in the Inspector must be:

- Listed in the packet spec by name, type, and meaning.
- Given a sensible default value.
- Annotated with a `[Tooltip("...")]` in the source.
- Range-clamped via `[Range(min, max)]` where the value has a meaningful range.

This is what makes "tune the prefab in the Inspector" a 60-second operation instead of a "what does this float do" archaeology session.

---

## Naming and numbering

- **Sandbox packets:** `WP-3.1.S.NN-<slug>.md` where `S` = sandbox sub-phase, `NN` = zero-padded ordinal. Example: `WP-3.1.S.0-camera-rig-sandbox.md`.
- **Integration packets:** `WP-3.1.S.NN-INT-<slug>.md`. Example: `WP-3.1.S.0-INT-camera-rig-into-mainscene.md`.
- **Sandbox scenes:** `Assets/_Sandbox/<kebab-case-feature>.scene`. Example: `Assets/_Sandbox/camera-rig.scene`.
- **Sandbox docs:** `Assets/_Sandbox/<kebab-case-feature>.md`. Example: `Assets/_Sandbox/camera-rig.md`.
- **Prefabs:** `Assets/Prefabs/<PascalCase>.prefab`. Example: `Assets/Prefabs/CameraRig.prefab`.

The `_Sandbox/` underscore prefix sorts these scenes to the top of Unity's Project window so Talon can find the test rig fast. None of these scenes ship in RETAIL builds — `_Sandbox/` is excluded from build settings.

---

## What this protocol means for Track 1 (engine work)

Track 1 (the engine deepening backlog) is **not affected** by this protocol. Engine packets continue to dispatch as they did during Phase 3.0.x and Phase 2: full ECS systems, full xUnit coverage, headless verifiable. The protocol only governs work that touches the `ECSUnity/` project.

Engine-side preconditions for Unity features (e.g., the engine emits `SoundTriggerBus` events that the Unity host consumes) are split: the engine-side emit ships in a regular engine packet under Track 1; the Unity-side consumption ships as a sandbox+integration pair under Track 2.

---

## What this protocol means for the existing 3.1.x scaffold

The existing `ECSUnity/` project, `MainScene.unity`, `EngineHost.cs`, `WorldStateProjectorAdapter.cs`, `RoomRectangleRenderer.cs`, `NpcDotRenderer.cs`, `CameraController.cs`, etc., **all stay as-is**. The first WP-3.1.S.NN packets do not modify them. The sandbox track *parallels* the existing scene; integration packets later replace specific live-scene components with the validated sandbox prefabs one at a time, in order, with confidence at each step.

The procedural `SceneBootstrapper.cs` reflection workaround stays in place for now (it currently keeps the live scene running). It will be retired once enough integration packets land that Talon can compose `MainScene.unity` directly via Inspector with validated prefabs.

---

## What this protocol does **not** do

- It does not forbid Talon from editing `.cs` files. Talon is free to make any edit; the protocol governs Sonnet packets only.
- It does not forbid Sonnet from editing `MainScene.unity` *forever*. It defers the privilege until Sonnet has a track record of validated sandbox prefabs and Talon explicitly authorises integration packets.
- It does not require sandbox packets to be tiny. A camera rig sandbox can ship a non-trivial `CameraController.cs` with several `[SerializeField]` knobs. The point is that the *scene context* is minimal, not that the script is.

---

## Anti-patterns to refuse

If a future Unity packet draft contains any of the following, revise before dispatch:

- "Modify `MainScene.unity` to add the new GameObject." → No. Ship a prefab; integration packet wires it.
- "Use `RuntimeInitializeOnLoadMethod` to bootstrap the new system." → No. That pattern hid the 3.1.x renderer bug. Ship a prefab and let Talon place it.
- "Tests assert the GameObject exists in the scene at runtime." → That tests bootstrap reflection, not visual correctness. Replace with sandbox manual recipe.
- "This packet ships features A, B, and C." → Split.
- "Talon can verify by running the full sim." → No. Sandbox first, integration second.

---

*Living document. Update when the sandbox track produces lessons that refine the protocol. The first sandbox packets to ship will validate or invalidate specific rules — be willing to revise.*
