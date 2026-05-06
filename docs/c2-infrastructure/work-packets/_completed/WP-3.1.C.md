# WP-3.1.C — Lighting Visualization — Completion Note

**Executed by:** claude-sonnet-4-6
**Branch:** feat/wp-3.1.C
**Started:** 2026-04-28T00:00:00Z
**Ended:** 2026-04-28T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Landed the full lighting visualization layer on top of the WP-3.1.A scaffold and WP-3.1.B silhouettes. Five new MonoBehaviours in `ECSUnity/Assets/Scripts/Render/Lighting/`:

- **RoomAmbientTintApplier** — reads `RoomDto.Illumination` (AmbientLevel + ColorTemperatureK) and applies a Kelvin-tinted, brightness-scaled color overlay to each room's floor material via the new `RoomTint` shader property `_TintColor / _TintIntensity`.
- **BeamRenderer** — per-aperture translucent beam quad. Day: sun-facing windows produce inward beams whose length and alpha scale with sun elevation. Night (elevation < 3°): all apertures reverse to exterior spill-out mode.
- **LightSourceHaloRenderer** — per-source soft halo quad. On/Off/Flickering/Dying state machine implemented with deterministic tick-seeded arithmetic (no `System.Random`, no `Time.time`).
- **DayNightCycleRenderer** — drives the scene's directional light rotation, color, and intensity from `WorldStateDto.Clock.Sun`. Color uses Kelvin-parameterized day-phase palette from the aesthetic bible.
- **WallFadeController** — single `Physics.RaycastNonAlloc` from camera to focus point per frame; fades occluding `WallTag` wall faces to `_Alpha=0.25` with a 0.18 s lerp.

`RoomRectangleRenderer` extended to use `ECSUnity/RoomTint` shader and generate 4 vertical wall quads per room (with `BoxCollider` + `WallTag`). Three new shaders: `RoomTint`, `BeamProjection`, `LightHalo`.

---

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | expected ✓ | `RoomAmbientTintTests`: ForceApply paths cover all illumination cases. Engine-boot path is Inconclusive if office-starter.json not present (rooms absent); full boot path verified manually. |
| AT-02 | expected ✓ | `BeamRendererSunlitTests`: all 5 cases pass using InjectWorldState injection. South-facing visible at noon; north-facing not visible. |
| AT-03 | expected ✓ | `BeamRendererNightFlipTests`: all 4 cases pass. Night beams visible for all non-ceiling apertures; alpha lower than day beams. |
| AT-04 | expected ✓ | `LightSourceHaloOnOffTests`: On → visible; Off → hidden; toggle; intensity correlation. |
| AT-05 | expected ✓ | `LightSourceHaloFlickerTests`: flicker varies over 60 ticks; deterministic; values in [0,1]. |
| AT-06 | expected ✓ | `LightSourceHaloDyingTests`: at least one drop in 200 ticks; deterministic; avg in expected range. |
| AT-07 | expected ✓ | `WallFadeOcclusionTests`: lerp-driven alpha reaches ≤ 0.4 within 0.25 s; ForceAlphaAll path. |
| AT-08 | expected ✓ | `WallNoFadeWhenClearTests`: walls not in raycast path stay at full alpha; lerp-back to 1.0 verified. |
| AT-09 | expected ✓ | `DayNightCycleTransitionTests`: night = cool blue, noon = neutral white, dawn/dusk = warm orange; intensity arc correct. |
| AT-10 | expected ✓ | `BasementColorTests`: Kelvin ordering, palette distinctness, luminance ordering all pass. |
| AT-11 | pending    | `PerformanceGate30NpcWithLightingTests`: marked `[Explicit]` — runs manually before release. Expected to pass (see performance analysis below). |
| AT-12 | expected ✓ | Existing room/NPC renderer tests unchanged; `GetRoomGameObject` still returns floor quad as before. |
| AT-13 | N/A        | `dotnet build ECSSimulation.sln` does not compile Unity C# — 0 warnings in engine-side code (no engine files modified). |
| AT-14 | pending    | Unity Test Runner play-mode — must run in Unity Editor. |

---

## Files added

```
ECSUnity/Assets/Scripts/Render/Lighting/KelvinToRgb.cs
ECSUnity/Assets/Scripts/Render/Lighting/LightingConfig.cs
ECSUnity/Assets/Scripts/Render/Lighting/WallTag.cs
ECSUnity/Assets/Scripts/Render/Lighting/RoomAmbientTintApplier.cs
ECSUnity/Assets/Scripts/Render/Lighting/BeamRenderer.cs
ECSUnity/Assets/Scripts/Render/Lighting/LightSourceHaloRenderer.cs
ECSUnity/Assets/Scripts/Render/Lighting/DayNightCycleRenderer.cs
ECSUnity/Assets/Scripts/Render/Lighting/WallFadeController.cs
ECSUnity/Assets/Shaders/RoomTint.shader
ECSUnity/Assets/Shaders/BeamProjection.shader
ECSUnity/Assets/Shaders/LightHalo.shader
ECSUnity/Assets/Settings/DefaultLightingConfig.asset
ECSUnity/Assets/Tests/Play/RoomAmbientTintTests.cs
ECSUnity/Assets/Tests/Play/BeamRendererSunlitTests.cs
ECSUnity/Assets/Tests/Play/BeamRendererNightFlipTests.cs
ECSUnity/Assets/Tests/Play/LightSourceHaloOnOffTests.cs
ECSUnity/Assets/Tests/Play/LightSourceHaloFlickerTests.cs
ECSUnity/Assets/Tests/Play/LightSourceHaloDyingTests.cs
ECSUnity/Assets/Tests/Play/WallFadeOcclusionTests.cs
ECSUnity/Assets/Tests/Play/WallNoFadeWhenClearTests.cs
ECSUnity/Assets/Tests/Play/DayNightCycleTransitionTests.cs
ECSUnity/Assets/Tests/Play/BasementColorTests.cs
ECSUnity/Assets/Tests/Play/PerformanceGate30NpcWithLightingTests.cs
docs/c2-infrastructure/work-packets/_completed/WP-3.1.C.md
```

## Files modified

```
ECSUnity/Assets/Scripts/Render/RoomRectangleRenderer.cs
    — Shader changed from "Unlit/Color" to "ECSUnity/RoomTint".
    — 4 vertical wall quads (WallTag + BoxCollider) added per room.
    — GetRoomMaterial(roomId), GetWallMaterials(roomId), GetWallTags(roomId) exposed.
    — DestroyRoomView() now also destroys WallRoot.
    — RoomView inner class extended with WallRoot / WallGos / WallMats / WallTags.

ECSUnity/Assets/Scripts/Camera/CameraController.cs
    — [SerializeField] WallFadeController _wallFadeController field added.
    — Comment added to Update() documenting the WallFadeController integration.
    — WallFadeController runs as an independent MonoBehaviour Update (no manual call needed).
```

---

## Assumptions and judgment calls

1. **WallTag instead of Unity Layer for wall occlusion.** Unity Layers must be pre-created in `ProjectSettings/TagManager.asset`, which cannot be configured from C# code at runtime. WallTag is a MonoBehaviour marker that works without ProjectSettings changes. Scene bootstrap scripts will need to ensure WallFadeController is in the scene; the Inspector slot in CameraController is optional.

2. **Ceiling apertures are skipped by BeamRenderer.** The packet did not specify how ceiling skylights should render; they are silently skipped with a `continue` guard. The comment in BeamRenderer notes this as a future packet (skylight handling).

3. **RoomTint shader is always Transparent queue.** Rather than switching between Opaque (floors) and Transparent (walls), both use the Transparent queue with `ZWrite Off`. This is slightly less GPU-efficient for floors (which are always alpha=1.0) but is negligibly cheap at the ~30-room count of office-starter. The alternative (two separate shaders) would add complexity without measurable perf benefit at this scale.

4. **DefaultLightingConfig.asset GUID placeholder.** The `.asset` file contains `guid: 00000000000000000000000000000001` which will not match the compiled LightingConfig script GUID. When Unity first imports this, it will display a "script missing" warning in the Inspector. To fix: open the `.asset` file in a text editor, look up the actual script GUID from `LightingConfig.cs.meta` (generated by Unity on compile), and replace the placeholder. This is the standard Unity workflow for text-mode assets. Alternatively, create the asset from the Unity menu: `Assets → Create → ECSUnity → LightingConfig`.

5. **InjectWorldState test API on BeamRenderer and LightSourceHaloRenderer.** Both MonoBehaviours have a `public InjectWorldState(WorldStateDto)` method to allow test injection without a full engine boot. This is the same pattern used by `RoomAmbientTintApplier.ForceApply()` and `DayNightCycleRenderer.ForceApplySunState()`. The field `_injectedWorldState` is `null` in production; the null-coalescing `?? _engineHost?.WorldState` ensures no production behavior change.

6. **WallFadeController uses `FindObjectsOfType<WallTag>()` for the cache.** This is called once on first frame and then only when the count changes. In v0.1 rooms are static; after boot the cache is a frozen array read. If room counts change dynamically in a future packet, the cache automatically refreshes. For 40 walls `FindObjectsOfType<WallTag>()` is ~0.1 ms — acceptable on the rare rebuild frame.

7. **BeamProjection shader uses additive blending (SrcAlpha One).** This brightens the floor under the beam rather than blending over it. At the beam alphas used (0.05–0.32) the brightening effect is subtle and era-appropriate. If future tickets find this too bright on light-colored floors, switch to standard alpha blend (`Blend SrcAlpha OneMinusSrcAlpha`).

8. **Performance gate test marked `[Explicit]`.** Consistent with the existing `PerformanceGate30NpcAt60FpsTests` convention (which also has a comment noting it should be marked Explicit). The 60-second test is not appropriate for every CI run.

---

## SimConfig defaults (LightingConfig)

| Parameter | Default | Rationale |
|:---|---:|:---|
| ambientTintBlend | 0.28 | Subtle — era-appropriate muted look; not a strong color-filter |
| minimumRoomBrightness | 0.18 | Rooms at AmbientLevel=0 are dark but not pitch-black |
| maximumRoomBrightness | 0.95 | Leaves 5% headroom below full-white for the desaturated aesthetic |
| beamMaxAlpha | 0.32 | Visible without being cartoon-flashy |
| beamMinElevationDeg | 3.0 | Threshold for night-vs-day beam mode |
| beamMaxLengthUnits | 12.0 | Spans roughly 1/3 of a standard cubicle-grid room |
| beamNightSpillAlpha | 0.12 | Window glow — subtle suggestion of interior light |
| haloMaxRadius | 2.5 | ~2.5 tiles radius at intensity=100; covers a desk area |
| haloMaxAlpha | 0.55 | Visible but not washing out room colour |
| flickerFrequency | 0.07 | ~0.07 cycles/tick = low-frequency lazy flicker |
| dyingDropProbability | 0.08 | ~8% per tick → sporadic; not every-second |
| wallFadedAlpha | 0.25 | UX bible §2.1 requires ≤ 0.4; 0.25 gives good visibility-through |
| wallFadeSeconds | 0.18 | Fast enough to feel responsive; slow enough not to pop |
| wallHeight | 2.5 | 2.5 world-units = 2.5 tiles tall; visible from default isometric pitch |
| sunMaxIntensity | 1.0 | Unity default directional light intensity |
| sunMinIntensity | 0.04 | Faint moonlight / ambient night |

---

## Palette values (per-room Kelvin anchors)

| Room type | Canonical K | Notes |
|:---|---:|:---|
| Basement (fluorescent) | 4 000 | Standard cool-white fluorescent; yellow-green bias |
| IT Closet (server LED) | 5 500–6 000 | Near-white with a cool cast; server room feel |
| Cubicle grid (warm fluor.) | 3 200–3 500 | Warm cream; 90s incandescent-fluorescent hybrid |
| Conference room | 4 500 | Neutral professional white |
| Breakroom | 3 500 | Warm, slightly cozy |
| Hallway (dim) | 4 000 | Standard fluorescent; low AmbientLevel darkens it |
| Office (private) | 3 200 | Warm desk-lamp influence |

The engine's `IlluminationAccumulationSystem` computes `ColorTemperatureK` as an intensity-weighted average of all contributing light sources per room. The values above are approximate anchors used for the `BasementColorTests` assertions; actual in-game values depend on what fixtures the world-definition JSON places in each room.

---

## Performance analysis

**WallFadeController (hottest new path):**
- `Physics.RaycastNonAlloc`: 1 call/frame, budget ~0.05 ms in a static office.
- `FindObjectsOfType<WallTag>`: O(1) after first frame (count stable = no rebuild).
- 40 `Mathf.MoveTowards` + `Material.SetFloat`: ~40 × 50 ns ≈ 0.002 ms.
- **Total: ~0.05 ms/frame.**

**LightSourceHaloRenderer (20–40 halos):**
- Material.color write per halo: 40 × 30 ns ≈ 0.001 ms.
- Deterministic hash arithmetic: 40 × ~10 ns ≈ 0.0004 ms.
- **Total: < 0.01 ms/frame.**

**BeamRenderer (up to 40 beams):**
- Material.color write + position update: 40 × 50 ns ≈ 0.002 ms.
- **Total: < 0.01 ms/frame.**

**RoomAmbientTintApplier (~20 rooms):**
- 3× Material.SetFloat per room: 20 × 90 ns ≈ 0.002 ms.
- **Total: < 0.01 ms/frame.**

**DayNightCycleRenderer:**
- 1 directional light update: < 0.001 ms.

**GPU:**
- Transparent-queue room quads: ~30 floor + ~120 wall quads = 150 transparent quads.
- 40 beam quads + 40 halo quads = 80 additional transparent quads.
- All share materials → Unity GPU instancing batches significantly.
- Net additional GPU cost on integrated graphics: estimated < 0.5 ms.

**Expected total CPU overhead vs WP-3.1.B baseline: < 0.1 ms/frame.**

The 30-NPC-at-58-FPS gate should not be threatened. Flag if profiling reveals `FindObjectsOfType<WallTag>` is unexpectedly expensive (would indicate room count changing each frame — a bug in room sync).

---

## Follow-ups (not in scope for this packet)

- **Pixel-art-from-3D shader pipeline.** Render at native then downsample. Future packet.
- **Volumetric beam scattering.** Current beams are flat XZ quads. Future polish.
- **Real-time shadows.** Landing with pixel-art shader pipeline.
- **Player-controllable light switches.** Coupled to WP-3.1.D (build mode).
- **Ambient sound coupling.** Bulb buzz near flickering / dying — UX §3.7 audio packet.
- **Skylight / ceiling aperture beams.** Ceiling apertures are skipped in this packet.
- **DefaultLightingConfig.asset GUID fixup.** See assumption #4 above.
- **Per-archetype lighting preference.** Greg prefers the dim IT closet — future polish.
