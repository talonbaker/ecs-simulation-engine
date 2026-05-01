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
