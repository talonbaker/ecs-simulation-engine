# WP-3.1.C — Lighting Visualization

> **DO NOT DISPATCH UNTIL WP-3.1.A IS MERGED.**
> This packet renders the engine's per-room illumination state on top of the 3.1.A scaffold. The `EngineHost`, `WorldStateProjectorAdapter`, and `RoomRectangleRenderer` must already exist on `main`. The 30-NPCs-at-60-FPS performance gate from 3.1.A also remains binding.

**Tier:** Sonnet
**Depends on:** WP-3.1.A (Unity scaffold), WP-1.2.A and WP-1.2.B (engine lighting layer — sun, apertures, illumination accumulation, projector population) — both Phase 1, already merged
**Parallel-safe with:** WP-3.1.B (silhouettes — different render concern), WP-3.1.D (build mode — different system surface), WP-3.1.F (JSONL stream — telemetry)
**Timebox:** 150 minutes
**Budget:** $0.60

---

## Goal

The engine has tracked illumination state per-room since Phase 1: ambient level, beam direction from windows, light-source state (on / off / flickering / dying), color temperature. The visualizer has rendered none of it. This packet closes that loop. After this packet:

- Each room's render reflects its current `RoomIllumination` (ambient color × intensity).
- Sun beams from windows visibly slice across floors at low-angle morning / evening light.
- Flickering fluorescents visibly flicker (tied to engine `LightSourceState`, not Unity's wall-clock).
- Dark hallways at night are dark.
- The IT closet glows pale yellow.
- Walls fade to translucent when they occlude the player's view of the focused area (per UX bible §2.1).

This is the first packet where the simulation looks *like an office* instead of *like a debug grid.* Lighting is the single highest-impact alive-feel system per the aesthetic bible §priority-1, and the engine has been waiting for a renderer.

The packet does **not** ship the pixel-art-from-3D shader pipeline (per aesthetic bible's deferred visual style). It uses Unity's built-in render pipeline + simple lit shaders + post-processing for the beam effect. The pixel-art-shader pipeline is a separate future packet, paired with art-pipeline work.

---

## Reference files

- `docs/c2-infrastructure/work-packets/WP-3.1.A-unity-scaffold-and-baseline-render.md` — what's on disk: `RoomRectangleRenderer`, `EngineHost`, camera with stub for wall-fade hook.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.2.A.md` and `WP-1.2.B.md` — engine lighting layer. Read for the shape of `RoomIllumination` (per-region intensity, color temp), `LightApertureComponent`, `LightSourceComponent` (state + color temp), `SunStateRecord` (sun position over the day cycle).
- `docs/c2-content/aesthetic-bible.md` — §"Priority 1 — Lighting" — the design contract: flickering fluorescent → irritation; warm desk lamp → comfort; dark hallway → suspicion; sun-beam seat → mood lift. This packet renders these states; the engine's lighting→drive coupling already happens (WP-1.5.A). Read also §"Time of day" for the morning/afternoon/evening/night color and angle commitments.
- `docs/c2-content/ux-ui-bible.md` §2.1 — wall-fade-on-occlusion behavior commitment.
- `APIFramework/Components/RoomIllumination.cs` — `AmbientIntensity`, `BeamSources`, `ColorTemperatureKelvin`, `IsDirectSunlit`, etc.
- `APIFramework/Components/LightApertureComponent.cs`, `LightSourceComponent.cs`, `LightState.cs` — per-window aperture and per-light-source state.
- `APIFramework/Components/SunStateRecord.cs` — sun position per game-hour.
- `APIFramework/Systems/Lighting/*` — engine lighting systems. **Read-only**; this packet does not modify engine code.
- `Warden.Telemetry/Projectors/*` — `WorldStateDto.rooms[].illumination` and `WorldStateDto.lightSources[]` (or whatever the projection surface is). Verify by reading the projector source.
- `ECSUnity/Assets/Scripts/Render/RoomRectangleRenderer.cs` (from 3.1.A) — extended in this packet to apply illumination color/intensity to the room mesh.
- `ECSUnity/Assets/Scripts/Camera/CameraController.cs` (from 3.1.A) — wall-fade hook integration.

---

## Non-goals

- Do **not** ship the pixel-art-from-3D rendering pipeline. Future packet.
- Do **not** modify the engine's lighting state, lighting systems, or lighting→drive coupling. Read-only.
- Do **not** add player-controllable light switches at v0.1. NPCs and the engine's autonomous state changes drive light state (a flickering bulb may eventually go to "dying" then "off"). Player control is a future build-mode-adjacent packet.
- Do **not** ship volumetric fog / atmospheric scattering effects. Sun beams are flat 2D-ish slices; volumetric is a polish packet.
- Do **not** add HDR / bloom / chromatic aberration. Era-appropriate look (per aesthetic bible) is muted, slightly desaturated, NOT modern-graphics-flashy.
- Do **not** ship the pickup-drop-and-break light fixtures verb. Future, coupled to physics packet.
- Do **not** modify the 30-NPCs-at-60-FPS performance gate from 3.1.A. Lighting must not violate it.
- Do **not** retry, recurse, or "self-heal."

---

## Design notes

### Render approach

Two layers of visual response to lighting:

**1. Per-room ambient tint.** `RoomRectangleRenderer` (extended) reads `RoomIllumination.AmbientIntensity` and `ColorTemperatureKelvin`, computes a tint color, applies to the room's mesh. Bright sunlit rooms render at 1.0× brightness with a warm yellow-white tint; dim hallways at 0.4× with a cool blue-grey tint; the basement at 0.6× with a fluorescent yellow-green; the IT closet at 0.5× with the pale yellow LED glow. Color temperature → RGB conversion uses a standard Kelvin-to-RGB lookup table.

**2. Beam overlays from apertures.** A `BeamRenderer` per window aperture renders a translucent yellow quad projected from the window into the room, oriented by `SunStateRecord.SunAngle`. The beam fades out over distance from the aperture (alpha falloff). Beams cull against walls that block the line. At night, beams flip — interior light spills *out* of windows (per aesthetic bible §"Time of day" — "Night: window beams flip").

**3. Light source halos.** A `LightSourceHaloRenderer` per `LightSourceComponent` entity renders a soft halo around the source. State-driven:
- `On`: steady halo at the source's `Intensity`.
- `Off`: no halo.
- `Flickering`: halo intensity oscillates per a deterministic frequency (engine-driven; reads from a `FlickerPhase` field on `LightSourceComponent` if present, else computed from `SimulationClock.CurrentTick * frequency`).
- `Dying`: halo at low intensity with random brief drops to 0 (deterministic via seeded RNG against the source entity id).

### Wall-fade-on-occlusion

UX bible §2.1 commits: walls fade to translucent when they occlude the player's view of the focused area. Implementation:

- Each frame, raycast from camera position toward the camera focus point (or selected entity if any).
- For walls between camera and focus, set their material's `_Alpha` parameter to a low value (e.g., 0.3).
- For walls not in the line of sight, set `_Alpha` to 1.0.
- Smooth interpolation over ~0.2 seconds to avoid flicker.

This requires walls to be on a `RoomBoundary` layer with appropriate colliders (or have a `WallTag` Unity component). The 3.1.A `RoomRectangleRenderer` may not have generated wall geometry separately — verify and add if missing. The fade-shader is a simple modification to the Standard shader exposing an `_Alpha` property.

### Time-of-day handling

`SunStateRecord` is updated by the engine's `SunSystem` (Phase 1, WP-1.2.A) per tick. The renderer reads `WorldStateDto.sunState` (or equivalent) and:
- Updates the directional Unity light's rotation to match the sun angle.
- Updates the directional light's color temperature to match (warm-orange at dawn, white at noon, warm-orange at dusk, cool dark blue at night).
- Triggers the day-night transition between interior-light-spill-out vs sun-spill-in for window beams.

A `DayNightCycleRenderer` MonoBehaviour owns the directional light reference and updates it from `WorldStateDto.sunState` per render frame.

### Performance considerations

The `LightSourceHaloRenderer` is the highest-cost addition: each light source (~20-40 in a typical office) gets a soft-shadow halo. Use cheap shader (alpha-blended quad with radial falloff texture) and batch by material. No real-time shadows (deferred to pixel-art-shader packet).

The wall-fade raycast is one cast per frame (camera → focus). Cheap.

The performance gate (30 NPCs at 60 FPS) is preserved. If lighting violates it, escalate as blocked.

### Tests

- `RoomAmbientTintTests.cs` — set `RoomIllumination.AmbientIntensity = 0.5f` on a room; assert `RoomRectangleRenderer` mesh material `_Color` reflects the tint.
- `BeamRendererSunlitTests.cs` — at game-hour 10 (mid-morning), assert beams from east-facing windows are visible; at game-hour 14, assert west-facing beams.
- `BeamRendererNightFlipTests.cs` — at game-hour 22, assert interior lights spill out via window apertures (beam direction reversed).
- `LightSourceHaloOnOffTests.cs` — `LightState.On` → halo visible; `LightState.Off` → halo not visible.
- `LightSourceHaloFlickerTests.cs` — `LightState.Flickering` → halo intensity varies over 60 frames; deterministic per seed.
- `LightSourceHaloDyingTests.cs` — `LightState.Dying` → low base intensity with sporadic drops.
- `WallFadeOcclusionTests.cs` — camera positioned with wall between camera and selected entity; wall material `_Alpha` ≤ 0.4 within 1 second.
- `WallNoFadeWhenClearTests.cs` — camera positioned with no wall between; all walls at `_Alpha = 1.0`.
- `DayNightCycleTransitionTests.cs` — fast-forward sim to game-hour 18; directional light color shifts to warm-orange; at game-hour 22, shifts to cool-dark-blue.
- `BasementColorTests.cs` — basement room rendered with fluorescent yellow-green tint; ITCloset rendered with pale yellow LED tint.
- `PerformanceGate30NpcWithLightingTests.cs` — 30-NPCs with full lighting: min FPS ≥ 55, mean ≥ 58, p99 ≥ 50.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `ECSUnity/Assets/Scripts/Render/Lighting/RoomAmbientTintApplier.cs` | Per-room ambient tint. |
| code | `ECSUnity/Assets/Scripts/Render/Lighting/BeamRenderer.cs` | Per-aperture window beam. |
| code | `ECSUnity/Assets/Scripts/Render/Lighting/LightSourceHaloRenderer.cs` | Per-light-source halo. |
| code | `ECSUnity/Assets/Scripts/Render/Lighting/DayNightCycleRenderer.cs` | Directional light driver. |
| code | `ECSUnity/Assets/Scripts/Render/Lighting/WallFadeController.cs` | Camera-occlusion wall fade. |
| code | `ECSUnity/Assets/Scripts/Render/Lighting/KelvinToRgb.cs` | Color temperature → RGB lookup. |
| code | `ECSUnity/Assets/Scripts/Render/RoomRectangleRenderer.cs` (modified) | Apply ambient tint to mesh. |
| code | `ECSUnity/Assets/Scripts/Camera/CameraController.cs` (modified) | Trigger wall-fade raycast per frame. |
| asset | `ECSUnity/Assets/Shaders/RoomTint.shader` | Simple lit shader exposing tint + alpha. |
| asset | `ECSUnity/Assets/Shaders/BeamProjection.shader` | Translucent beam shader. |
| asset | `ECSUnity/Assets/Shaders/LightHalo.shader` | Soft halo shader. |
| code | `ECSUnity/Assets/Scripts/Render/Lighting/LightingConfig.cs` | ScriptableObject with tunables. |
| asset | `ECSUnity/Assets/Settings/DefaultLightingConfig.asset` | Defaults. |
| test | `ECSUnity/Assets/Tests/Play/RoomAmbientTintTests.cs` | Tint applied. |
| test | `ECSUnity/Assets/Tests/Play/BeamRendererSunlitTests.cs` | Daytime beams. |
| test | `ECSUnity/Assets/Tests/Play/BeamRendererNightFlipTests.cs` | Night flip. |
| test | `ECSUnity/Assets/Tests/Play/LightSourceHaloOnOffTests.cs` | On/off. |
| test | `ECSUnity/Assets/Tests/Play/LightSourceHaloFlickerTests.cs` | Flicker. |
| test | `ECSUnity/Assets/Tests/Play/LightSourceHaloDyingTests.cs` | Dying. |
| test | `ECSUnity/Assets/Tests/Play/WallFadeOcclusionTests.cs` | Wall fade. |
| test | `ECSUnity/Assets/Tests/Play/WallNoFadeWhenClearTests.cs` | No fade when clear. |
| test | `ECSUnity/Assets/Tests/Play/DayNightCycleTransitionTests.cs` | Day-night. |
| test | `ECSUnity/Assets/Tests/Play/BasementColorTests.cs` | Per-room palette. |
| test | `ECSUnity/Assets/Tests/Play/PerformanceGate30NpcWithLightingTests.cs` | **FPS preserved.** |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.1.C.md` | Completion note. SimConfig defaults. Performance measurements. Whether the wall-fade behaved smoothly at 30+ FPS. Visual-style notes (what specific palette values were chosen for ITCloset, basement, conference room). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `RoomIllumination.AmbientIntensity` change reflects in room mesh tint within 1 frame. | play-mode test |
| AT-02 | Daytime: beams from window apertures visible; angle matches `SunStateRecord.SunAngle`. | play-mode test |
| AT-03 | Nighttime: window beams reverse — interior light spills outward. | play-mode test |
| AT-04 | `LightSourceState.On` → halo visible at the source's intensity. `Off` → halo hidden. | play-mode test |
| AT-05 | `LightSourceState.Flickering` → halo intensity oscillates deterministically; same seed = same pattern. | play-mode test |
| AT-06 | `LightSourceState.Dying` → low base intensity with sporadic seed-deterministic drops. | play-mode test |
| AT-07 | Wall between camera and focus → wall material `_Alpha ≤ 0.4` within 1 second. | play-mode test |
| AT-08 | No wall between camera and focus → all walls at `_Alpha = 1.0`. | play-mode test |
| AT-09 | Day-night cycle: directional light color shifts continuously from cold-blue (night) → warm-orange (dawn) → white (noon) → warm-orange (dusk) → cold-blue (night). | play-mode test |
| AT-10 | Basement render uses fluorescent yellow-green tint; IT Closet uses pale yellow LED tint; first-floor cubicle area uses warm-cream tint. | play-mode test |
| AT-11 | **Performance gate.** 30 NPCs with full lighting: min ≥ 55, mean ≥ 58, p99 ≥ 50. | play-mode test |
| AT-12 | All Phase 0/1/2/3.0.x and 3.1.A tests stay green. | regression |
| AT-13 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-14 | Unity Test Runner: all 3.1.A, 3.1.B (if merged), 3.1.C tests pass. | unity test runner |

---

## Followups (not in scope)

- **Pixel-art-from-3D shader pipeline.** The aesthetic-bible commitment. Render the 3D scene at native resolution; downsample with a pixel-art shader. Future packet, paired with art-pipeline work.
- **Volumetric beam scattering / atmospheric fog.** Polish; current beams are flat alpha quads. Future.
- **Real-time shadows.** Current implementation has no dynamic shadows. Per aesthetic bible "Real shadows from real light, with a pixel-art look" is the eventual target — landing with the pixel-art shader pipeline.
- **Player-controllable light switches.** Player clicks a light fixture in build mode → toggles state. Couples to 3.1.D.
- **Ambient sound coupling.** Bulb buzz when camera near flickering / dying source. UX §3.7 audio packet.
- **Multi-floor lighting.** Single floor v0.1; future.
- **Light fixture pickup.** Coupled to physics packet. Future.
- **Per-archetype lighting preference.** Greg likes the dim IT closet; the Climber wants the corner office sun. Future polish.
