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
- Grid not visible → ReferenceGrid script may show as missing (broken GUID).
  Delete the ReferenceGrid GameObject in the Hierarchy and drag
  Assets/_Sandbox/ReferenceGrid.cs onto a new empty GameObject.

## Integration verification (after WP-3.1.S.0-INT)

This step confirms the camera rig works in the live MainScene with the
real engine.

1. Open Assets/Scenes/MainScene.unity.
2. Press Play. Engine ticks; rooms render; NPCs render as dots.
3. Pan, rotate, zoom — same controls as the sandbox.
4. Confirm the camera framing matches the sandbox feel — same altitudes,
   same pan speed, same start focus point.
5. Toggle wall fade by moving the camera close to a wall. Confirm walls
   fade as expected (this proves _wallFadeController is wired).
6. Confirm 60+ FPS with 30 NPCs (existing perf gate).

If integration verification fails:
- Camera doesn't move → CameraController._engineHost reference broken.
  Re-wire in the Inspector.
- Walls don't fade → CameraController._wallFadeController reference
  broken. Re-wire.
- Two cameras visible / "Two AudioListeners" warning → the old
  Main Camera GameObject wasn't deleted. Delete it.
