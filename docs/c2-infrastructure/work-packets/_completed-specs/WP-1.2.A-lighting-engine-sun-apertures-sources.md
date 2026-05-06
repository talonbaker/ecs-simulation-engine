# WP-1.2.A — Lighting Engine: Sun + Apertures + Sources + Illumination

**Tier:** Sonnet
**Depends on:** WP-1.1.A (rooms, spatial index, proximity events). Already merged on `staging` via PR #37.
**Parallel-safe with:** WP-1.3.A (Movement quality), WP-1.6.A (Narrative telemetry). Different file footprints; only `SimulationBootstrapper.cs` and `SimConfig.json` are commonly touched and conflicts there are sectional/auto-mergeable.
**Timebox:** 100 minutes
**Budget:** $0.50

---

## Goal

Land the lighting layer of the engine that the v0.3 wire format describes. Four things come live in `APIFramework`:

1. **Sun position driven by time-of-day.** A `SunSystem` reads `SimulationClock.CircadianFactor` (0..1 of day) and produces sun azimuth, elevation, and a six-phase day-phase enum (`night`, `earlyMorning`, `midMorning`, `afternoon`, `evening`, `dusk`) — exposed as queryable engine state for AI-tier reasoning.

2. **Light apertures (windows) as first-class entities.** `LightApertureComponent` mirrors the v0.3 `LightApertureDto`. Each window admits a beam from the sun-position vector when the sun's azimuth is within the aperture's facing range and elevation is positive (above horizon).

3. **Light sources (fixtures) as first-class entities.** `LightSourceComponent` mirrors `LightSourceDto`. Each source has a kind, a state machine (on/off/flickering/dying), intensity, color temperature, position, and a containing room. The `LightSourceStateSystem` ticks state machines (a `flickering` source produces stochastic on-off transitions; a `dying` source slowly fades intensity and eventually transitions to `off`).

4. **Per-room illumination accumulated each tick.** The `IlluminationAccumulationSystem` writes the room's `Illumination` field (`AmbientLevel`, `ColorTemperatureK`, `DominantSourceId`) by summing contributions from all light sources whose `RoomId` matches the room, plus a contribution from any aperture admitting sun into the room. The result is what proximity-aware behavior systems will eventually read for the bible's lighting-to-drive mappings.

What this packet does **not** do: lighting-to-drive coupling (irritation bumps under flickering fluorescents, etc.) — that's WP-1.5.A, which depends on lighting being live first. No telemetry projection of lighting state on the wire (deferred unified projector packet).

---

## Reference files

- `docs/c2-content/DRAFT-aesthetic-bible.md` — priority-1 (lighting). Source of light-source kinds, state-value names, day-phase mapping. **Read first.** Section "What the engine commits to" enumerates exactly the fields this packet implements.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.0.B.md` — confirms the v0.3 DTO surface this packet's components mirror.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.1.A.md` — confirms the spatial layer (rooms, spatial index, proximity events) this packet builds on.
- `Warden.Contracts/Telemetry/LightSourceDto.cs`, `LightApertureDto.cs`, `SunStateDto.cs`, `RoomDto.cs` — wire-format shapes the new components mirror field-by-field.
- `APIFramework/Components/RoomComponent.cs` — existing room shape from WP-1.1.A. Lighting writes `Illumination` on this; do not change `RoomComponent` itself.
- `APIFramework/Core/ISpatialIndex.cs`, `APIFramework/Core/GridSpatialIndex.cs` — existing spatial primitives. Lighting can use `QueryRadius` to find sources affecting a position.
- `APIFramework/Core/SimulationClock.cs` — `CircadianFactor` is the input to `SunSystem`.
- `APIFramework/Core/SimulationBootstrapper.cs` — system + service registration site.
- `APIFramework/Core/SystemPhase.cs` — phase enum. Lighting runs after spatial sync, before social/proximity consumers, so per-tick illumination is current when consumed.
- `APIFramework/Components/Tags.cs` — for `LightSourceTag`, `LightApertureTag`.
- `APIFramework/Core/SeededRandom.cs` — RNG source for flicker stochasticity.
- `SimConfig.json` — runtime tuning lives here.
- `APIFramework.Tests/Systems/*.cs` — pattern reference.

## Non-goals

- Do **not** modify `Warden.Telemetry/TelemetryProjector.cs` or any file under `Warden.Telemetry/`. Wire-format population of lighting state is a deferred unified-projector packet. **This is the parallel-safety contract with WP-1.3.A and WP-1.6.A.**
- Do **not** modify any file under `Warden.Contracts/`. DTOs already exist.
- Do **not** modify any file under `docs/c2-infrastructure/schemas/`. No schema bump.
- Do **not** implement lighting-to-drive coupling (the bible's mappings: flickering → irritation, sunbeam → mood lift, dark → suspicion). That's WP-1.5.A. This packet exposes per-room illumination as engine-readable state; consumers come later.
- Do **not** implement lighting-affects-movement-speed or lighting-affects-pathfinding. Those are tuning concerns for the movement packet (WP-1.3.A) which runs in parallel.
- Do **not** add render-style or visual-effect fields. The simulation is headless; lighting is *queryable state* (per the bible), not pixels.
- Do **not** populate any specific named light fixtures or windows (the buzzing breakroom strip, the server-room LEDs, the parking-lot floodlights). The world-bootstrap packet (Phase 1.7) reads `world-definition.json` and instantiates concrete sources/apertures. This packet ships only the *capability*.
- Do **not** implement raymarching or photon-style propagation. Per-room illumination is a sum of contributions, not a per-tile field. Per-tile would be a future enhancement once perf data justifies it.
- Do **not** change movement-related systems or files. Movement quality is WP-1.3.A; staying off those files keeps the parallel dispatch clean.
- Do **not** modify `SunSystem` to react to weather. Cloud cover, rain, etc., are out of scope; the sun is a deterministic time-of-day function.
- Do **not** add a NuGet dependency.
- Do **not** use `System.Random`. `SeededRandom` only.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (Architectural axiom 8.1.)

---

## Design notes

### Sun position from time-of-day

The existing `SimulationClock.CircadianFactor` is `0..1` over a 24-hour cycle. Map this to sun position with a simple orbital model:

- `azimuthDeg = 360 * circadianFactor` (sun rises in the east at 0.25, peaks south at 0.5, sets in the west at 0.75 — building's east/north convention is fixed; orientation parameterization is a v0.3.x followup if needed).
- `elevationDeg = 90 * sin(2π * (circadianFactor - 0.25))` — peaks +90° at noon (0.5), -90° at midnight (0.0), zero at sunrise/sunset.
- `dayPhase` derived from `circadianFactor` ranges:
  - `[0.00, 0.20)` → `night`
  - `[0.20, 0.30)` → `earlyMorning`
  - `[0.30, 0.45)` → `midMorning`
  - `[0.45, 0.65)` → `afternoon`
  - `[0.65, 0.80)` → `evening`
  - `[0.80, 0.85)` → `dusk`
  - `[0.85, 1.00)` → `night`

Phase boundaries are tunable via `SimConfig.lightingDayPhaseBoundaries`. The above are starting points.

`SunStateService` (a singleton, registered in DI) exposes the current sun state. Systems read it; nothing writes it directly except `SunSystem`.

### Light apertures (windows)

`LightApertureComponent`:
- `Id: string` (UUID)
- `Position: (int X, int Y)`
- `RoomId: string` — the room the aperture admits light *into*
- `Facing: ApertureFacing` enum: `North, East, South, West, Ceiling`
- `AreaSqTiles: float` — `0.5..64.0`

A `LightApertureTag` marks aperture entities for system iteration.

The `ApertureBeamSystem` (per-tick) computes for each aperture:
- If sun elevation ≤ 0, no beam. The aperture admits no light.
- Otherwise, compute the beam's contribution based on alignment between sun azimuth and aperture facing:
  - `North` faces 0°, accepts azimuth within ±90° of north (so 270°–360° and 0°–90°)
  - `East` faces 90°, accepts ±90°
  - `South` faces 180°, accepts ±90°
  - `West` faces 270°, accepts ±90°
  - `Ceiling` accepts any azimuth when elevation > 30° (skylight)
- Within the accepted range, beam intensity scales with `sin(elevation)` × `cos(angle off-axis)` × `AreaSqTiles`.
- The beam's color temperature is sun-phase-dependent: cold at dawn/dusk (~4000K), warm at noon (~5500K), red-orange at sunset (~3000K).

The system writes a per-aperture `currentBeamContribution` (intensity 0–100, color temperature K) that the illumination accumulator reads. A new internal record `ApertureBeamState` (not a component, just an in-engine cache) holds these.

### Light sources (fixtures)

`LightSourceComponent`:
- `Id: string`
- `Kind: LightKind` — enum mirroring schema's ten values
- `State: LightState` — enum: `On, Off, Flickering, Dying`
- `Intensity: int` — `0..100`
- `ColorTemperatureK: int` — `1000..10000`
- `Position: (int X, int Y)`
- `RoomId: string`

A `LightSourceTag` marks source entities.

The `LightSourceStateSystem` (per-tick) handles state machines:

- `On` and `Off` are stable; no transitions in this packet (transitions happen via external triggers — e.g., an NPC flipping a switch, or a tool-induced fault, both of which are later packets).
- `Flickering` produces stochastic on/off micro-transitions: each tick, with probability `SimConfig.lightingFlickerOnProb` (default 0.7) the source emits at full intensity; otherwise emits at 0. The component's `Intensity` field is **not** modified — flicker is a per-tick *effective* intensity, cached separately. The component records the source as flickering; the effective intensity is computed by the accumulator. Use `SeededRandom`, never `System.Random`.
- `Dying` slowly fades: each tick, decrement `Intensity` by 1 with probability `SimConfig.lightingDyingDecayProb` (default 0.05; over many ticks, intensity drifts to 0). When `Intensity == 0`, transition `State` to `Off`. Determinism preserved because `SeededRandom` is the only RNG.

The system never resurrects sources; transitions away from `Dying`/`Off` happen via external triggers (replacements), not scheduled.

### Per-room illumination accumulation

`IlluminationAccumulationSystem` (per-tick, after sun + state systems): for each room entity, computes the new `Illumination` field:

- Sum contributions from all light sources where `RoomId == this room`. Contribution = `state-effective intensity × falloff(distance from source to room center)`. Falloff is linear within room bounds; outside the source's natural range it's zero. `effective intensity` for `Flickering` is the system's per-tick computed value; for `Dying` it's the decayed `Intensity`; for `On` it's `Intensity`; for `Off` it's 0.
- Add contributions from all apertures where `RoomId == this room`, using the sun-driven beam contribution (computed by `ApertureBeamSystem`).
- Sum totals to compute new `AmbientLevel` (clamped 0–100).
- Compute weighted-average color temperature across contributors (each contribution's color temperature weighted by its intensity).
- `DominantSourceId` is the source/aperture id with the highest single contribution to this room. If no contributors, null.

Update the room entity's `RoomComponent.Illumination` in place (immutable record, replace it).

### Why per-room and not per-tile

The bible commits to per-region illumination — "Each room has an `illumination` field — ambient level, per-tile (or per-region) intensity, color temperature." Per-room is the cheaper option and sufficient for AI-tier reasoning. Per-tile is a future enhancement: when a system needs to query "is this exact tile in shadow," we can add it. For now, "what's the breakroom's ambient lighting" is the question the engine answers.

### Phase ordering

In `SystemPhase`:
- SpatialIndexSync (existing) — keeps positions in the index
- RoomMembership (existing) — computes which entities are in which rooms
- **NEW: SunSystem** — computes sun state from circadian factor
- **NEW: LightSourceStateSystem** — handles flickering/dying transitions
- **NEW: ApertureBeamSystem** — computes beam contributions per aperture
- **NEW: IlluminationAccumulationSystem** — sums into per-room illumination
- ProximityEventSystem (existing)
- (everything else, including future lighting-to-drive coupling in WP-1.5.A)

Phase enum may need a new slot named `Lighting` to group the four new systems; the Sonnet decides whether that's tidier than slotting them into existing phases.

### Determinism

Flicker and dying decay both use `SeededRandom`. Two runs with the same seed produce byte-identical illumination trajectories over arbitrary tick counts. Tests verify this with a 5000-tick determinism check.

### SimConfig additions

```jsonc
{
  "lighting": {
    "dayPhaseBoundaries": {
      "earlyMorningStart": 0.20, "midMorningStart": 0.30,
      "afternoonStart":    0.45, "eveningStart":    0.65,
      "duskStart":         0.80, "nightStart":      0.85
    },
    "flickerOnProb":     0.70,
    "dyingDecayProb":    0.05,
    "apertureRangeBase": 5,
    "sourceRangeBase":   3
  }
}
```

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/LightSourceComponent.cs` | Record per Design notes. |
| code | `APIFramework/Components/LightApertureComponent.cs` | Record per Design notes. |
| code | `APIFramework/Components/LightKind.cs` | Enum, ten values mirroring schema. |
| code | `APIFramework/Components/LightState.cs` | Enum: `On, Off, Flickering, Dying`. |
| code | `APIFramework/Components/ApertureFacing.cs` | Enum: `North, East, South, West, Ceiling`. |
| code | `APIFramework/Components/DayPhase.cs` | Enum, six values mirroring schema. |
| code | `APIFramework/Components/SunStateRecord.cs` | `(double AzimuthDeg, double ElevationDeg, DayPhase DayPhase)`. (Distinct from `SunStateDto` — engine-side record; same shape.) |
| code | `APIFramework/Components/Tags.cs` (modified) | Add `LightSourceTag`, `LightApertureTag`. |
| code | `APIFramework/Systems/Lighting/SunStateService.cs` | Singleton exposing current sun state. Updated by SunSystem. |
| code | `APIFramework/Systems/Lighting/SunSystem.cs` | Reads `SimulationClock.CircadianFactor`, computes sun position, writes to `SunStateService`. |
| code | `APIFramework/Systems/Lighting/LightSourceStateSystem.cs` | Per-tick state-machine ticking for sources. |
| code | `APIFramework/Systems/Lighting/ApertureBeamSystem.cs` | Per-tick aperture beam contribution computation. |
| code | `APIFramework/Systems/Lighting/IlluminationAccumulationSystem.cs` | Per-tick per-room illumination sum. |
| code | `APIFramework/Systems/Lighting/ApertureBeamState.cs` | Internal cache record. Not a component. |
| code | `APIFramework/Components/EntityTemplates.cs` (modified) | Add factories: `LightSource(...)`, `LightAperture(...)`. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register `SunStateService` (singleton), four new systems in correct phase order. |
| code | `APIFramework/Core/SystemPhase.cs` (modified, if needed) | Add a `Lighting` phase if Sonnet judges it cleaner than slotting into existing phases. |
| code | `SimConfig.json` (modified) | Add the `lighting` section per Design notes. |
| code | `APIFramework.Tests/Components/LightSourceComponentTests.cs` | Field validation, enum bounds, room-id reference. |
| code | `APIFramework.Tests/Components/LightApertureComponentTests.cs` | Same shape. |
| code | `APIFramework.Tests/Systems/Lighting/SunSystemTests.cs` | At circadian factor 0.5, elevation peaks (~+90°), azimuth ~180°. At 0.0, elevation ~-90°. Day-phase boundaries match SimConfig. |
| code | `APIFramework.Tests/Systems/Lighting/LightSourceStateSystemTests.cs` | `Flickering` produces both on and off ticks across N samples; `Dying` slowly decays intensity and transitions to Off when zero; `On` and `Off` are stable. |
| code | `APIFramework.Tests/Systems/Lighting/ApertureBeamSystemTests.cs` | South-facing window admits beam at noon (azimuth 180°, elevation 90°), no beam at midnight (elevation -90°). North-facing admits when sun is in the south... actually, wait — facing dictates which direction the WINDOW POINTS. Verify a north-facing aperture (window points north) admits sun when sun is in the south (azimuth 180°). The Sonnet reads the aesthetic bible to confirm semantics; tests follow the bible's mental model. |
| code | `APIFramework.Tests/Systems/Lighting/IlluminationAccumulationSystemTests.cs` | Empty room → ambient 0. Room with one source on at intensity 80 → ambient near 80 (within falloff range). Room with one flickering source → ambient varies. Room with aperture and noon sun → ambient elevated. Color temperature averaging is intensity-weighted. |
| code | `APIFramework.Tests/Systems/Lighting/LightingDeterminismTests.cs` | Two runs, same seed, 5000 ticks → byte-identical illumination trajectories per room. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-1.2.A.md` | Completion note. Standard template. Enumerate (a) what runtime state is now available, (b) what consumers are reserved for follow-ups (lighting-to-drive coupling, lighting-affects-movement, projector). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | All new components compile and field-mirror sanity tests pass (LightSourceComponent ↔ LightSourceDto round-trip equal). | unit-test |
| AT-02 | `SunSystem` produces `dayPhase = afternoon` for `circadianFactor = 0.55` (within the SimConfig boundaries). | unit-test |
| AT-03 | `SunSystem` elevation is positive between sunrise (factor 0.25) and sunset (factor 0.75), negative outside. | unit-test |
| AT-04 | `LightSourceStateSystem`: `Flickering` source produces both on-ticks and off-ticks within 1000 samples (statistically; not deterministic per-tick but the distribution matches `flickerOnProb`). | unit-test |
| AT-05 | `LightSourceStateSystem`: `Dying` source intensity decays toward 0 over many ticks; transitions to `Off` when intensity reaches 0. | unit-test |
| AT-06 | `LightSourceStateSystem`: `On` and `Off` sources do not transition without external triggers. | unit-test |
| AT-07 | `ApertureBeamSystem`: south-facing aperture admits a beam at noon (sun azimuth 180°, elevation 90°). | unit-test |
| AT-08 | `ApertureBeamSystem`: any aperture admits no beam when sun elevation ≤ 0 (night). | unit-test |
| AT-09 | `IlluminationAccumulationSystem`: a room with no sources or apertures has `AmbientLevel == 0`. | unit-test |
| AT-10 | `IlluminationAccumulationSystem`: a room with one `On` source at intensity 80 has `AmbientLevel` within falloff range of 80. | unit-test |
| AT-11 | `IlluminationAccumulationSystem`: `DominantSourceId` is set to the highest-contribution source in a multi-source room. | unit-test |
| AT-12 | `IlluminationAccumulationSystem`: color temperature is intensity-weighted across multiple sources. | unit-test |
| AT-13 | `LightingDeterminismTests` produce byte-identical illumination trajectories across two runs with the same seed over 5000 ticks. | unit-test |
| AT-14 | `Warden.Telemetry.Tests` all pass — projector still emits `SchemaVersion = "0.1.0"` and lighting fields are absent (this packet does not modify the projector). | build + unit-test |
| AT-15 | All existing `APIFramework.Tests` stay green (rooms, social, physiology, movement). | build + unit-test |
| AT-16 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-17 | `dotnet test ECSSimulation.sln` — every existing test stays green; new tests pass. | build |

---

## Followups (not in scope)

- WP-1.5.A — Lighting-to-drive coupling: flickering fluorescents bump irritation, sunbeams nudge mood, dark hallways raise suspicion. Reads `RoomComponent.Illumination` and `LightSourceComponent.State`; writes drive deltas via the social-engine systems from WP-1.4.A.
- Per-tile illumination field (deferred): if a system needs "is this exact tile lit," add a per-tile cache derived from per-room values plus aperture beam projection.
- Lighting affects movement speed (NPCs walk slower in dim hallways) — small per-NPC modifier hooked into MovementSystem from WP-1.3.A.
- Light-source NPC interactions: an NPC flips a switch (`On` ↔ `Off`); a faulty fluorescent goes from `On` to `Flickering` to `Dying` over time. Trigger conditions are later packets.
- Weather/seasonal sun variation — out of scope; sun is deterministic time-of-day function.
- Building orientation parameterization (so windows-face-east is configurable) — v0.3.x followup if needed.
- Unified projector packet: emit `clock.sun`, `lightSources[]`, `lightApertures[]`, and `rooms[].illumination` on the wire. Bumps emitted `SchemaVersion` to `"0.3.0"`.
