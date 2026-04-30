# WP-3.1.S.2 — Draggable Prop with Snap-to-Grid Sandbox

> **Protocol:** Track 2 (visual sandbox) under `docs/UNITY-PACKET-PROTOCOL.md`.
> **Position in sub-phase:** Third sandbox packet. Establishes the build-mode interaction primitive (grab, move, snap, drop, parent) on isolated geometry.

**Tier:** Sonnet
**Depends on:** WP-3.1.S.0 (camera rig prefab — used in the sandbox scene). No dependency on WP-3.1.S.1 (selection); the two are orthogonal interaction modes.
**Parallel-safe with:** All Track 1 engine packets. All other Track 2 sandbox packets.
**Timebox:** 150 minutes
**Budget:** $0.60

---

## Goal

Establish the **drag-to-place** primitive in Unity as a fully sandboxed feature: a `DraggableProp.cs` MonoBehaviour, a `Table.prefab` and `Banana.prefab`, and a sandbox scene with a 10×10 reference grid where Talon can pick up a table, move it, snap it to integer tile positions, drop it, and then drop a banana onto the table (which parents the banana to the table at a configurable socket).

After this packet, the build-mode interaction primitive exists, tuned, and ready for any future packet that wants to drag-place anything (furniture, hazards, decorations, NPCs in dev console). Future packet `WP-3.1.S.2-INT` will wire this into the existing build-mode glue (`Assets/Scripts/BuildMode/`) and call into `IWorldMutationApi` on drop. That integration is out of scope here.

This packet ships:

1. `Assets/Scripts/BuildMode/DraggableProp.cs` — MonoBehaviour. Holds the prop's grid-snap configuration, parent-on-drop socket configuration, and the grab/drop state machine.
2. `Assets/Scripts/BuildMode/DragHandler.cs` — singleton-ish manager. Owns the "currently dragged" reference, performs the screen-to-world raycast, and applies snap. Mirrors `SelectionManager.cs` from S.1 in shape (one file, simple state).
3. `Assets/Scripts/BuildMode/PropSocket.cs` — optional component. A child transform on a prop that declares "things can attach here." A table has one or more `PropSocket` children; bananas snap to the nearest socket on drop.
4. `Assets/Prefabs/Table.prefab` — a wrapper prefab. A 1×1×0.6 cuboid (table-sized in world-units) with `DraggableProp.cs` and one `PropSocket` child at `(0, 0.7, 0)` (top centre).
5. `Assets/Prefabs/Banana.prefab` — a wrapper prefab. A scaled 0.3×0.1×0.1 capsule (banana-sized) with `DraggableProp.cs` and a `_targetSocketTag = "Surface"` value so it preferentially snaps to surface-tagged sockets.
6. `Assets/_Sandbox/draggable-prop.scene` — sandbox scene per the protocol.
7. `Assets/_Sandbox/draggable-prop.md` — 5-minute test recipe.

---

## Reference files (read in this order)

1. `docs/UNITY-PACKET-PROTOCOL.md` — load-bearing protocol.
2. `docs/PHASE-3-1-S-PLAN.md` — context on where this packet sits.
3. `docs/c2-content/ux-ui-bible.md` §5 (build mode) — Off-hours = player's build window. Drag, snap, drop. No undo at v0.1.
4. `docs/c2-content/world-bible.md` — names what props live in the office (desks, chairs, microwaves, fridges, etc.). The "table" in this sandbox is a generic stand-in; future content packets layer specific prop catalog.
5. `ECSUnity/Assets/Scripts/BuildMode/` — read whatever's there. The existing build-mode glue from WP-3.1.D ships some pattern; we are *not* refactoring it here, but the new `DragHandler` should not collide with existing class names.
6. `ECSUnity/Assets/Scripts/Camera/CameraController.cs` — the sandbox uses the same camera rig. The drag-handler's screen-to-world raycast hits a floor-plane collider (added in the sandbox scene) and projects the dragged prop's position onto the grid.

---

## Non-goals

- Do **not** call `IWorldMutationApi`. Integration is `WP-3.1.S.2-INT`, out of scope.
- Do **not** modify `MainScene.unity` or any scene outside `Assets/_Sandbox/`.
- Do **not** modify any file under `Assets/Scripts/BuildMode/` *that already exists*. New files only. (If naming collisions force a rename, escalate to Talon — don't silently rename.)
- Do **not** modify any file under `Assets/Scripts/Engine/`, `Assets/Scripts/Render/`, or anything outside `Assets/Scripts/BuildMode/` and `Assets/_Sandbox/` and `Assets/Prefabs/`.
- Do **not** add multi-select, drag-rectangle, copy-paste, or rotation. Just grab-move-snap-drop. Rotation is a follow-up packet.
- Do **not** add collision-with-walls or collision-with-other-props validation. Drop wherever. Collision validation lives in the integration packet (or a future packet) once the engine's `IWorldMutationApi` rejects invalid placements.
- Do **not** add visual feedback for "this spot is invalid" — there's no concept of invalid in v0.1.
- Do **not** add a build-mode-toggle UI. The sandbox's build mode is "always on." The integration packet adds the toggle.
- Do **not** add inventory, palette, or "spawn from menu" — the sandbox's props are pre-placed; you only move what's already there.
- Do **not** ship per-archetype prop variants.
- Do **not** add a Cinemachine, URP, HDRP, or DOTween dependency.
- Do **not** reflection-bootstrap the build mode from a `RuntimeInitializeOnLoadMethod`.

---

## Design notes

### State machine

`DraggableProp` holds one of:
- `Idle` — at rest, can be grabbed.
- `Dragging` — currently held by the cursor; transform follows cursor projected onto the floor plane, snapped to the grid each frame.
- `Settled` — just dropped; transitions back to `Idle` next tick.

`DragHandler` polls `Input.GetMouseButtonDown(0)` on a `DraggableProp` (raycast hit) → `Idle → Dragging`. Holds the reference. Polls `Input.GetMouseButtonUp(0)` → `Dragging → Settled → Idle`. On drop, the handler checks: did the drop position overlap a parent prop with an open `PropSocket` matching the drop's `_targetSocketTag`? If yes, parent the dropped prop to the socket and snap to its local position.

### Snap-to-grid

Snap unit is `_snapTileSize` on `DraggableProp` (default `1.0f`, matching the engine's tile size). `DragHandler` projects the cursor ray onto the `y = 0` plane, then rounds the X/Z components to the nearest multiple of `_snapTileSize`. The dragged prop's `transform.position = (snappedX, originalY, snappedZ)` while dragging; on drop, `transform.position` is finalised at the snapped coords.

While dragging, the prop's Y is preserved from its idle state — props don't fall through the floor mid-drag, and a banana being dragged retains its socket-relative height until the next drop. (Once dropped onto a surface socket, Y is overridden by the socket's local Y.)

### Parent-on-drop socket

`PropSocket` is a child transform on a parent prop with:
- `Tag` (string) — e.g., `"Surface"` for a tabletop, `"Wall"` for a wall mount.
- `IsOccupied` (bool, set by the handler when something snaps in).
- `OccupyingProp` (reference, set when occupied).

A banana (`_targetSocketTag = "Surface"`) dropped overlapping a table's `Surface` socket → parents to that socket, sets `IsOccupied = true`, snaps `localPosition` to `(0, 0, 0)` and `localRotation` to identity. If the banana is later picked up again, `IsOccupied = false` and the parent reference is cleared.

If a banana is dropped *not* overlapping any matching socket, it stays at the drop position on the floor (parent = scene root or whatever the original parent was). This is the v0.1 behaviour; future packets can add "must drop on a socket" enforcement.

### Inspector contracts

`DraggableProp.cs`:

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `_snapTileSize` | `float` | `1.0f` | `[0.1, 5.0]` | World-units per snap step |
| `_targetSocketTag` | `string` | `""` (empty) | — | If non-empty, attempt to parent onto a matching socket on drop |
| `_grabHeight` | `float` | `0.1f` | `[0, 2]` | World-units lift-off-floor while dragging (visual feedback) |

`DragHandler.cs`:

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `_camera` | `Camera` | `null` (auto-grabs `Camera.main`) | — | Drag ray source |
| `_floorPlaneY` | `float` | `0f` | — | Y of the projection plane |
| `_dragLayerMask` | `LayerMask` | everything | — | Which layers can be grabbed |

`PropSocket.cs`:

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `_tag` | `string` | `"Surface"` | — | Socket type identifier |
| `_snapRadius` | `float` | `0.5f` | `[0.05, 5]` | World-units within which a drop counts as "on" the socket |

Add `[Tooltip(...)]` and `[Range(...)]` annotations to all fields.

---

## Sandbox scene composition (`Assets/_Sandbox/draggable-prop.scene`)

The scene contains:

- One instance of `Assets/Prefabs/CameraRig.prefab` from WP-3.1.S.0.
- One `Directional Light` matching the established sandbox lighting.
- A 10×10 reference grid centred at `(5, 0.01, 5)` (so the grid spans `(0,0)` to `(10,10)`). Same `LineRenderer` or Gizmos approach as in S.0; adjust extent.
- A floor plane at `y = 0` with a `MeshCollider` (size `(10, 0, 10)`, position `(5, 0, 5)`) — gives the drag-handler something to raycast against. Material can be a translucent grey or just left invisible.
- One instance of `Assets/Prefabs/Table.prefab` placed at `(3, 0.3, 3)` (table top at `y = 0.6`).
- One instance of `Assets/Prefabs/Banana.prefab` placed at `(7, 0.05, 7)` (lying on the floor).
- A `DragHandler` GameObject at `(0, 0, 0)` with the manager singleton.

The scene is composed for one purpose: pick up table, move it, snap it. Pick up banana, move it onto the table, watch it parent. Nothing else.

---

## Test recipe (`Assets/_Sandbox/draggable-prop.md`)

Verbatim:

```markdown
# Draggable Prop Sandbox

## What this validates
Drag-to-place primitive: DraggableProp.cs + DragHandler.cs + PropSocket.cs
working together to grab a prop, move it, snap to integer tile positions,
drop it, and parent smaller props onto larger props' sockets.

## Setup (one-time)
None — the scene already has a table at (3, 0.3, 3) and a banana at (7, 0.05, 7).

## Test (every run)
1. Open Assets/_Sandbox/draggable-prop.scene.
2. Press Play.
3. Move the camera to a 30° down-and-back view of the grid.
4. Click and hold on the table. Move the cursor across the grid. Expect:
   table follows cursor, snapping to integer tile positions. The table's
   bottom should appear lifted slightly off the floor (grab-height feedback).
5. Release the mouse. Expect: table snaps to the nearest integer tile and
   stays there.
6. Click and hold on the banana. Move the cursor over the table you just
   placed. Expect: banana follows cursor, snapping similarly.
7. Release the mouse over the table. Expect: banana parents to the
   table — when you next move the table, the banana moves with it.
8. Pick up the table again. Move it. Confirm banana follows.
9. Pick up the banana off the table. Drop it on empty floor. Confirm
   banana detaches from table (table can move without banana).

## Tuning pass (Talon)
Open Table.prefab and Banana.prefab in the Inspector. Adjust:
- _snapTileSize on each — try 0.5 (sub-tile) or 2.0 (larger snap) for feel.
- _grabHeight — the lift while dragging. Make it readable but not goofy.
- PropSocket._snapRadius on the table — make the "drop into socket" forgiving
  but not silly.

Save the prefabs. Commit the tuned values.

## Expected
- Drag is responsive; no perceivable lag.
- Snap feels confident — the prop "clicks" into place.
- Banana attaches to table reliably when dropped near the table top.
- Banana doesn't attach to floor (no Surface socket on the floor).
- 60+ FPS.

## If it fails
- Drag does nothing → DragHandler not in scene, or _camera is null and
  Camera.main isn't tagged correctly, or _dragLayerMask excludes the prop.
- Prop teleports far from cursor → floor plane y or projection logic wrong.
- No snap → _snapTileSize is 0 or the snap math has a bug.
- Banana doesn't parent → _targetSocketTag on banana doesn't match the
  socket's _tag, or the snap radius is too small.
- Banana parents to floor → there's a stray PropSocket somewhere it
  shouldn't be.
- Picking up a parented banana doesn't detach → IsOccupied not cleared.
```

---

## Acceptance criteria

1. `Assets/Scripts/BuildMode/DraggableProp.cs`, `DragHandler.cs`, `PropSocket.cs` exist with the public APIs implied by the design notes.
2. `Assets/Prefabs/Table.prefab` and `Assets/Prefabs/Banana.prefab` instantiate cleanly.
3. `Assets/_Sandbox/draggable-prop.scene` opens cleanly with no missing-script warnings.
4. `Assets/_Sandbox/draggable-prop.md` is verbatim the recipe in **Test recipe**.
5. The Inspector contract tables are reflected in `[Tooltip]` + `[Range]` annotations.
6. **No file outside the paths listed in §"This packet ships" is modified**, and **no existing file under `Assets/Scripts/BuildMode/` is modified.** Verify with `git diff --name-only`.
7. `MainScene.unity` is unchanged.
8. `dotnet test` passes; `dotnet build` is clean.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

This is a Track 2 (Unity) packet. xUnit tests are necessary but **not sufficient** — the visual layer must be verified by Talon in Unity Editor before PR is mergeable.

The Sonnet executor's pipeline:

1. Implement the spec — write the three scripts, build the two prefabs, compose the sandbox scene, write the recipe.
2. Run `dotnet test` and `dotnet build`. Must be green.
3. Stage all changes including the self-cleanup deletion (see below).
4. Commit on the worktree's feature branch.
5. Push the branch.
6. Stop. Do **not** open a PR yet. Do **not** merge.
7. The final line of the commit message must be: `READY FOR VISUAL VERIFICATION — run Assets/_Sandbox/draggable-prop.md`.

Talon's pipeline (after Sonnet's push):

1. Open the Unity Editor on the feature branch.
2. Run the test recipe at `Assets/_Sandbox/draggable-prop.md`.
3. If the recipe passes: open the PR, merge to `staging`.
4. If the recipe fails: file the failure as PR review comments or as a follow-up packet. Do not ask the original Sonnet to iterate ad-hoc.

### Cost envelope (1-5-25 Claude army)

Target: **$0.60** (150-minute timebox). If costs approach $1.00 without the recipe being achievable, **escalate to Talon** with a `WP-3.1.S.2-blocker.md` note.

Unity-specific cost-discipline:
- Don't author the `Table.prefab` or `Banana.prefab` by elaborate art — primitive cube + scaled capsule is fine. The prefabs exist to test interaction, not aesthetics.
- The drag math is screen-to-world ray + plane intersection + integer snap. Don't over-engineer.
- Parenting in Unity is `transform.SetParent()`. Don't reach for fancy attachment systems.

### Self-cleanup on merge

Before opening the PR (after Talon's visual verification passes), the executing Sonnet must:

1. **Check downstream dependents:**
   ```bash
   git grep -l "WP-3.1.S.2" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```

2. **Expected result:** `WP-3.1.S.2-INT-draggable-into-build-mode.md` exists as a downstream dependent. **Leave the spec file in place** and add the status header:
   ```markdown
   > **STATUS:** SHIPPED to staging YYYY-MM-DD. Retained because pending packet `WP-3.1.S.2-INT` depends on this spec.
   ```
   Add `Self-cleanup: spec retained, dependents: WP-3.1.S.2-INT.` to the commit message.

3. The scripts and prefabs live in their final paths and are not deleted.

4. **Do not touch** files under `_completed/` or `_completed-specs/`.

---

*WP-3.1.S.2 establishes the drag-to-place language. The integration packet `-INT` later wires this into the existing build-mode glue + `IWorldMutationApi` so dropping a prop in MainScene actually changes the engine state.*
