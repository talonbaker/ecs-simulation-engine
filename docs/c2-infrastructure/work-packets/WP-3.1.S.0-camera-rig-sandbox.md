# WP-3.1.S.0 — Camera Rig Sandbox

> **Protocol:** This packet is the inaugural Track 2 packet under `docs/UNITY-PACKET-PROTOCOL.md`. Read that document before drafting or dispatching anything else under the Track 2 sandbox track.
> **Sub-phase:** 3.1.S (visual sandbox). Parallel to the now-complete 3.1.A–H bundle, scoped to atomic, sandbox-first, Inspector-driven Unity work.

**Tier:** Sonnet
**Depends on:** Existing 3.1.x scaffold (`ECSUnity/`, `CameraController.cs`, `CameraInputBindings.cs`, `CameraConstraints.cs`)
**Parallel-safe with:** All Track 1 engine packets (WP-3.0.4, WP-3.0.3, WP-3.0.5, WP-3.2.x). Disjoint file surface.
**Timebox:** 90 minutes
**Budget:** $0.40

---

## Goal

Extract the existing camera trio (`CameraController.cs`, `CameraInputBindings.cs`, `CameraConstraints.cs`) into a **standalone, Inspector-tunable `CameraRig.prefab`**, and ship a **`Assets/_Sandbox/camera-rig.scene`** that demonstrates it on a bare 60×60 reference grid with no engine, no NPCs, no `WorldStateDto`. The camera fixes that landed in commits `abb9b3f`, `8f0420e`, and `108d7dc` (altitude 5–50, pan speed 20, mouse drag 5× sensitivity, axis convention) get **locked into the prefab's Inspector defaults** so future scenes inherit known-good values.

After this packet, the camera is a settled artifact: any future scene that needs camera control drags `CameraRig.prefab` in, presses play, and gets the same feel.

This is the simplest possible Track 2 packet. It validates the protocol on real code that already exists, with no new behaviour to debug. If the protocol can't ship a sandbox-extracted camera in 90 minutes, the protocol needs revision. That's the point.

---

## Reference files

- `docs/UNITY-PACKET-PROTOCOL.md` — **read in full.** All five rules apply. Particularly Rule 2 (sandbox-then-integration), Rule 3 (no scene mutations outside `Assets/_Sandbox/`), Rule 4 (5-minute test recipe), Rule 5 (Inspector contracts).
- `docs/PHASE-3-REALITY-CHECK.md` — context on why this sub-phase exists.
- `docs/c2-content/ux-ui-bible.md` §2.1 — camera commitments. Single-stick-equivalent. Fixed altitude under ceiling. Walls-fade-on-occlusion. Lazy-susan rotate. Bounded zoom.
- `ECSUnity/Assets/Scripts/Camera/CameraController.cs` — read in full. **Do not rewrite logic.** Reference behaviour is already correct as of `108d7dc`.
- `ECSUnity/Assets/Scripts/Camera/CameraInputBindings.cs` — read in full. The grab-style mouse drag negation from `8f0420e` and 5× sensitivity from `abb9b3f` must be preserved.
- `ECSUnity/Assets/Scripts/Camera/CameraConstraints.cs` — read in full. Clamp logic is settled.
- `ECSUnity/Assets/Scripts/Engine/SceneBootstrapper.cs` — read for the altitude/start-focus values it currently injects via reflection (`startFocus = (30, 0, 20)`, focus on the office-starter centre). The sandbox scene's grid is centred at the same point so the prefab's defaults work in both contexts.
- Recent fix commits: `abb9b3f` (camera altitude/mouse speed), `8f0420e` (mouse drag direction), `108d7dc` (NPC dot axis — for context on the engine's coordinate convention; X = column, Z = depth, Y = vertical floor 0).

---

## Non-goals

- Do **not** modify `CameraController.cs`, `CameraInputBindings.cs`, or `CameraConstraints.cs` logic. Behaviour is already correct. This packet only refactors *packaging* — extraction into a prefab.
- Do **not** modify `MainScene.unity` or `SceneBootstrapper.cs`. Integration is **WP-3.1.S.0-INT**, a separate follow-up packet.
- Do **not** add any new camera features (orbit modes, zoom presets, inertia, smooth pan, edge-pan, screen-shake, follow-target). Future packets only.
- Do **not** add a Cinemachine dependency. Hand-rolled controller stays.
- Do **not** modify `RoomRectangleRenderer`, `NpcDotRenderer`, or anything in `ECSUnity/Assets/Scripts/Render/`.
- Do **not** import any additional Unity packages.
- Do **not** ship play-mode tests for the sandbox scene. The sandbox is verified manually per the test recipe; the existing `CameraControllerTests.cs` (or equivalent under `ECSUnity.Tests/`) continues to cover the script's logic.
- Do **not** introduce a `RuntimeInitializeOnLoadMethod` for the sandbox. The scene is composed via Inspector; the prefab is dragged in manually.

---

## Deliverables

1. **`Assets/Prefabs/CameraRig.prefab`**
   A prefab containing:
   - A child `Main Camera` GameObject with `Camera`, `AudioListener`, and `CameraController` components.
   - The `CameraController`'s `[SerializeField]` defaults set to the values currently working in `MainScene.unity` post-`108d7dc`:
     - `_panSpeed = 20f`
     - `_rotateSpeed = 90f`
     - `_zoomSpeed = 5f`
     - `_minAltitude = 5f`, `_maxAltitude = 50f`, `_pitchAngle = 50f`
     - `_startFocusPoint = (30f, 0f, 20f)`, `_startYaw = 0f`
   - The `Camera` field defaults: `clearFlags = SolidColor`, `backgroundColor = (0.08, 0.08, 0.10, 1)`, `fieldOfView = 60`, `nearClipPlane = 0.3`, `farClipPlane = 1000`. (Match `SceneBootstrapper.SetupCamera`.)
   - The `_engineHost` and `_wallFadeController` fields **left null** in the prefab. They are populated only in integration scenes by Talon's hand.

2. **`Assets/_Sandbox/camera-rig.scene`**
   A bare scene with:
   - The `CameraRig.prefab` instance (dragged in, no engine).
   - A 60×60 reference grid on the XZ plane at Y = 0:
     - 60 lines along X (one per integer, from Z = 0 to Z = 60).
     - 60 lines along Z (one per integer, from X = 0 to X = 60).
     - Implementation: a single `LineRenderer`-or-Gizmos `MonoBehaviour` named `ReferenceGrid` is acceptable. Choose whichever is simpler. Tint: a muted grey (e.g. `(0.3, 0.3, 0.3)`).
     - Origin marker (a `(2,2)` red quad at `(0, 0.01, 0)`) so Talon can locate the world origin instantly.
     - Centre marker (a `(2,2)` cyan quad at `(30, 0.01, 20)`) so Talon can confirm the prefab's `_startFocusPoint` lines up with where it should.
     - Axis labels via `TextMesh` or `TextMeshPro`: "X = +" at `(50, 0.5, 20)`, "Z = +" at `(30, 0.5, 50)`. These tell Talon at a glance which way is which.
   - One `Directional Light` matching `SceneBootstrapper.SetupDirectionalLight` (warm tint, intensity 1.1, rotation Euler `(50, -30, 0)`) so the grid is well-lit.

3. **`Assets/_Sandbox/camera-rig.md`**
   The 5-minute test recipe per Rule 4:

   ```markdown
   # Camera Rig Sandbox

   ## What this validates
   The CameraRig.prefab handles pan, rotate, zoom, recenter, and stays in
   bounds on a 60×60 reference grid that mirrors the office-starter world.

   ## Setup (one-time, after pulling this packet)
   None — the scene already has CameraRig.prefab placed.

   ## Test (every run)
   1. Open Assets/_Sandbox/camera-rig.scene.
   2. Press Play.
   3. Pan with WASD: camera should glide across the grid at ~20 units/sec.
   4. Rotate with Q / E: camera should orbit around the focus point smoothly,
      lazy-susan style. No nausea-inducing snap.
   5. Zoom with the scroll wheel: altitude should clamp at 5 (close) and 50
      (far). At 5 you should see ~6 grid cells; at 50 you should see roughly
      the whole 60×60 grid.
   6. Middle-click drag: camera pans grab-style (drag right → camera moves
      left across the grid — opposite of click direction, like dragging a map).
   7. Right-click drag: rotates yaw.
   8. Press F: camera snaps back to centre marker (cyan quad at (30, 0, 20)).
   9. Double-click anywhere: same recenter as F.

   ## Expected
   - Camera always above floor (Y > 5, never < 5).
   - Origin marker (red) and centre marker (cyan) both clearly visible at
     mid-altitude.
   - Pan, rotate, zoom feel responsive — no perceivable lag.
   - No clipping into the floor at minimum altitude.
   - 60+ FPS in editor; this is sandbox, not the engine, so it should fly.

   ## If it fails
   - Camera invisible / black screen → Camera component on the prefab's
     child is missing or disabled. Check the prefab inspector.
   - Pan or rotate doesn't respond → CameraController script reference on
     the prefab is broken (missing script). Re-link in the inspector.
   - Zoom limits feel wrong → Tune `_minAltitude` / `_maxAltitude` directly
     in the prefab inspector. Changes are persistent.
   - Camera starts looking at empty space → `_startFocusPoint` doesn't match
     the centre marker. Sync them in the prefab inspector.
   ```

4. **`Assets/_Sandbox/_Sandbox.asmdef`** (if not already present)
   Assembly definition scoping the `_Sandbox/` folder. References the same dependencies as `ECSUnity.asmdef` minus engine references. Excluded from RETAIL builds via scripting-define check. *Confirm this asmdef does not break the existing ECSUnity build before merging.*

---

## Inspector contracts (Rule 5)

Document these exactly in the packet completion notes for the next sandbox packet author to reference:

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `_engineHost` | `EngineHost` | `null` | — | Optional. Wired by integration packet. |
| `_wallFadeController` | `WallFadeController` | `null` | — | Optional. Wired by integration packet. |
| `_panSpeed` | `float` | `20f` | `[0.1, 100]` | World-units / sec |
| `_rotateSpeed` | `float` | `90f` | `[0, 360]` | Degrees / sec |
| `_zoomSpeed` | `float` | `5f` | `[0.1, 50]` | World-units / scroll step |
| `_minAltitude` | `float` | `5f` | `[1, 100]` | World-units |
| `_maxAltitude` | `float` | `50f` | `[1, 100]` | World-units, > `_minAltitude` |
| `_pitchAngle` | `float` | `50f` | `[0, 90]` | Degrees from horizontal |
| `_startFocusPoint` | `Vector3` | `(30, 0, 20)` | — | World-space, Y should be 0 |
| `_startYaw` | `float` | `0f` | `[0, 360]` | Degrees |

Add `[Range(...)]` annotations to `CameraController.cs` matching the table above. **This is the only modification permitted to `CameraController.cs` in this packet.** Tooltips already exist; ranges are the addition.

---

## Out of scope; queued for follow-up packets

These are intentionally not done here. Author them as separate packets if they prove necessary:

- **WP-3.1.S.0-INT** — wire `CameraRig.prefab` into `MainScene.unity`, retire `SceneBootstrapper.SetupCamera`. Talon authorises after the sandbox feels right.
- **WP-3.1.S.x** — gamepad input bindings. The current `CameraInputBindings` is keyboard + mouse only. UX bible §2.1 commits to gamepad eventually; not now.
- **WP-3.1.S.x** — selection-driven recenter. Right now F snaps to a hard-coded `(30, 0, 20)`. After WP-3.1.S.{selection-outline} ships, F snaps to the selected entity. Cross-packet dependency.

---

## Acceptance criteria

1. `Assets/Prefabs/CameraRig.prefab` exists and references the existing `CameraController.cs`.
2. `Assets/_Sandbox/camera-rig.scene` opens cleanly in Unity Editor without missing-script warnings.
3. The 5-minute test recipe in `Assets/_Sandbox/camera-rig.md` walks Talon through verification successfully.
4. `[Range(...)]` annotations on `CameraController.cs` match the Inspector contracts table.
5. **No file outside `Assets/Prefabs/`, `Assets/_Sandbox/`, and `ECSUnity/Assets/Scripts/Camera/CameraController.cs` is modified.** Verify with `git diff --name-only`.
6. Existing `ECSUnity` Editor and Play modes still work — `MainScene.unity` is unchanged.
7. Existing test suite passes unchanged.

---

## Completion-note template (for the Sonnet to fill in)

```markdown
# WP-3.1.S.0 — Completion Note

## Files changed
- Assets/Prefabs/CameraRig.prefab (new)
- Assets/_Sandbox/camera-rig.scene (new)
- Assets/_Sandbox/camera-rig.md (new)
- Assets/_Sandbox/_Sandbox.asmdef (new, if needed)
- ECSUnity/Assets/Scripts/Camera/CameraController.cs ([Range] annotations only)

## Inspector contract changes
<copy the table from §"Inspector contracts" above; note any deviations>

## Test recipe verified manually
<Sonnet does not run this — Talon does, post-merge>

## Anomalies / things Talon should know
<any surprises during extraction; e.g., the prefab system rejected the X reference, etc.>

## Follow-up packet candidates
- WP-3.1.S.0-INT (wire prefab into MainScene)
- <any others discovered during this packet>
```

---

*WP-3.1.S.0 establishes the sandbox track. If it ships clean in 90 minutes, the protocol works. If it doesn't, revise the protocol before drafting WP-3.1.S.1.*
