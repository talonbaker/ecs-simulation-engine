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

## Integration verification (after WP-3.1.S.3-INT)

This step confirms the popup binds to live NPC state.

1. Open Assets/Scenes/MainScene.unity.
2. Press Play. Engine ticks; NPCs render.
3. Click an NPC dot. Expect: popup appears with three sections:
   - **Surface** — NPC's name, current action (e.g. "Greg | Eat").
   - **Behaviour** — header at 50% alpha, body says "(coming soon)".
   - **Internal** — header at 50% alpha, body says "(coming soon)".
4. Wait a few seconds. As the NPC's dominant drive changes (Eat → Drink,
   etc.), the Surface tier's "Current Action" updates live in the popup.
5. Click another NPC. Popup repaints with new NPC's data.
6. Click empty floor. Popup disappears.
7. Click a sleeping NPC (if any). Expect: popup shows "Sleep" as the
   action — confirms IsSleeping branch in DeriveCurrentAction fires.

## If integration fails
- Popup appears but has no data → WorldStateInspectorBinder isn't
  finding the entity in WorldStateDto. Check entity ID matching.
- Popup shows stale data → Update() loop isn't firing or the binder
  isn't refreshing. Check the _trackedEntityId logic.
- Popup throws exception when an NPC dies → BuildDataForEntity isn't
  handling the entity-left-DTO case. Should call _popup.Hide().
- Tier 2 / Tier 3 sections show data instead of placeholder → struct
  defaults aren't empty (default keyword returns zeroed structs, which
  is correct for string fields — they'll be null, and Show() uses
  "(coming soon)" literals, not the struct fields).
