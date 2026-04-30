# WP-3.1.S.1 — Selection Outline Sandbox

> **Protocol:** Track 2 (visual sandbox) under `docs/UNITY-PACKET-PROTOCOL.md`.
> **Position in sub-phase:** Second sandbox packet. Establishes the click-to-select primitive on isolated geometry, with no engine and no NPCs.

**Tier:** Sonnet
**Depends on:** WP-3.1.S.0 (camera rig prefab — to drop into this sandbox scene). The camera rig is *used* in the sandbox; nothing on this packet's surface depends on its internals.
**Parallel-safe with:** All Track 1 engine packets. All other Track 2 sandbox packets.
**Timebox:** 120 minutes
**Budget:** $0.50

---

## Goal

Establish the **click-to-select** primitive in Unity as a fully sandboxed feature: a `Selectable.cs` MonoBehaviour, a screen-space outline shader, and a sandbox scene with one cube and one sphere. Click a primitive → outline appears around it. Click empty space or another primitive → previous outline clears, new one appears (or none).

After this packet, the visual selection language exists, tuned, and ready for any future packet that wants to highlight an entity (an NPC, a piece of furniture, a hazard). Future packet `WP-3.1.S.1-INT` will wire this prefab onto `NpcDotRenderer`'s per-NPC quads to produce the first selectable NPC; that integration is out of scope here.

This packet ships:

1. `Assets/Scripts/Selection/Selectable.cs` — MonoBehaviour attached to any GameObject that should be clickable. Holds the selection state, exposes a public event when selected/deselected.
2. `Assets/Scripts/Selection/SelectionManager.cs` — singleton-ish MonoBehaviour that owns the "currently selected" reference and broadcasts the click → ray-cast → `Selectable.Select()` flow.
3. `Assets/Scripts/Selection/OutlineRenderer.cs` — script that draws an outline around a GameObject's mesh when its `Selectable` is selected. Approach: simple two-pass rendering (back-faces dilated, then front faces) using a shared `Outline.shader`. (See **Implementation notes** below for shader options.)
4. `Assets/Shaders/Outline.shader` — minimal screen-aligned outline shader compatible with the built-in render pipeline. Single-color, configurable thickness, configurable pulse speed.
5. `Assets/Prefabs/Selectable.prefab` — a wrapper prefab that bundles `Selectable.cs` + `OutlineRenderer.cs` so future scenes drag-and-drop a single component reference.
6. `Assets/_Sandbox/selection-outline.scene` — sandbox scene per the protocol.
7. `Assets/_Sandbox/selection-outline.md` — 5-minute test recipe.

---

## Reference files (read in this order)

The Sonnet should read these inline before implementing. All paths are relative to the repo root.

1. `docs/UNITY-PACKET-PROTOCOL.md` — load-bearing protocol. Especially Rule 3 (Sonnet does not edit `MainScene.unity`) and Rule 4 (test recipe).
2. `docs/PHASE-3-1-S-PLAN.md` — context on where this packet sits in the sandbox sub-phase.
3. `docs/c2-content/ux-ui-bible.md` §3 (selection / inspection) — the design intent for selection feedback. Ghost-camera, layered disclosure. The outline is the entry point for "I want to know about this."
4. `docs/c2-content/aesthetic-bible.md` — colour palette commitments. The outline colour should sit comfortably in the early-2000s-office aesthetic.
5. `ECSUnity/Assets/Scripts/Camera/CameraController.cs` — the camera the sandbox uses. The selection raycast originates from `Camera.main`; this script's `Camera` reference is the one the raycast will use.
6. `ECSUnity/Assets/Scripts/Render/RenderColorPalette.cs` — existing palette helper. The outline colour can either be a new entry in this palette or an Inspector-tunable on the `Selectable.prefab` directly. **Prefer the Inspector tunable** — keeps the palette focused on entity colours.

---

## Non-goals

- Do **not** integrate with `NpcDotRenderer` or any engine-side renderer. Integration is `WP-3.1.S.1-INT`, out of scope here.
- Do **not** modify `MainScene.unity` or any scene outside `Assets/_Sandbox/`.
- Do **not** modify `WorldStateProjectorAdapter`, `EngineHost`, or any file under `Assets/Scripts/Engine/`.
- Do **not** add multi-select, drag-rectangle-select, group-select, or modifier-key-select. Single-click single-select only at v0.1.
- Do **not** add hover-highlight or pre-selection states. Just selected-or-not.
- Do **not** add a tooltip, label, or any UI panel. The outline is the entire visual language. Future packets layer text on top.
- Do **not** ship per-archetype outline colour variants. One outline colour, configurable.
- Do **not** add Cinemachine or any URP / HDRP dependency. Built-in render pipeline only — that's what `RoomTint.shader` and the existing `Unlit/Color` materials use.
- Do **not** introduce new test infrastructure. Existing `ECSUnity.Tests` patterns apply if you write any tests; otherwise the test recipe handles verification.
- Do **not** reflection-bootstrap selection from a `RuntimeInitializeOnLoadMethod`. The protocol forbids this pattern.

---

## Design notes

### Outline implementation

Two viable approaches; pick one in implementation, document the choice in the completion note:

**Option A — Inverted-hull outline (classic).** Duplicate the mesh at slight scale-up, render the duplicate with front-face culling and an unlit colour. Cheap, no shader-pass dependencies, works in built-in pipeline. Caveat: thickness varies with object size — a small sphere gets a thinner outline than a big cube unless you compensate.

**Option B — Stencil-buffer outline.** Render selected objects into a stencil mask, then draw a fullscreen quad that samples the stencil and emits an outline at edges. Better visual result, constant thickness regardless of object size. More complex; requires `CommandBuffer` setup or a custom `OnRenderImage` MonoBehaviour.

**Recommendation:** Option A for v0.1. Cheaper, smaller surface area, easier to verify in the recipe. Talon can request Option B in a follow-up packet if the look isn't right.

### Selection input

Use `Camera.main.ScreenPointToRay(Input.mousePosition)` and `Physics.Raycast`. Selectables get a `Collider` (BoxCollider on the cube, SphereCollider on the sphere — the prefab's `Selectable.prefab` includes one auto-added). Click on empty space → ray hits nothing → deselect current.

Avoid the new Input System dependency for this packet; it's already pulled in for camera input, but keep selection on the legacy `Input` API for simplicity. If the legacy API is unavailable in the project's input mode, document the issue and escalate.

### Selection events

`Selectable` exposes:
```csharp
public event Action<Selectable> OnSelected;
public event Action<Selectable> OnDeselected;
public bool IsSelected { get; }
```

`SelectionManager` exposes:
```csharp
public event Action<Selectable> OnSelectionChanged; // fires with new selection or null
public Selectable CurrentSelection { get; }
public void SelectByRaycast(); // internal use; called every Update
public void Deselect();         // explicit clear, e.g. on Esc
```

A future integration packet (`WP-3.1.S.3-INT`) wires `SelectionManager.OnSelectionChanged` to the inspector popup.

### Outline parameters (Inspector contract)

`OutlineRenderer.cs` exposes:

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `_outlineColor` | `Color` | `(0.4, 0.85, 1.0, 1.0)` (cyan) | — | Outline tint |
| `_outlineThickness` | `float` | `0.05f` | `[0.01, 0.5]` | World-units of outline scale-up |
| `_pulseSpeed` | `float` | `0f` | `[0, 5]` | Hz; 0 = no pulse, static outline |
| `_pulseAmount` | `float` | `0.0f` | `[0, 0.5]` | Thickness modulation amount when pulsing |

`SelectionManager.cs` exposes:

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `_camera` | `Camera` | `null` (auto-grabs `Camera.main` if null) | — | Selection ray source |
| `_selectableLayerMask` | `LayerMask` | everything | — | Which layers `Physics.Raycast` considers |
| `_clickButton` | `int` | `0` (left mouse) | `[0, 2]` | `Input.GetMouseButtonDown` index |

Add `[Tooltip(...)]` and `[Range(...)]` annotations to all fields above.

---

## Sandbox scene composition (`Assets/_Sandbox/selection-outline.scene`)

The scene contains:

- One instance of `Assets/Prefabs/CameraRig.prefab` (from WP-3.1.S.0) at default position.
- One `Directional Light` matching `SceneBootstrapper.SetupDirectionalLight` (warm tint, intensity 1.1, rotation Euler `(50, -30, 0)`).
- One `Cube` primitive at `(28, 0.5, 18)` with `Selectable.prefab` components attached. Material: `Unlit/Color`, colour `(0.85, 0.85, 0.85)` (light grey).
- One `Sphere` primitive at `(32, 0.5, 22)` with `Selectable.prefab` components attached. Material: `Unlit/Color`, colour `(0.85, 0.85, 0.85)` (light grey).
- A small reference grid (10×10, lighter than S.0's full 60×60) so Talon has spatial reference but isn't distracted.
- A `SelectionManager` GameObject at `(0, 0, 0)` (no transform meaning) holding the manager singleton.

The scene is composed for one purpose: click each primitive in turn and watch the outline behaviour. Nothing else.

---

## Test recipe (`Assets/_Sandbox/selection-outline.md`)

The Sonnet writes this verbatim. Do not paraphrase:

```markdown
# Selection Outline Sandbox

## What this validates
Click-to-select primitive: Selectable.cs + OutlineRenderer.cs + Outline.shader
working together to highlight a clicked GameObject and clear the highlight
when something else (or nothing) is clicked.

## Setup (one-time, after pulling this packet)
None — the scene already has the cube and sphere placed.

## Test (every run)
1. Open Assets/_Sandbox/selection-outline.scene.
2. Press Play.
3. Move the camera so both the cube and the sphere are clearly visible.
4. Click the cube. Expect: cyan outline appears around the cube.
5. Click the sphere. Expect: cube's outline disappears, sphere's outline
   appears.
6. Click the empty floor (the grid). Expect: sphere's outline disappears.
   No new outline.
7. Click the cube again, then press Esc. Expect: outline clears.

## Tuning pass (Talon)
Open the Selectable.prefab in the Inspector. Adjust:
- _outlineColor — try a few options. Keep one Talon likes.
- _outlineThickness — eyeball it at 0.05 default; raise/lower to taste.
- _pulseSpeed — leave at 0 unless the static outline feels lifeless.

Save the prefab. Commit the tuned values.

## Expected
- Outline appears within one frame of click.
- Outline thickness ~0.05 world-units, visually clear at default camera
  altitude (~25 units).
- No outline visible when nothing is selected.
- Outline doesn't flicker, doesn't z-fight with the object's surface.
- 60+ FPS in editor.

## If it fails
- Click does nothing → SelectionManager.cs not in scene, or _camera field
  is null and Camera.main isn't tagged correctly.
- Outline doesn't render → Outline.shader compile error (check the console)
  or material reference broken on the prefab.
- Outline z-fights the object → _outlineThickness is too small. Raise.
- Outline appears around everything at once → SelectionManager isn't
  tracking the previous selection. Bug in OnSelectionChanged.
- Click on empty space throws an exception → ray hit nothing path is not
  guarded.
```

---

## Acceptance criteria

1. `Assets/Scripts/Selection/Selectable.cs`, `SelectionManager.cs`, `OutlineRenderer.cs` exist with the public APIs listed in **Design notes**.
2. `Assets/Shaders/Outline.shader` compiles without warnings in the Unity built-in render pipeline.
3. `Assets/Prefabs/Selectable.prefab` instantiates cleanly when added to a scene.
4. `Assets/_Sandbox/selection-outline.scene` opens cleanly with no missing-script warnings.
5. `Assets/_Sandbox/selection-outline.md` is verbatim the recipe in **Test recipe** above.
6. The Inspector contract tables in **Design notes** are reflected in `[Tooltip]` + `[Range]` annotations on the corresponding `[SerializeField]` fields.
7. **No file outside the paths listed in §"This packet ships" is modified.** Verify with `git diff --name-only`.
8. `MainScene.unity` is unchanged. `NpcDotRenderer.cs` and `RoomRectangleRenderer.cs` are unchanged.
9. `dotnet test` passes; `dotnet build` is clean.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

This is a Track 2 (Unity) packet. xUnit tests are necessary but **not sufficient** — the visual layer must be verified by Talon in Unity Editor before PR is mergeable.

The Sonnet executor's pipeline:

1. Implement the spec — write `Selectable.cs`, `SelectionManager.cs`, `OutlineRenderer.cs`, `Outline.shader`, build `Selectable.prefab`, compose `Assets/_Sandbox/selection-outline.scene`, write `Assets/_Sandbox/selection-outline.md`.
2. Run `dotnet test` and `dotnet build`. Must be green.
3. Stage all changes including the self-cleanup deletion (see below).
4. Commit on the worktree's feature branch.
5. Push the branch.
6. Stop. Do **not** open a PR yet. Do **not** merge.
7. The final line of the commit message must be: `READY FOR VISUAL VERIFICATION — run Assets/_Sandbox/selection-outline.md`.

Talon's pipeline (after Sonnet's push):

1. Open the Unity Editor on the feature branch.
2. Run the test recipe at `Assets/_Sandbox/selection-outline.md`.
3. If the recipe passes: open the PR, merge to `staging`.
4. If the recipe fails: file the failure as PR review comments or as a follow-up packet. Do not ask the original Sonnet to iterate ad-hoc.

### Cost envelope (1-5-25 Claude army)

Target: **$0.50** (120-minute timebox). If costs approach $1.00 without the recipe being achievable, **escalate to Talon** with a `WP-3.1.S.1-blocker.md` note.

Unity-specific cost-discipline:
- Pick Option A (inverted-hull) on the first try. Don't go down the Option B (stencil) rabbit hole unless A measurably fails.
- The `Outline.shader` is short — write it once, test it once, ship it. Don't iterate on appearance; that's Talon's tuning pass.
- `Physics.Raycast` debugging: if a click doesn't register, check the LayerMask first, then the Collider presence. Don't chase ghosts.

### Self-cleanup on merge

Before opening the PR (after Talon's visual verification passes), the executing Sonnet must:

1. **Check downstream dependents:**
   ```bash
   git grep -l "WP-3.1.S.1" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```

2. **Expected result:** `WP-3.1.S.1-INT-selection-into-npc-renderer.md` exists as a downstream dependent. **Leave the spec file in place** and add the status header:
   ```markdown
   > **STATUS:** SHIPPED to staging YYYY-MM-DD. Retained because pending packet `WP-3.1.S.1-INT` depends on this spec.
   ```
   Add `Self-cleanup: spec retained, dependents: WP-3.1.S.1-INT.` to the commit message.

3. The shader, scripts, and prefab live in their final paths and are not deleted.

4. **Do not touch** files under `_completed/` or `_completed-specs/`.

---

*WP-3.1.S.1 establishes the selection visual language. The integration packet `-INT` later swaps `Selectable.prefab` onto NPC dots so the player can pick an NPC. That step is one Inspector-wiring change once this packet's prefab exists.*
