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

### Rule 6 — Feel-level work requires a playtest session, not just xUnit

> **Authority:** Added 2026-05-01 as part of the Playtest Program kickoff. See `docs/playtest/README.md` for the program shape.

Some packet acceptance criteria cannot be verified by `dotnet test`, by a sandbox 5-minute recipe, or by reading a JSON list. They depend on **how something feels when you move around inside it.** Examples:

- A camera rig that "pans smoothly without overshoot" can pass its sandbox recipe and still feel sluggish at ×4 with 30 NPCs and active build mode.
- A selection cue that "appears on click and disappears when something else is clicked" can pass its recipe and still feel mushy when the camera is rotating.
- Chore rotation that "produces refusal cascade" can pass xUnit (the cascade fires) and still fail to read as Donna grumbling because the animation timing or the chibi-emotion popup is off.
- A new pixel-art shader that "renders the scene correctly" can pass every contract test and still look wrong because the dithering reads as banding at ×1 but as moiré at ×16.
- An audio synthesis change that "emits the correct sound trigger" can pass and still be jarring because the attenuation curve is wrong.

These are **integrated-whole, human-perception** claims. The verification harness for them is a human in front of the integrated scene over sustained play — i.e., a session of the Playtest Program.

**The rule:**

A packet whose acceptance criteria contain feel-level claims **must declare itself `feel-verified-by-playtest`** in the packet header. Specifically, the packet header includes:

```markdown
**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN:** <list — e.g., "camera pan-and-rotate at ×4 under load; selection-cue lag during camera motion">
```

When this flag is YES:

- xUnit and the sandbox 5-minute recipe still ship — they verify *contract* and *first-light*.
- The **next post-merge `PT-NNN` session** evaluates the surfaces listed and is the formal feel acceptance.
- The flag does **not** gate merge. The Playtest Program runs in parallel; phase dispatch is unaffected unless a Critical bug surfaces (per `docs/playtest/README.md`'s severity rubric).
- The flag **does** mean the packet is not "fully accepted" until a session evaluates it. Bugs found in that session feed normal `BUG-NNN` intake; the originating packet is referenced in the bug's `Discovered in:` field.

**Triggers — when does a packet need this flag?**

If any of the following is true, the answer is YES:

- The packet ships visual output the player will see (renderers, shaders, animation, lighting, UI).
- The packet ships motion or input-response behavior (camera, selection, drag, build placement, scrolling).
- The packet ships audio synthesis or trigger routing.
- The packet ships emergent gameplay surfaces where "does it read?" matters (chore rotation, bereavement cascade, plague propagation, fire spread).
- The packet's acceptance criteria contain language like "feels right," "reads as," "is satisfying," "is responsive," "doesn't jitter," "doesn't stutter," "looks correct," "matches the bible's tone."

If **none** of those is true — the packet is pure engine internals, schema-only, doc-only, or an algorithm-correctness change — the flag is NO and xUnit alone is sufficient.

**Anti-pattern: hiding feel claims behind contract assertions.** A packet that asserts `cameraSpeedDegPerSec >= 90` *implements* a contract for camera speed, but it does not *verify* that 90 deg/sec feels right. The contract assertion is necessary; it is not sufficient. The packet declares `feel-verified-by-playtest: YES` and lets the session decide whether 90 is the right number.

**See also:** `docs/playtest/README.md` for the program shape; `docs/c2-infrastructure/work-packets/_PACKET-COMPLETION-PROTOCOL.md` for how the flag interacts with the Variant B (Track 2) acceptance footer.

### Rule 7 — RETAIL build correctness requires a build-verification recipe

> **Authority:** Added 2026-05-02 in response to a discovery during WP-PT.1 playtesting that a Standalone Player build was producing 960+ compile errors. SRD §8.7 commits the engine to ship as RETAIL with `#if WARDEN`-gated code stripped clean. The Editor Play mode cannot detect when that strip is broken; only a real Standalone build can. See `docs/playtest/BUILD-VERIFICATION-RECIPE.md` for the canonical recipe.

Some failure modes are invisible to xUnit, the sandbox 5-minute recipe, the PT-NNN feel session, and even the Editor's Play mode. They appear only when the project is built as a RETAIL Standalone target — i.e., with `WARDEN` removed from the scripting defines. Examples:

- A non-`#if WARDEN`-guarded script references a WARDEN-only type. The reference compiles in the Editor (where WARDEN is defined). It fails to resolve in RETAIL — and cascades into hundreds of unrelated errors as dependent files fail to compile.
- A test asmdef (`Tests/Edit/*.asmdef` or `Tests/Play/*.asmdef`) is configured with `Include Platforms` = `Any Platform` instead of restricted to `Editor`. The test code gets compiled into the Player build and fails because `nunit.framework` / `UnityEngine.TestTools` aren't shipped to Standalone.
- A runtime script `using UnityEditor;` without an `#if UNITY_EDITOR` guard. The Editor doesn't notice; the Player build can't find the namespace.
- IL2CPP code-stripping at `Managed Stripping Level >= Low` removes a reflection target that `SceneBootstrapper.cs` needs at runtime. Compiles clean; crashes at boot.
- The build's scene list (`EditorBuildSettings.asset`) includes a `_Sandbox/*.unity` or `PlaytestScene.unity` that drags WARDEN-only dependencies into the build.

These are **build-correctness** failures, not feel failures. The verification harness for them is the **Build Verification Recipe** at `docs/playtest/BUILD-VERIFICATION-RECIPE.md`: switch target to Standalone, **remove WARDEN from scripting defines**, build, run the .exe, verify the engine runs cleanly without dev tooling, restore WARDEN.

**The rule:**

A packet that touches any build-configuration surface listed below **must declare itself `build-verified-by-recipe`** in the packet header:

```markdown
**Build-verified-by-recipe:** YES
**Why:** <which surface this packet touches — e.g., "adds new #if WARDEN files; modifies asmdef references">
```

Triggers (any one is sufficient):

- The packet modifies `ProjectSettings/ProjectSettings.asset` (especially `scriptingDefineSymbols`).
- The packet adds, removes, or modifies any `*.asmdef` file.
- The packet introduces new `#if WARDEN` blocks or removes existing ones.
- The packet adds files under `ECSUnity/Assets/Plugins/` or modifies plugin import settings.
- The packet modifies the build's scene list (`EditorBuildSettings.asset`).
- The packet adds new `using UnityEditor;` references in any runtime script under `Assets/Scripts/`.
- The packet ships a new MonoBehaviour or runtime script that uses reflection on private fields (relevant to IL2CPP stripping).

If **none** of those is true, the flag is `NO`. The recipe still runs periodically as phase-wave hygiene (see `docs/playtest/README.md`'s "When to run it" section), catching cumulative drift across packets that individually didn't trip the trigger.

**Compatibility with Rule 6 (`feel-verified-by-playtest`):** the two flags are independent. A single packet can carry `feel-verified-by-playtest: YES` (visual primitive that needs feel evaluation) AND `build-verified-by-recipe: YES` (the same packet also touches asmdefs). Both verification surfaces run; both can produce bugs.

**See also:** `docs/playtest/BUILD-VERIFICATION-RECIPE.md` for the recipe; `docs/c2-infrastructure/work-packets/_PACKET-COMPLETION-PROTOCOL.md` for how the flag appears in the Variant B (Track 2) acceptance footer.

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
