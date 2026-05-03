# floor-room-identity sandbox — test recipe

**Scene:** `Assets/_Sandbox/floor-room-identity.unity`
**Time:** ~10-15 minutes
**WP:** 4.0.D — Floor & Room Visual Identity

---

## Setup

1. Open `floor-room-identity.unity` in the Unity Editor.
2. If the scene is empty, run **ECS → _Sandbox → Build Floor-Room-Identity Scene** from the menu bar to populate it.
3. If textures appear pink/missing, run **ECS → Generate Room Visual Identity Assets** first, then re-open the scene.
4. Press **Play**.

---

## Zone layout (top-down view at 15m altitude)

```
[ Zone 1: Carpet ]  [ Zone 2: Linoleum ]  [ Zone 3: OfficeTile ]
[ Zone 4: Concrete ] [ Zone 5: Hardwood ] [ Zones 6-8: reserved ]
```

Walls separate zones: alternating CubicleWall (short, fabric) and StructuralWall (full-height, painted).
Doors between zones 1↔2, 2↔3, 3↔4: alternating RegularDoor and RestroomDoor.
Trim lines appear at each floor-type boundary.

---

## Step 1 — Floor identity at 15m (default)

**Expected:** Each zone reads as a distinct surface type at a glance.

| Zone | Material | Read |
|:-----|:---------|:-----|
| 1 | Carpet | Muted blue-gray; weave hint visible |
| 2 | Linoleum | Pale yellow-green; tile-line grid |
| 3 | OfficeTile | Off-white; subtle speckle |
| 4 | Concrete | Plain gray; aggregate texture |
| 5 | Hardwood | Warm brown; grain lines |

Click **15m** altitude button (or default camera height).
Verify: you can name each material without reading the zone label.

---

## Step 2 — Altitude variation

Click each altitude button and confirm materials remain legible:

| Altitude | Pass condition |
|:---------|:---------------|
| 8m | Materials should be MORE legible — texture detail readable |
| 15m | Default. All 5 types distinct. |
| 25m | Still distinct; detail may compress but color/tone separation holds |
| 40m | Color/tone separation must hold; fine texture detail may be lost |

**Fail:** Any two floor zones look identical at 15m altitude.

---

## Step 3 — Wall identity

Pan to the zone boundaries and look at the separating walls.

- **CubicleWall** (between zones 1-2 and 3-4): shorter than full height; muted blue-gray fabric appearance; you can see "above" the wall.
- **StructuralWall** (between zones 2-3 and 4-5): full height; off-white painted concrete appearance.

**Expected:** CubicleWall and StructuralWall are visually distinct at 15m.

---

## Step 4 — Door identity

Look at the doors between zones.

- **RegularDoor** (zones 1↔2 and 3↔4): warm wood look; visible handle area on one side.
- **RestroomDoor** (zones 2↔3): same frame as RegularDoor but with a restroom-symbol decal visible at 15m.

**Expected:** You can distinguish RegularDoor from RestroomDoor at 15m altitude.
**Fail:** Restroom symbol is not readable at 15m.

---

## Step 5 — Trim tiles

Look at the floor seams between zones.

- Between all adjacent zones with DIFFERENT materials: a thin darker trim line should be visible.
- Between hypothetical same-material zones: NO trim line.

**Expected:** Trim appears exactly where floor types change, nowhere else.

---

## Step 6 — Lighting interaction (Toggle lighting)

Press **Toggle Lighting** button:
- Flip between: directional light ON vs OFF.
- With ambient room tint applied: materials should shift in tone (warm tint vs cool) but remain distinguishable.

**Expected:** All 5 floor types remain distinguishable under both lighting conditions.
**Fail:** A material disappears or becomes indistinguishable when lighting changes.

---

## Step 7 — Pixel-art shader A/B

Press **Toggle pixel-art** button:
- With shader ON: scene renders through the pixel-art down-sampling pass.
- With shader OFF: raw URP rendering.

**Expected:**
- Shader ON: materials read correctly under pixel-art rendering; no color blowout.
- Shader OFF: same materials, higher-resolution appearance.

---

## Step 8 — Wall fade check

Move the camera so it looks through a wall into a zone (camera slightly above a CubicleWall or StructuralWall boundary).

**Expected:** The wall facing the camera fades to semi-transparent (alpha ~0.25), revealing the zone behind it.
**Fail:** New wall materials don't fade.

---

## Pass criteria (all must hold)

- [ ] All 5 floor types visually distinct at 15m altitude
- [ ] CubicleWall and StructuralWall visually distinct
- [ ] RegularDoor and RestroomDoor visually distinct; restroom symbol readable at 15m
- [ ] Trim appears at material transitions, absent at same-material boundaries
- [ ] Materials hold under lighting toggle
- [ ] Materials hold under pixel-art shader toggle
- [ ] Wall fade works for new wall materials

---

## Known limitations (v0.2)

- Textures are programmatic placeholders; final hand-drawn art arrives in WP-4.1.2.
- WindowWall material present but no window-opening cut through the wall geometry (future polish).
- Door materials on floor/wall geometry only; actual door swing is functional but uses existing door prefab visuals.
- Floor UV tiling is uniform (not world-space); very large rooms may look stretched.
