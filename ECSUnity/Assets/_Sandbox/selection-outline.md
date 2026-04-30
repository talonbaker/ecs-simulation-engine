# Selection Outline Sandbox

## What this validates
Click-to-select primitive: Selectable.cs + OutlineRenderer.cs + Outline.shader
working together to highlight a clicked GameObject and clear the highlight
when something else (or nothing) is clicked.

## Setup (one-time, after pulling this packet)
None — the scene already has the cube and sphere placed.

## Test (every run)
1. Open Assets/_Sandbox/selection-outline.unity.
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
