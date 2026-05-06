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

---

## Integration verification (after WP-3.1.S.2-INT)

This step confirms drag-place in MainScene mutates engine state.

---

### Part A — One-time scene setup

Do this once after pulling the WP-3.1.S.2-INT branch. You only need to redo
it if you re-create the scene from scratch.

**Step 1 — Confirm the project compiles cleanly.**
Open Unity. Wait for the spinner in the bottom-right to finish. The Console
(Window → General → Console) should show zero errors. If it shows errors,
stop here and fix them before continuing.

**Step 2 — Open MainScene.**
In the Project window (bottom panel), navigate to `Assets/Scenes/` and
double-click `MainScene.unity`. The scene loads in the Hierarchy (left panel).

**Step 3 — Create the PropToEngineBridge GameObject.**
In the Hierarchy, right-click on an empty area → Create Empty.
A new GameObject called "GameObject" appears. Rename it `PropToEngineBridge`
(press F2 or click it twice slowly).

**Step 4 — Add the PropToEngineBridge component.**
With `PropToEngineBridge` selected in the Hierarchy, look at the Inspector
(right panel). Scroll to the bottom and click **Add Component**.
Type `PropToEngineBridge` in the search box and select it.
You should now see three empty fields: `Build Mode Controller`, `Archetype Map`,
and `Drag Handler` (Unity shows field names in the Inspector with spaces added).

**Step 5 — Wire Build Mode Controller.**
In the Hierarchy, find the existing `BuildModeController` GameObject (it was
there before this packet — search the Hierarchy search bar if needed).
Drag it from the Hierarchy into the `Build Mode Controller` field on the
`PropToEngineBridge` component in the Inspector.

**Step 6 — Wire the Archetype Map.**
In the Project window, navigate to `Assets/Resources/`.
Find `PropArchetypeMap.asset`. If it doesn't exist yet:
  a. Right-click in `Assets/Resources/` → Create → Build Mode → Prop Archetype Map.
  b. Name it `PropArchetypeMap`.
  c. Select it. In the Inspector, click the `+` button under `Entries` twice to
     add two rows. Fill them in:
       Row 0: Prefab Name = `Table`, Archetype Id = `table`
       Row 1: Prefab Name = `Banana`, Archetype Id = `banana`
  d. Press Ctrl+S to save.
Drag `PropArchetypeMap.asset` from the Project window into the `Archetype Map`
field on `PropToEngineBridge`.

**Step 7 — Wire DragHandler on BuildModeController.**
Select the `BuildModeController` GameObject in the Hierarchy.
In its Inspector, find the `Drag Handler` field (added by WP-3.1.S.2-INT).
Find the `DragHandler` GameObject in the Hierarchy (it may be a child of
`BuildModeController` or a separate top-level GameObject; search the Hierarchy
bar if unsure). Drag it into the `Drag Handler` field.

**Step 8 — Save the scene.**
Press Ctrl+S. The asterisk (*) next to the scene name in the title bar should
disappear, confirming the scene is saved.

---

### Part B — Test recipe (run every time)

**Step 1 — Enter Play mode.**
Press the Play button (▶) at the top of the editor, or press Ctrl+P.
Wait for the engine to finish booting — you'll see NPC characters start moving
in the Scene/Game view. The Console may show boot log lines; that's normal.
If you see red errors at boot, stop and fix them before continuing.

**Step 2 — Open the Console so you can read log output.**
Window → General → Console. Dock it somewhere visible. Click the **Clear**
button to remove old messages so new ones are easy to spot.

**Step 3 — Enter build mode.**
Press the **B key** (or click the build-mode toggle button on the UI if one
exists). You should see some visual indicator that build mode is active —
a UI highlight, cursor change, or button state change depending on what was
already implemented. DragHandler is now active.

**Step 4 — Grab a prop.**
Click and hold the left mouse button on any prop in the scene that has a
`DraggableProp` component (the Table and Banana from the sandbox both qualify
if they're in MainScene; otherwise any prop you've wired with `DraggableProp`).
While holding:
- The prop should lift slightly off the surface (grab-height feedback).
- Moving the mouse should move the prop, snapping to the grid.
If nothing happens, see "If integration fails" below.

**Step 5 — Drop the prop.**
Release the left mouse button. The prop should snap to the nearest tile and
stay there. Immediately check the Console.

**Step 6 — Verify the Console log.**
You should see a line like:
```
[PropToEngineBridge] Drop: 'Table' (archetype='table') → tile (3, 3)
```
The exact tile coordinates will match where you dropped it. This confirms
the bridge received the drop event and called the mutation API.

If you see this log, the engine-side integration is working. The engine
recorded the structural change and invalidated the pathfinding cache.

**Step 7 — Verify NPC re-pathfinding (optional but thorough).**
If an NPC's path runs through the tile where you placed the prop, watch the
NPC on the next engine tick — it should re-route around the prop rather than
walk through it. This confirms `StructuralChangeBus` fired and the pathfinding
cache was invalidated. (This is easiest to see if you drop the prop directly
in an NPC's path.)

**Step 8 — Exit build mode.**
Press **B** again (or click the build-mode toggle). DragHandler is now inactive.
Click and hold on any prop. It should not move. This confirms build mode is
correctly gating drag input.

**Step 9 — Exit Play mode.**
Press Ctrl+P or click the Play button again. Unity returns to Edit mode.
The scene reverts to its saved state (props go back to where they were before
you pressed Play — this is normal Unity behavior).

---

### If integration fails

**Drag does nothing in Play mode (build mode on)**
- Check that `BuildModeController._dragHandler` is wired (Step 7 of setup).
  Select `BuildModeController` in the Hierarchy; `Drag Handler` field must not
  be None/Missing.
- Check the Console for a "DragHandler" or "camera is null" warning.
- Check that the prop you're clicking has a `DraggableProp` component and a
  Collider on the same or child GameObject. Without a collider, the raycast
  won't hit it.

**Prop drags but Console shows no "[PropToEngineBridge]" log**
- Check that `PropToEngineBridge` GameObject is active in the Hierarchy
  (checkbox next to its name must be ticked).
- Check that `_buildModeController` is wired on the bridge (Step 5 of setup).
- Check the Console for a NullReferenceException during drop — it may be
  crashing before it can log.

**Console logs appear but engine state doesn't change / NPCs ignore the prop**
- `MutationApi` may be null (engine not fully booted). Check the boot log for
  errors above the drop log line.
- Check the Console for an exception on the same tick as the drop log.

**Prop snaps back to its original position immediately after drop**
- The engine rejected the placement. Check the Console for a rejection log
  or exception. Most common cause: the archetype string in `PropArchetypeMap`
  doesn't match what the engine expects (case-sensitive). Try lowercase.

**Drag still works after exiting build mode**
- `BuildModeController._dragHandler` is not wired, so `Deactivate()` was never
  called. Redo Step 7 of the setup and save the scene.
