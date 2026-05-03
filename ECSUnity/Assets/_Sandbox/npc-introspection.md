# NPC Introspection Overlay Sandbox

## What this validates

The WARDEN-only F2 overlay: floating per-NPC text panels showing name, life state,
current activity, intended next action, top-3 drives, stress/willpower, and a
one-line "why" explanation. Three modes (Off / Selected / All) toggled by F2 or the
`introspect` dev console verb.

## Setup (one-time)

1. Open **`Assets/_Sandbox/npc-introspection.unity`**.
2. The scene contains one **OverlaySystem** GameObject. If its `_host` and
   `_selection` Inspector slots are empty, drag in the scene's **EngineHost** and
   **SelectionController** GameObjects.  
   *(SceneBootstrapper auto-creates EngineHost if none exists — check the hierarchy
   after first Play if you don't see it before.)*
3. Save the scene.

## Test (every run)

1. Press **Play**.
2. Wait ~1 second for the engine to boot and NPCs to spawn.
3. Press **F2** once.  
   **Expected:** mode pill appears top-right: `INTROSPECT: Selected  [F2]`.  
   No overlays visible yet (nothing selected).
4. Click any NPC silhouette.  
   **Expected:** one overlay panel appears above that NPC showing 5 lines:  
   - Glance: `<Name>  [Alive]  <activity>`  
   - Next action line  
   - Top-3 drives (e.g. `hunger 0.42 | fatigue 0.31 | ...`)  
   - Stress / willpower (e.g. `stress 18  willpower 82/100`)  
   - "Why" line (e.g. `working (scheduled)` or `—`)
5. Press **F2** again.  
   **Expected:** mode pill updates to `INTROSPECT: All  [F2]`. All alive NPCs now
   have overlays. Overlays that would overlap are nudged apart vertically.
6. Press **F2** once more.  
   **Expected:** all overlays disappear; mode pill disappears.
7. *(Console step — sandbox only)* The sandbox scene has no DevConsolePanel, so
   backtick/tilde does nothing here. The `introspect` command is covered by
   `IntrospectCommandTests.cs` unit tests and will be manually verified in PlaytestScene
   via the WP-4.0.F-INT integration packet.
9. Press Play to stop.

## Expected

- F2 cycles Off → Selected → All → Off with no console errors.
- `introspect on/off/selected` works from the dev console.
- Overlay panels track NPCs as they move (positions update each frame).
- Text content refreshes ~4 times per second (slight lag is normal).
- Deceased NPCs (if any) do NOT get overlay panels.
- Top-right pill shows current mode when overlay is non-Off.
- With 5+ NPCs in All mode, stacked overlays separate visually rather than
  piling on top of each other.

## Tuning (Talon)

Adjust on the **NpcIntrospectionOverlay** Inspector:
- `_worldYOffset` (default 1.8) — raise/lower the anchor above each NPC.
- `_minPixelSpacing` (default 90) — gap between stacked panels in All mode.
- `_updateHz` (default 4) — refresh rate; raise if values look stale.

## If it fails

- **No overlays appear after F2** → EngineHost not wired in Inspector; check that
  `_host` slot is populated (or let SceneBootstrapper auto-wire it on Play).
- **Overlay pinned to screen corner, doesn't track NPCs** → `Camera.main` not set;
  ensure the Main Camera tag is on the camera in the scene.
- **`introspect` command not found in console** → this sandbox has no DevConsolePanel;
  console testing is deferred to WP-4.0.F-INT (PlaytestScene wiring).
- **All text shows `—`** → NPCs may not have SocialDrivesComponent or
  IntendedActionComponent yet (early boot ticks); wait 2–3 seconds and press F2 again.
- **FPS drops below 60 in All mode** → `_updateHz` too high or 30+ NPCs; lower
  `_updateHz` to 2 or reduce NPC count.
