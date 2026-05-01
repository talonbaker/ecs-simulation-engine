# WP-3.1.S.3 — Click-to-Inspect Popup Sandbox

> **STATUS:** SHIPPED to staging 2026-04-30. Retained because pending packet `WP-3.1.S.3-INT` depends on this spec.

> **Protocol:** Track 2 (visual sandbox) under `docs/UNITY-PACKET-PROTOCOL.md`.
> **Position in sub-phase:** Fourth and final foundation sandbox packet. Establishes the click-reveals-data primitive on isolated geometry.

**Tier:** Sonnet
**Depends on:** WP-3.1.S.0 (camera rig prefab — used in the sandbox scene). Optional: WP-3.1.S.1 (selection). If S.1 has merged, the popup is driven by `SelectionManager.OnSelectionChanged`. If not, the popup uses its own internal click handler. **The packet ships both code paths and chooses at runtime.**
**Parallel-safe with:** All Track 1 engine packets. All other Track 2 sandbox packets.
**Timebox:** 120 minutes
**Budget:** $0.50

---

## Goal

Establish the **click-reveals-data** primitive in Unity as a fully sandboxed feature: a `InspectorPopup.cs` MonoBehaviour, a UI canvas prefab with three text fields (`Name`, `Drives`, `Mood`), and a sandbox scene with a single clickable cube. Click cube → popup appears next to cursor, shows hard-coded test values. Click anywhere else → popup hides.

After this packet, the inspector visual language exists, tuned, and ready for any future packet that wants to surface entity data on click. Future packet `WP-3.1.S.3-INT` will bind the popup to the selected entity's `WorldStateDto` slice and add the three-tier disclosure pattern from UX bible §4.2. That binding work is out of scope here.

This packet ships:

1. `Assets/Scripts/UI/InspectorPopup.cs` — MonoBehaviour. Owns the popup canvas, three text references, and a `Show(InspectorPopupData)` method. Hides on `Hide()` or on click-outside.
2. `Assets/Scripts/UI/InspectorPopupData.cs` — plain data struct passed to `Show`. Three fields: `Name`, `Drives`, `Mood`.
3. `Assets/Scripts/UI/PopupClickHandler.cs` — fallback click handler used when `SelectionManager` from S.1 isn't present. Performs its own raycast against `InspectorTarget` components and routes to the popup. If `SelectionManager` IS present, this script subscribes to `OnSelectionChanged` instead (no own raycast).
4. `Assets/Scripts/UI/InspectorTarget.cs` — marker MonoBehaviour. Attach to any GameObject that should reveal the popup on click. Holds the `InspectorPopupData` payload that the popup will display.
5. `Assets/Prefabs/UI/InspectorPopupCanvas.prefab` — Unity UI canvas (`Canvas` in `Screen Space - Overlay` mode) with a panel, drop-shadow, and three TextMeshProUGUI children for `Name`, `Drives`, `Mood`.
6. `Assets/_Sandbox/inspector-popup.scene` — sandbox scene per the protocol.
7. `Assets/_Sandbox/inspector-popup.md` — 5-minute test recipe.

---

## Reference files (read in this order)

1. `docs/UNITY-PACKET-PROTOCOL.md` — load-bearing protocol.
2. `docs/PHASE-3-1-S-PLAN.md` — context.
3. `docs/c2-content/ux-ui-bible.md` §4 (inspection / disclosure) — the design intent for the inspector. Three-tier layered disclosure. The popup at v0.1 ships only the surface tier (Name + current action). Tier 2 (drives, mood) is shown for tuning purposes but won't be in the integration packet's first version.
4. `docs/c2-content/aesthetic-bible.md` §UI — typography, panel materials, drop-shadow style. CRT-era. No floating-emoji-popup-modern-game.
5. `ECSUnity/Assets/Scripts/Camera/CameraController.cs` — the camera the sandbox uses.
6. `ECSUnity/Assets/Scripts/Selection/SelectionManager.cs` — *if S.1 has merged.* If not present yet, the popup falls back to `PopupClickHandler.cs`. The Sonnet should check at the start of the packet whether `SelectionManager.cs` exists (`git ls-files ECSUnity/Assets/Scripts/Selection/`) and structure the integration accordingly.
7. Unity TextMeshPro package — the popup uses `TextMeshProUGUI`. Confirm the package is in `ECSUnity/Packages/manifest.json`; if not, add it as a dependency.

---

## Non-goals

- Do **not** bind to `WorldStateDto`. Integration is `WP-3.1.S.3-INT`, out of scope. Hard-coded test values only at v0.1.
- Do **not** modify `MainScene.unity` or any scene outside `Assets/_Sandbox/`.
- Do **not** modify any file under `Assets/Scripts/Engine/` or `Assets/Scripts/Render/`.
- Do **not** ship the three-tier disclosure (Surface / Behaviour / Internal). Single tier (the three fields) at v0.1; tiers land in the integration packet.
- Do **not** add tabs, scrollbars, or expand-collapse animations. One static panel with three lines of text.
- Do **not** add tooltips, secondary popups, or modal dialogs.
- Do **not** add localization. English strings hard-coded.
- Do **not** add the persistent chronicle reader (CDDA-style log). Future packet (`WP-3.1.S.6`).
- Do **not** add notifications, banners, toasts, or alert sounds.
- Do **not** introduce a new UI framework (UI Toolkit, IMGUI). Standard Unity UGUI Canvas.
- Do **not** ship per-archetype popup styles.

---

## Design notes

### Show / hide mechanics

`InspectorPopup.Show(data)` sets the three text fields and enables the canvas. `InspectorPopup.Hide()` disables the canvas. State machine on `_isVisible` with `OnEnable`/`OnDisable` driving `Canvas.enabled`.

The popup is positioned at the screen-space cursor location offset by `_cursorOffset` (default `(20, 20)` so it sits down-and-right of the cursor; configurable). When the cursor is too close to a screen edge, the popup flips to the opposite side (right edge → left of cursor; bottom edge → above). Edge-flip logic in `InspectorPopup.UpdatePosition()`.

### Click-outside dismiss

A transparent fullscreen `Image` on the popup canvas (interactable, blocking raycasts) catches clicks outside the panel and triggers `Hide()`. The panel itself blocks the raycast from reaching the fullscreen image, so clicks on the panel don't dismiss it.

(Alternative: poll `Input.GetMouseButtonDown(0)` in `Update`, raycast against the panel, dismiss if no hit. Both work. Prefer the canvas approach — less per-frame work.)

### S.1 integration (conditional)

Two integration patterns; pick at runtime:

- **If `SelectionManager` exists** (`SelectionManager.Instance != null`): subscribe `InspectorPopup` to `SelectionManager.OnSelectionChanged`. When selection changes to a `Selectable` whose GameObject also has `InspectorTarget`, fetch the target's `InspectorPopupData` and call `Show`. When selection changes to null, call `Hide`.
- **If no `SelectionManager`** (S.1 not merged yet): `PopupClickHandler.cs` performs its own `Camera.main.ScreenPointToRay` + `Physics.Raycast` against `InspectorTarget` components. Same flow as the S.1 manager, scoped to popup.

The Sonnet implements both paths, and `InspectorPopup.Awake()` chooses at runtime via:

```csharp
var sm = FindObjectOfType<SelectionManager>();
if (sm != null) {
    sm.OnSelectionChanged += HandleSelectionChanged;
} else {
    var fallback = gameObject.AddComponent<PopupClickHandler>();
    fallback.OnClick += HandlePopupClick;
}
```

### Inspector contracts

`InspectorPopup.cs`:

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `_canvas` | `Canvas` | `null` (assigned in prefab) | — | The popup root canvas |
| `_nameText` | `TMP_Text` | `null` (assigned in prefab) | — | Name field |
| `_drivesText` | `TMP_Text` | `null` (assigned in prefab) | — | Drives field |
| `_moodText` | `TMP_Text` | `null` (assigned in prefab) | — | Mood field |
| `_cursorOffset` | `Vector2` | `(20, 20)` | — | Pixels offset from cursor |
| `_panelPadding` | `float` | `12f` | `[0, 50]` | Padding inside the panel |

`InspectorTarget.cs`:

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `_data` | `InspectorPopupData` | (default-constructed) | — | The Name/Drives/Mood payload to show |

`PopupClickHandler.cs` exposes:
```csharp
public event Action<InspectorTarget> OnClick;
public void Activate();   // start listening
public void Deactivate(); // stop listening
```

Add `[Tooltip(...)]` and `[Range(...)]` annotations.

---

## Sandbox scene composition (`Assets/_Sandbox/inspector-popup.scene`)

The scene contains:

- One instance of `Assets/Prefabs/CameraRig.prefab` from S.0.
- One `Directional Light` matching established sandbox lighting.
- A small reference grid (5×5) — minimal context.
- One `Cube` primitive at `(2, 0.5, 2)` with both `Selectable.prefab` (if S.1 has shipped) and `InspectorTarget.cs` attached. The `_data` field is hard-coded:
  - `Name`: `"Sandbox Cube"`
  - `Drives`: `"Hunger 30 / Thirst 12 / Stress 45"`
  - `Mood`: `"Bored, mildly anxious"`
- One instance of `Assets/Prefabs/UI/InspectorPopupCanvas.prefab` parented to the scene root. Initially hidden.
- An `InspectorPopup` GameObject holding the manager script, with references to the popup canvas and text fields wired in the prefab itself.

The scene is composed for one purpose: click cube → popup. Click empty space → no popup. That's it.

---

## Test recipe (`Assets/_Sandbox/inspector-popup.md`)

Verbatim:

```markdown
# Inspector Popup Sandbox

## What this validates
Click-reveals-data primitive: InspectorPopup.cs + InspectorTarget.cs +
the popup canvas prefab working together to show a small data panel
when a clickable object is clicked, hide it on click-outside.

## Setup (one-time)
None — the scene already has a clickable cube and the popup canvas.

## Test (every run)
1. Open Assets/_Sandbox/inspector-popup.scene.
2. Press Play.
3. Click the cube. Expect: popup appears down-and-right of the cursor with
   three lines:
   - Name: Sandbox Cube
   - Drives: Hunger 30 / Thirst 12 / Stress 45
   - Mood: Bored, mildly anxious
4. Click the cube again. Popup stays (showing same data — clicking the
   already-selected target is idempotent).
5. Move cursor near the right edge of the screen and click somewhere
   that triggers a popup. Expect: popup flips to the left side of the
   cursor (edge-flip logic).
6. Click on empty floor (the grid). Expect: popup disappears.
7. Press Play, click the cube, then press Esc. Expect: popup
   disappears. (Optional — only if Esc binding is wired.)

## Tuning pass (Talon)
Open InspectorPopupCanvas.prefab in the Inspector. Adjust:
- Panel background colour, alpha — should sit comfortably with the
  established palette (CRT-era; not modern-game).
- TextMeshPro font asset — pick a typeface from the project's font set
  that reads as early-2000s.
- Drop shadow distance / strength — readable but not tacky.
- _cursorOffset on InspectorPopup — try (16, 16) for tighter, (32, 32)
  for looser.
- Padding inside the panel.

Save the prefab. Commit the tuned values.

## Expected
- Popup appears within one frame of click.
- Three text lines render cleanly with no visible Unicode boxes.
- Edge-flip works on right and bottom screen edges.
- Click-outside dismissal is reliable.
- 60+ FPS.
- Popup z-orders above all 3D geometry (it's a Screen Space - Overlay
  canvas, so it should be).

## If it fails
- Click does nothing → InspectorPopup not subscribed to SelectionManager,
  or PopupClickHandler not active, or InspectorTarget missing on the cube.
- Popup appears but is empty → text references on the prefab are null.
- Popup is huge / tiny → Canvas Scaler isn't set up. Set Reference
  Resolution to 1920×1080 and Match to 0.5.
- Popup follows cursor jitterily during click-outside → fullscreen blocker
  image is registering hover events. Should only block click events.
- Popup never dismisses → the click-outside path isn't wired.
- Edge-flip doesn't fire → screen-edge math wrong, likely missing
  RectTransform.rect.size lookup.
```

---

## Acceptance criteria

1. `Assets/Scripts/UI/InspectorPopup.cs`, `InspectorPopupData.cs`, `PopupClickHandler.cs`, `InspectorTarget.cs` exist with the public APIs implied by the design notes.
2. `Assets/Prefabs/UI/InspectorPopupCanvas.prefab` instantiates cleanly with no missing-script warnings.
3. `Assets/_Sandbox/inspector-popup.scene` opens cleanly.
4. `Assets/_Sandbox/inspector-popup.md` is verbatim the recipe in **Test recipe**.
5. The Inspector contract tables are reflected in `[Tooltip]` + `[Range]` annotations.
6. The conditional integration with `SelectionManager` (if S.1 has shipped) works without compile errors when S.1 is absent.
7. **No file outside `Assets/Scripts/UI/`, `Assets/Prefabs/UI/`, and `Assets/_Sandbox/` is modified.** Verify with `git diff --name-only`.
8. `MainScene.unity` is unchanged. `NpcDotRenderer.cs`, `RoomRectangleRenderer.cs`, all engine code unchanged.
9. `dotnet test` passes; `dotnet build` is clean.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

This is a Track 2 (Unity) packet. xUnit tests are necessary but **not sufficient** — the visual layer must be verified by Talon in Unity Editor before PR is mergeable.

The Sonnet executor's pipeline:

1. Implement the spec — write the four scripts, build the popup canvas prefab, compose the sandbox scene, write the recipe.
2. Confirm the conditional `SelectionManager` integration compiles whether S.1 has shipped or not.
3. Run `dotnet test` and `dotnet build`. Must be green.
4. Stage all changes including the self-cleanup deletion (see below).
5. Commit on the worktree's feature branch.
6. Push the branch.
7. Stop. Do **not** open a PR yet.
8. The final line of the commit message must be: `READY FOR VISUAL VERIFICATION — run Assets/_Sandbox/inspector-popup.md`.

Talon's pipeline (after Sonnet's push):

1. Open the Unity Editor on the feature branch.
2. Run the test recipe at `Assets/_Sandbox/inspector-popup.md`.
3. If the recipe passes: open the PR, merge to `staging`.
4. If the recipe fails: PR review comments or follow-up packet. Don't iterate ad-hoc.

### Cost envelope (1-5-25 Claude army)

Target: **$0.50** (120-minute timebox). Escalate to Talon with a `WP-3.1.S.3-blocker.md` note if costs approach $1.00 without acceptance.

Unity-specific cost-discipline:
- TextMeshPro asset import can be slow on first add. If the package isn't already in the project, do the import once and keep going.
- Don't iterate on visual styling — that's Talon's tuning pass. Ship sensible defaults; let Talon refine.
- Click-outside detection has many implementations; pick the canvas-blocker approach and stick with it.

### Self-cleanup on merge

Before opening the PR (after Talon's visual verification passes), the executing Sonnet must:

1. **Check downstream dependents:**
   ```bash
   git grep -l "WP-3.1.S.3" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```

2. **Expected result:** `WP-3.1.S.3-INT-popup-into-selection.md` exists as a downstream dependent. **Leave the spec file in place** and add the status header:
   ```markdown
   > **STATUS:** SHIPPED to staging YYYY-MM-DD. Retained because pending packet `WP-3.1.S.3-INT` depends on this spec.
   ```
   Add `Self-cleanup: spec retained, dependents: WP-3.1.S.3-INT.` to the commit message.

3. The scripts and prefab live in their final paths and are not deleted.

4. **Do not touch** files under `_completed/` or `_completed-specs/`.

---

*WP-3.1.S.3 establishes the click-reveals-data language. The integration packet `-INT` later binds the popup to live `WorldStateDto` data and lands the three-tier disclosure (Surface / Behaviour / Internal) from UX bible §4.2.*
