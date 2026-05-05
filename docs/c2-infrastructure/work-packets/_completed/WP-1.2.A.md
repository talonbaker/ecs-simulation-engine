# WP-1.2.A — lighting-engine-sun-apertures-sources — Completion Note

**Executed by:** sonnet-4.6
**Branch:** feat/wp-1.2.A
**Started:** 2026-04-25T00:00:00Z
**Ended:** 2026-04-25T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Landed the lighting layer on top of the WP-1.1.A spatial engine. Four systems live in `APIFramework/Systems/Lighting/`: `SunSystem` (circadian sun position), `LightSourceStateSystem` (flickering/dying state machines), `ApertureBeamSystem` (per-window beam contributions), and `IlluminationAccumulationSystem` (per-room ambient sum). A `SunStateService` singleton threads sun state between them without ECS component overhead.

**Key judgement calls:**

1. **`SimulationClock.CircadianFactor` is not 0..1.** The existing property is a sleep-urgency multiplier (0.10–1.60), not a day fraction. `SunSystem` reads `Clock.GameTimeOfDay / SimulationClock.SecondsPerDay` to obtain the true 0..1 cycle position. The packet's description of this property was incorrect; the orbital formulas assume 0..1 and are unchanged.

2. **`ProximityEventSystem` moved to Lighting phase (7).** The packet requires lighting to run *before* proximity events (lighting reads room membership; proximity events are the last consumer). Moving ProximityEventSystem from `Spatial=5` to `Lighting=7` makes this dependency explicit and puts the new phase to immediate use. All prior proximity tests still pass.

3. **`ApertureBeamState` made `public`.** Initially `internal`, but `ApertureBeamSystem.GetBeamState()` is a public method — C# requires the return type to match. Made public; no API surface concern since it's a readonly record.

4. **Aperture facing semantics:** `North` facing means the window opens outward to the north and admits light coming from the north (sun azimuth within ±90° of 0°). `South` admits the noon sun (azimuth 180°). This is the physically correct model; tests confirm AT-07 and AT-08.

5. **`SunStateService.UpdateSunState` made `public`** so test harnesses can inject a known sun state without driving the clock.

**Runtime state now available:**
- `SunStateService.CurrentSunState` — live sun azimuth, elevation, and DayPhase.
- Per-aperture `ApertureBeamSystem.GetBeamState(entity)` — intensity and color temperature of the current beam.
- `LightSourceStateSystem.GetEffectiveIntensity(entity)` — per-tick effective intensity (flickering resolution, dying decay).
- `RoomComponent.Illumination` — `AmbientLevel`, `ColorTemperatureK`, `DominantSourceId` updated every tick by `IlluminationAccumulationSystem`.

**Reserved for follow-ups:** Lighting-to-drive coupling (WP-1.5.A), lighting-affects-movement (WP-1.3.A tuning), wire projection (`lightSources[]`, `lightApertures[]`, `clock.sun`) via the unified projector packet.

---

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | OK | `LightSourceComponentTests` and `LightApertureComponentTests` verify field-by-field round-trip and enum integer alignment with Warden.Contracts DTOs. |
| AT-02 | OK | `SunSystem_AtDayFraction055_ProducesAfternoonPhase` — dayFraction=0.55 → `DayPhase.Afternoon`. |
| AT-03 | OK | `SunSystem_BetweenSunriseAndSunset_ElevationIsPositive` and `AtNight_ElevationIsNegative` pass for boundary-bracketing values. |
| AT-04 | OK | `FlickeringSource_ProducesBothOnAndOffTicks_Over1000Samples` — both on and off ticks observed; on-rate in [50%, 90%]. |
| AT-05 | OK | `DyingSource_IntensityDecaysTowardZero` and `TransitionsToOff_WhenIntensityReachesZero` both pass. |
| AT-06 | OK | `OnSource_DoesNotTransition` and `OffSource_DoesNotTransition` over 1000 ticks. |
| AT-07 | OK | `SouthFacingAperture_AdmitsBeam_AtNoon` — azimuth=180°, elevation=90° → positive beam. |
| AT-08 | OK | `AnyAperture_NoBeam_WhenElevationAtOrBelowHorizon` covers all five facing values. |
| AT-09 | OK | `EmptyRoom_AmbientLevel_IsZero`. |
| AT-10 | OK | `RoomWithOnSource_AtCenter_AmbientLevelNear80` — source at room center, intensity 80 → AmbientLevel in [70, 100]. |
| AT-11 | OK | `MultiSourceRoom_DominantSourceId_IsHighestContributor` — bright source at center wins over dim source at corner. |
| AT-12 | OK | `MultiSourceRoom_ColorTemperature_IsIntensityWeighted` — two sources (80 × 4000K, 40 × 6000K) → weighted avg ≈ 4666K. |
| AT-13 | OK | `LightingPipeline_TwoRunsSameSeed_ProduceIdenticalIlluminationTrajectories` over 5000 ticks with flickering + dying sources. |
| AT-14 | OK | All 24 `Warden.Telemetry.Tests` pass. `TelemetryProjector` still emits `SchemaVersion = "0.1.0"`; `Warden.Telemetry` not modified. |
| AT-15 | OK | All prior `APIFramework.Tests`, spatial, social, physiology tests still green. |
| AT-16 | OK | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors. |
| AT-17 | OK | `dotnet test ECSSimulation.sln` — 567 passed, 0 failed (up from 503; +64 new tests). |

---

## Files added

```
APIFramework/Components/DayPhase.cs
APIFramework/Components/LightKind.cs
APIFramework/Components/LightState.cs
APIFramework/Components/ApertureFacing.cs
APIFramework/Components/SunStateRecord.cs
APIFramework/Components/LightSourceComponent.cs
APIFramework/Components/LightApertureComponent.cs
APIFramework/Systems/Lighting/ApertureBeamState.cs
APIFramework/Systems/Lighting/SunStateService.cs
APIFramework/Systems/Lighting/SunSystem.cs
APIFramework/Systems/Lighting/LightSourceStateSystem.cs
APIFramework/Systems/Lighting/ApertureBeamSystem.cs
APIFramework/Systems/Lighting/IlluminationAccumulationSystem.cs
APIFramework.Tests/Components/LightSourceComponentTests.cs
APIFramework.Tests/Components/LightApertureComponentTests.cs
APIFramework.Tests/Systems/Lighting/SunSystemTests.cs
APIFramework.Tests/Systems/Lighting/LightSourceStateSystemTests.cs
APIFramework.Tests/Systems/Lighting/ApertureBeamSystemTests.cs
APIFramework.Tests/Systems/Lighting/IlluminationAccumulationSystemTests.cs
APIFramework.Tests/Systems/Lighting/LightingDeterminismTests.cs
docs/c2-infrastructure/work-packets/_completed/WP-1.2.A.md
```

## Files modified

```
APIFramework/Components/Tags.cs                  — add LightSourceTag, LightApertureTag
APIFramework/Components/EntityTemplates.cs       — add LightSource() and LightAperture() factory methods
APIFramework/Config/SimConfig.cs                 — add LightingConfig + DayPhaseBoundariesConfig classes; SimConfig.Lighting property
APIFramework/Core/SystemPhase.cs                 — add Lighting = 7 phase between Spatial and Physiology; update doc comments
APIFramework/Core/SimulationBootstrapper.cs      — add SunState service; register 4 lighting systems + move ProximityEventSystem to Lighting phase
SimConfig.json                                   — add "lighting" section with phase boundaries and stochastic probabilities
```

## Diff stats

21 files added, 6 files modified. ~900 insertions across production code; ~500 insertions in tests.

## Followups

- WP-1.5.A: Lighting-to-drive coupling — flickering → irritation, sunbeam → mood lift, dark room → suspicion/loneliness bumps.
- Unified projector packet: emit `clock.sun`, `lightSources[]`, `lightApertures[]`, `rooms[].illumination` on the wire; bump schema to `"0.3.0"`.
- World-bootstrap packet (Phase 1.7): `world-definition.json` instantiates concrete fixtures and windows via `EntityTemplates.LightSource(...)` / `EntityTemplates.LightAperture(...)`.
- WP-1.3.A tuning: lighting-affects-movement-speed (NPCs slower in dim rooms) — small per-NPC modifier reading `RoomComponent.Illumination.AmbientLevel`.
- Building orientation parameterization: currently east=90°, south=180° is hardcoded; could be a `SimConfig.lighting.buildingOrientationOffsetDeg` if needed.
- Per-tile illumination: when a system needs "is this exact tile lit," derive from per-room illumination plus aperture beam projection.
- Light-source NPC interactions (switch flips, fault transitions) — deferred to NPC interaction packets.
