# Particle Vocabulary Sandbox — Test Recipe

**Scene:** `ECSUnity/Assets/_Sandbox/particle-vocabulary.unity`
**Time:** 10–15 minutes
**Prerequisite:** Unity Editor open with URP + VFX Graph package, scene loaded.

---

## Pre-flight

1. Open `Assets/_Sandbox/particle-vocabulary.unity`.
2. In the Inspector, verify `ParticleTriggerSpawner` on the EngineHost GameObject has:
   - `EngineHost` field wired to the scene's EngineHost.
   - `Catalog` field wired to `Assets/Settings/DefaultParticleTriggerCatalog.asset`.
3. Verify `DefaultParticleTriggerCatalog.asset` has all 10 entries with their `VfxAsset` fields pointing to `Assets/VFX/*.vfx`. If any are unassigned, drag the corresponding `.vfx` file in now.
4. Verify each `.vfx` asset has been opened in the VFX Graph editor and its graph is wired (Initialize → Update → Output, with `Intensity` exposed property connected to spawn rate). If not wired yet, open each and build the minimal graph per the design notes in the file header.

---

## Step 1 — Per-effect individual test (10 effects × ~30s each = 5 min)

For each effect listed below:

1. Click its button in the Sandbox UI panel.
2. Observe the effect at the center of the room (default altitude, pixel-art mode ON).
3. Confirm it reads as the stated phenomenon (see "Expected read" column).
4. Toggle "Toggle pixel-art" — confirm the effect still reads at down-sample resolution.

| # | Button | Expected read |
|:--|:-------|:--------------|
| 0 | SteamFromCoffee | Gentle white wisps rising slowly upward. Reads as steam. |
| 1 | SteamFromFood | Slightly broader white plume rising upward. |
| 2 | SmokeFromFire | Thick dark gray/black rising column, faster than steam. |
| 3 | Sparks | Brief orange/yellow radial burst, particles arc with gravity. |
| 4 | DustKickedUp | Low horizontal tan cloud, fades quickly. |
| 5 | WaterSplash | Blue/white radial burst, particles arc and fall fast. |
| 6 | BulbFlicker | Tiny warm-white pulse near a light source position. Brief. |
| 7 | CleaningMist | Light blue/white gentle mist rising and spreading. |
| 8 | BreathPuff | Small white forward cone puff from NPC mouth height. |
| 9 | SpeechBubblePuff | Tiny white upward puff above NPC head. Very brief. |

---

## Step 2 — Camera-distance attenuation (2 min)

1. Use the altitude buttons to move the camera to 30m altitude (or pan far from the center).
2. Fire Sparks at the center.
3. Confirm the effect fades / doesn't render at or beyond ~30m (camera culling distance).

---

## Step 3 — Fire All stress test (2 min)

1. Click **Fire All** (fires all 10 effects simultaneously).
2. Observe FPS counter — should remain ≥ 60 FPS.
3. Confirm no visual artifacts or z-fighting.

---

## Step 4 — Loop sustained test (2 min)

1. Select Sparks, enable **Loop selected**.
2. Observe 30 seconds of continuous re-fire.
3. Confirm FPS stays stable, no particle count explosion, no Unity console errors.
4. Disable loop.

---

## Pass criteria

- All 10 effects read as the intended phenomenon at 15m altitude. ✓
- Effects read at pixel-art down-sample resolution (no disappearance or unreadable noise). ✓
- Fire All holds ≥ 60 FPS. ✓
- Sustained loop is stable for 30 seconds. ✓
- Camera culling fades effects beyond ~30m. ✓

---

## Known gaps (to be addressed in WP-4.1.2 or later)

- VFX Graph assets are placeholder quality. Final art pass in WP-4.1.2.
- Y-position for effects is always floor-level (Y=0). BulbFlicker should spawn at ceiling height — wire this in WP-4.0.H-INT when the spawner has room-height data.
- `particle-vocabulary.unity` scene needs to be created in the Unity Editor and configured per the scene spec in WP-4.0.H.
