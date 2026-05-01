# Phase 3.1.S — Visual Sandbox Track Plan

> **Track:** Track 2 (Unity, atomic, sandbox-first) per `docs/PHASE-3-REALITY-CHECK.md`.
> **Protocol:** Every packet in this plan must comply with `docs/UNITY-PACKET-PROTOCOL.md`.
> **Authority:** Living document. Reorder freely as the sandbox track produces lessons.

---

## Why this plan exists

The 3.1.A–H bundle shipped tested and compiling, but the live Unity scene didn't behave as expected — six "Phase 3.1.x fix" commits patched the divergence after the fact. The new sandbox track replaces "ship eight packets in a bundle, integrate via reflection" with "one feature, one prefab, one sandbox scene, Talon validates in 5 minutes, integration is a separate one-line wiring packet."

This document names the first wave of sandbox packets in the order they should dispatch, with rationale for each ordering choice.

---

## First wave — foundation prefabs

The first four packets are the cheapest, lowest-risk extractions. They lock in fixes the live scene already needed and prove the protocol works on real code before any new behaviour is introduced.

### WP-3.1.S.0 — Camera Rig Sandbox **[SPECCED, READY TO DISPATCH]**

Extract the existing `CameraController` + `CameraInputBindings` + `CameraConstraints` into a `CameraRig.prefab` plus a 60×60 reference-grid sandbox scene. Locks in altitude/pan-speed/axis fixes from `abb9b3f`, `8f0420e`, `108d7dc`.

**Why first:** The smallest possible packet. Code already exists and is correct. Only refactor *packaging*, not behaviour. If this can't ship clean in 90 minutes, the protocol is wrong.

**Spec:** `docs/c2-infrastructure/work-packets/WP-3.1.S.0-camera-rig-sandbox.md`

---

### WP-3.1.S.1 — Selection Outline Sandbox

A `Selectable.cs` MonoBehaviour + outline shader (e.g., camera-stack-based or material-replacement) on a sandbox scene with a cube and a sphere. Click to select, click empty space to deselect. Outline thickness, colour, and pulse speed are `[SerializeField]` knobs Talon tunes by eye.

**Why second:** No engine dependency. Tests outline shader pipeline against URP / built-in render pipeline — answers a fundamental rendering question before any NPC integration. The shader work that lands here unblocks the future hover/highlight/tooltip surface (UX bible §3).

**Sandbox scene contents:** one cube at `(0, 0.5, 0)`, one sphere at `(2, 0.5, 0)`, the camera rig prefab from S.0, directional light.

**Out of scope:** No selection-driven inspector UI, no tooltips, no NPC selection. Just "thing was clicked → outline appears."

---

### WP-3.1.S.2 — Draggable Prop with Snap-to-Grid Sandbox

A `DraggableProp.cs` MonoBehaviour on a sandbox scene with a "table" prefab and a "banana" prefab. Drag the table on the grid; it snaps to integer tile positions. Drop a banana on the table; the banana parents to the table and snaps to a `[SerializeField]` socket.

**Why third:** Tests the fundamental build-mode interaction primitive (grab/move/place) without touching the engine's `IWorldMutationApi`. Talon validates feel — does snap distance feel right, is the grab visual obvious enough — before integration tries to call into engine mutations.

**Sandbox scene contents:** the camera rig, a 10×10 sub-region of the reference grid, two prefab instances (`Table.prefab`, `Banana.prefab`).

**Out of scope:** No `IWorldMutationApi` calls (that lives in the integration packet). No collision-with-walls validation. No multi-select. No undo. Just drag, snap, parent.

---

### WP-3.1.S.3 — Click-to-Inspect Popup Sandbox

A `Inspector​Popup.cs` MonoBehaviour + a UI canvas prefab, on a sandbox scene with a single cube. Click cube → popup appears next to cursor with three text fields (`Name`, `Drives`, `Mood`) populated from `[SerializeField]` test values. Click anywhere else → popup hides. Tunes typography, panel padding, drop-shadow per UX bible §4.2.

**Why fourth:** The third interaction primitive. After S.0 (camera), S.1 (highlight), S.2 (drag), S.3 establishes "click reveals data." Together these four sandboxes give Talon every visual language the player UI will eventually need, isolated and tunable.

**Out of scope:** No `WorldStateDto` consumption, no live data binding, no three-tier inspector tabs. Just a popup with hard-coded test values that Talon styles to taste.

---

## Second wave — integration packets

After the four sandbox packets ship and Talon has validated the prefabs in their sandbox scenes, integration packets wire them into the live engine scene one at a time.

### WP-3.1.S.0-INT — Wire CameraRig into MainScene

Replace `SceneBootstrapper.SetupCamera`'s programmatic camera setup with the validated `CameraRig.prefab` instance in `MainScene.unity`. Retire the reflection-based field-injection. Talon hand-wires `_engineHost` and `_wallFadeController` references in the Inspector once.

**Why:** The reflection bootstrap is the kind of trick that hid the missing-renderer bug for the entire 3.1.x bundle. Replacing it with explicit Inspector wiring is a structural improvement.

---

### WP-3.1.S.1-INT — Wire Selectable into NPC Renderer

Make `NpcDotRenderer`'s per-NPC quad use the validated `Selectable.cs` script. Click an NPC → outline appears around its dot.

**Why:** First link between visual interaction and engine entities. Establishes the click-routes-to-`EntityId` pattern that S.3-INT will reuse.

---

### WP-3.1.S.2-INT — Wire DraggableProp into Build Mode

The existing build-mode glue (3.1.D, currently in the live scene) gets reworked to use the validated `DraggableProp.prefab` workflow. `IWorldMutationApi.AddEntity` calls happen on drop instead of on a custom UI button.

**Why:** Replaces the existing build-mode prototype with the sandbox-validated pattern. Resolves the "build mode click+see-some-result" issue Talon flagged for Phase 4 deferral.

---

### WP-3.1.S.3-INT — Wire InspectorPopup into Selection

The popup from S.3 binds to the selected entity's `WorldStateDto` slice. Three tiers — Surface (Name + current action), Behaviour (drives + mood), Internal (workload + memory snippets) — match UX bible §4.2.

**Why:** First end-to-end binding from engine state to player-visible UI. Validates the projection seam once, then every future inspector field is a small additive change to the same prefab.

---

## Third wave — extension packets

Once the foundation prefabs and their integrations are in the live scene, additional sandbox packets layer on:

- **WP-3.1.S.4** — Time-control HUD (pause / ×1 / ×4 / ×16 buttons, current-time readout). Sandbox scene shows a fake clock; integration packet binds to `SimulationClock`.
- **WP-3.1.S.5** — Notification carrier (diegetic phone-ring / fax / email indicator per UX bible §3 and Q4 from the bible). Sandbox shows the icons cycling on a timer; integration binds to `NarrativeBus`.
- **WP-3.1.S.6** — CDDA-style event log scroll. Sandbox shows a hard-coded scroll of fake events; integration binds to the persistent chronicle.
- **WP-3.1.S.7** — Wall-fade tuning sandbox (extracted from existing 3.1.C wall-fade logic; re-validated in isolation).
- **WP-3.1.S.8** — Lighting visualization tuning (extracted from existing 3.1.C illumination code; tuned against a static room lit by varying values).
- **WP-3.1.S.9+** — Silhouette renderer evolution per archetype. The existing silhouette work from 3.1.B re-validated as a sandbox; future per-archetype designs (cast bible) layer on.

Each gets a corresponding `-INT` packet.

---

## Dispatch cadence

- **One sandbox packet at a time.** Sonnet ships, Talon verifies in the 5-minute test recipe, packet merges. Only then does the next one dispatch.
- **One integration packet at a time.** Same rule. Integration touches the live scene; never two at once.
- **Track 1 (engine) packets dispatch in parallel** with the sandbox track. They share zero file surface.
- **Bundles are forbidden** until further notice. The 3.1.A–H bundle proved the cost.

---

## Re-evaluation checkpoints

- **After WP-3.1.S.0 ships:** is the 90-minute timebox right? Is the prefab-extraction workflow smooth in Unity Editor, or did the Sonnet hit prefab-system friction? Adjust the protocol's Rule 3 if needed.
- **After WP-3.1.S.0-INT ships:** is the pattern of "sandbox packet then integration packet" delivering the time-savings the protocol promised? If integration takes longer than sandbox, the split is wrong.
- **After all four foundation sandbox packets ship:** redraft this plan based on what was learned. The third-wave list is a guess, not a commitment.

---

## What this plan does not include

- **Phase 4 work.** Deferred per the reality-check addendum. Re-evaluate when 3.0.x and 3.2.x close.
- **Visual playtesting / "feel" balance pass.** Per Talon's Q2(b) — that's later, requires multiple humans, and isn't a Sonnet-ownable workstream.
- **Track 1 engine packets.** Tracked in `docs/PHASE-3-REALITY-CHECK.md` and `docs/PHASE-3-PARALLEL-ENGINE-CANDIDATES.md`.

---

*The point of this plan is to make Track 2 small, predictable, and forward-marching. If the sandbox packets are stable and integrations cleanly compose, the visual layer of the project starts to feel as solid as the engine layer already does.*
