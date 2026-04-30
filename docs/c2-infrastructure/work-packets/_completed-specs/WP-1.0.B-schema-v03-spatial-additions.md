# WP-1.0.B — Schema v0.3 Spatial Additions

**Tier:** Sonnet
**Depends on:** WP-1.0.A.1 (must already be merged — this packet bumps schemaVersion on top of 0.2.1)
**Timebox:** 90 minutes
**Budget:** $0.30

---

## Goal

Land the v0.3 minor bump on `world-state.schema.json` — the **first move on the spatial axis**, **promoted from v0.5 because the aesthetic bible makes spatial structure a foundation system** for both lighting (priority 1) and proximity (priority 2). This packet adds three optional surfaces to the wire format and one extension to an existing object:

1. **`rooms[]` top-level array.** First-class room entities with id, name, category, bounds, floor, and illumination state. Rooms are what proximity subscribes to ("same-room awareness") and what lighting accumulates onto.
2. **`lightSources[]` top-level array.** Interior light sources (overhead fluorescents, desk lamps, server LEDs, breakroom strip) with state (on/off/flickering/dying), intensity, color temperature, position, and the room they live in.
3. **`lightApertures[]` top-level array.** Windows. Each admits a beam from the sun-position vector defined on the clock state. Carries position, the room it admits light into, the cardinal direction it faces, and area.
4. **`clock.sun` extension.** Existing `clock` object gains a `sun` sub-object with `azimuthDeg`, `elevationDeg`, and `dayPhase` enum. Time-of-day is the input to lighting; the sim already tracks circadian factor — this exposes the geometry on the wire.

Bump `schemaVersion` enum from `["0.1.0", "0.2.1"]` to `["0.1.0", "0.2.1", "0.3.0"]`. v0.1 and v0.2.1 samples must continue to round-trip cleanly under v0.3 (additive compatibility — every new field is optional). Pure contract bump; engine-side population of any of these is the spatial-engine packet that follows (Phase 1.1).

---

## Reference files

- `docs/c2-content/DRAFT-aesthetic-bible.md` — priority-1 (lighting) and priority-2 (proximity) systems. Source of light-source kinds, light state values, day-phase enum, lighting-to-behavior mappings (mappings themselves are out of scope here, but the field set on the wire is what those mappings will read).
- `docs/c2-content/DRAFT-world-bible.md` — named anchors and the three-floor building. Source of the floor enum and the room category enum.
- `docs/c2-infrastructure/00-SRD.md` §8.6 (visual target is 2.5D top-down management sim — engine exposes spatial structure for AI-tier reasoning about places).
- `docs/c2-infrastructure/SCHEMA-ROADMAP.md` — currently has spatial at v0.5; this packet promotes it to v0.3 and rewrites the roadmap accordingly.
- `docs/c2-infrastructure/schemas/world-state.schema.json` — current v0.2.1 file to extend.
- `Warden.Contracts/SchemaValidation/world-state.schema.json` — embedded-resource mirror; must stay in sync.
- `Warden.Contracts/Telemetry/WorldStateDto.cs` — DTO surface to extend.
- `Warden.Contracts/SchemaValidation/Schema.cs` — version constants.
- `Warden.Contracts.Tests/Samples/world-state-v021.json` — v0.2.1 sample, to be preserved untouched.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.0.A.md` — for format reference of how a contract bump is structured.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.0.A.1.md` — confirms the v0.2.1 baseline this packet builds on top of.

## Non-goals

- Do **not** modify the engine. No spatial index, no rooms-as-engine-entities, no light-system code. v0.3 is a wire-format bump only; engine population is Phase 1.1 / 1.2.
- Do **not** populate the new fields in `Warden.Telemetry` projector. The projector still emits `SchemaVersion = "0.1.0"` and absent/empty arrays; existing projector tests must stay green.
- Do **not** add a `rooms`-membership field on `entities[].position`. Tempting (per the schema-roadmap's earlier plan), but room-membership-on-the-entity is a derived index that the engine maintains; adding it to the wire now would require the engine to populate it, which is engine-side work the next packet owns. Keep room-membership *implicit* at v0.3 — derivable from `position` + `rooms[].bounds`. The follow-up engine packet may add an explicit field later as a v0.3.x.
- Do **not** add lighting-to-drive coupling fields (e.g., per-room `irritationContribution`). Those are SimConfig values, not wire-state. The bibles' mappings are configuration, not telemetry.
- Do **not** add a `Stain` or `BrokenItem` entity kind. Those are v0.4 chronicle work.
- Do **not** add named-anchor authoring (the Microwave, the Window, the Smoking Bench). The schema commits the *shape* of a room and a light source. Specific named anchors are content, populated by the world-bootstrap packet (Phase 1.7) reading `world-definition.json`.
- Do **not** introduce a NuGet dependency. The minimal in-house validator stays the only validator.
- Do **not** retry, recurse, or "self-heal" on schema-validation failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (Architectural axiom 8.1.)
- Do **not** touch any v0.2.1 surfaces (`entities[].social`, `relationships[]`, `memoryEvents[]`). Those are correct as landed; this packet is purely additive on top.

---

## Design notes

### Rooms as wire entities, not engine entities (yet)

`rooms[]` is a top-level array, `maxItems: 64` (the building has three floors and ~25 active rooms; 64 gives slack for future expansions, supply closets, parking-lot zones, etc.). Each room object:

- `id` — UUID.
- `name` — short string, `maxLength: 64`. Examples: `"first-floor-breakroom"`, `"basement-loading-dock"`, `"top-floor-conference-room"`. Slug-like; named anchors from the world bible are referenced by id, not by name.
- `category` — enum: `breakroom, bathroom, cubicleGrid, office, conferenceRoom, supplyCloset, itCloset, hallway, stairwell, elevator, parkingLot, smokingArea, loadingDock, productionFloor, lobby, outdoor`. Open-ended; the cast bible doesn't preclude additions. **The aesthetic bible's lighting-to-behavior mappings are keyed on `category`** — flickering breakroom strips are different from server-room LEDs because the categories differ.
- `floor` — enum: `basement, first, top, exterior`. Three building floors plus an `exterior` for parking lot, smoking bench, anything outside the building.
- `boundsRect` — required object: `{x, y, width, height}` in tile units, all `0–512` integers. Grid-aligned bounds are sufficient for v0.3; polygons are deferred. Tile coordinate system is the same as the existing `entities[].position`.
- `illumination` — required object: `{ambientLevel: 0–100, colorTemperatureK: 1000–10000, dominantSourceId: optional UUID}`. The dominant-source field references a `lightSources[].id` that is "the main light" the player would say lights this room. Optional because some rooms (parking lot at midnight) have no dominant source.

`additionalProperties: false` at the room object level. The `rooms[]` array is optional (absent on v0.1/v0.2.1 samples).

### Light sources

`lightSources[]` top-level array, `maxItems: 256` (the office has lots of lights — every cubicle desk lamp, every overhead bank, every server-room LED counts). Each source:

- `id` — UUID.
- `kind` — enum from the aesthetic bible: `overheadFluorescent, deskLamp, serverLed, breakroomStrip, conferenceTrack, exteriorWall, signageGlow, neon, monitorGlow, otherInterior`.
- `state` — enum from the aesthetic bible: `on, off, flickering, dying`. `dying` is a slow-fade state that persists until the source is replaced; `flickering` produces the irritation-bump-per-minute mapping the bible commits to.
- `intensity` — `0–100` integer. 0 means the source produces no light even when `state == on` (a dead bulb still in its socket).
- `colorTemperatureK` — `1000–10000` integer. Era-appropriate fluorescents are 4000–5000K; warm desk lamps 2700–3000K; server LEDs 6500K-cool with a slight pale-yellow flicker.
- `position` — `{x, y}` in tile units, both `0–512` integers. Same coordinate system as entities.
- `roomId` — required UUID, references `rooms[].id`.

`additionalProperties: false`. Optional array.

### Light apertures (windows)

`lightApertures[]` separate top-level array, `maxItems: 64`. Apertures admit *exterior* light driven by sun-position; they're geometrically distinct from interior sources and behave differently (beam direction, day-phase color shift, night reversal where interior light spills out instead). Each aperture:

- `id` — UUID.
- `position` — `{x, y}` in tile units.
- `roomId` — required UUID, references `rooms[].id` — the room the aperture admits light *into*.
- `facing` — enum: `north, east, south, west, ceiling`. `ceiling` covers skylights (one of the top-floor offices may have one). Direction determines which sun-azimuth ranges produce a beam.
- `areaSqTiles` — `0.5–64` number (allow non-integer for half-tile-wide windows). Larger windows admit more light at the same sun angle.

`additionalProperties: false`. Optional array.

### Sun position on the clock

`clock.sun` is an optional sub-object on the existing `clock` object (which currently has `gameTimeDisplay`, `dayNumber`, `isDaytime`, `circadianFactor`, `timeScale`). Adding `sun` doesn't break v0.1 or v0.2.1 consumers because they ignore unknown sub-fields. The `sun` object:

- `azimuthDeg` — `0–360` number. Sun's compass direction, 0 = north, 90 = east, etc.
- `elevationDeg` — `−90` to `90` number. Negative is below horizon; positive is above. The aesthetic bible's "early morning" / "afternoon" / "evening" shapes map onto elevation ranges.
- `dayPhase` — enum: `night, earlyMorning, midMorning, afternoon, evening, dusk`. The bible's six-phase shape; redundant with elevation+azimuth but cheap and tractable for AI-tier reasoning.

All three fields required when `sun` is present. `sun` itself is optional. v0.1/v0.2.1 samples with no `sun` continue to validate.

### Why rooms are required to have bounds, not optional

A room without bounds is unanswerable by the spatial query "is entity X in room Y." Optional bounds would mean the engine has to invent them at projection time or reject queries. Required bounds make the wire format self-sufficient for AI-tier reasoning about places. Polygon support is deferred to a later patch; rect bounds are sufficient for the early-2000s grid-aligned office layout the world bible commits to.

### Why lightSources and lightApertures are separate arrays

They behave differently. Sources radiate from a fixed position with a fixed direction-distribution; apertures admit a beam from a moving sun-position. Mixing them in one array would force every entry to carry both sun-direction and intrinsic-direction fields, with most being null. Two arrays is cleaner and matches the aesthetic bible's distinction.

### Schema version enum: keep both prior versions

`schemaVersion` enum becomes `["0.1.0", "0.2.1", "0.3.0"]` — three accepted versions. v0.1 and v0.2.1 documents continue to be valid (additive compatibility); v0.3 documents stamp `"0.3.0"` to claim the new fields. We do not collapse v0.2.1 (unlike the v0.2.0 collapse in WP-1.0.A.1) because v0.2.1 is a real, stable contract that may have producers.

### Roadmap rewrite

`SCHEMA-ROADMAP.md` currently has spatial at v0.5. This packet rewrites:

- §v0.3 — was "persistent narrative chronicle." Becomes "spatial pillar."
- §v0.4 — becomes "persistent narrative chronicle" (was v0.3).
- §v0.5 — becomes "character definitions" (was v0.4).
- New §v0.6 — placeholder for whatever's next.

The shift is one minor bump down for chronicle and characters. Document the promotion's rationale: aesthetic bible promotes spatial to a foundation system; lighting and proximity both depend on rooms; therefore the contract bump must precede those engine packets.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `docs/c2-infrastructure/schemas/world-state.schema.json` (modified) | `schemaVersion` enum becomes `["0.1.0", "0.2.1", "0.3.0"]`. Add `rooms[]` top-level optional array (`maxItems: 64`) with `$defs/room`. Add `lightSources[]` top-level optional array (`maxItems: 256`) with `$defs/lightSource`. Add `lightApertures[]` top-level optional array (`maxItems: 64`) with `$defs/lightAperture`. Extend `clock` to allow optional `sun` sub-object (`$defs/sunState`). Every new object `additionalProperties: false`; every new array has explicit `maxItems`; every numeric field has explicit `minimum`/`maximum`. |
| code | `Warden.Contracts/SchemaValidation/world-state.schema.json` (modified) | Embedded-resource mirror — must match canonical bit-for-bit. |
| code | `Warden.Contracts/Telemetry/RoomDto.cs` (new) | `RoomDto(string Id, string Name, RoomCategory Category, BuildingFloor Floor, BoundsRectDto Bounds, IlluminationDto Illumination)`. Records `BoundsRectDto(int X, int Y, int Width, int Height)` and `IlluminationDto(int AmbientLevel, int ColorTemperatureK, string? DominantSourceId)`. Enums `RoomCategory` (sixteen values) and `BuildingFloor` (four values), camelCase JSON via `JsonStringEnumConverter`. |
| code | `Warden.Contracts/Telemetry/LightSourceDto.cs` (new) | `LightSourceDto(string Id, LightKind Kind, LightState State, int Intensity, int ColorTemperatureK, PositionStateDto Position, string RoomId)`. Reuse the existing `PositionStateDto` from v0.1 if it has `{x, y}` already; otherwise define a small `PointDto`. Enums `LightKind` (ten values) and `LightState` (four values). |
| code | `Warden.Contracts/Telemetry/LightApertureDto.cs` (new) | `LightApertureDto(string Id, PositionStateDto Position, string RoomId, ApertureFacing Facing, double AreaSqTiles)`. Enum `ApertureFacing` (five values). |
| code | `Warden.Contracts/Telemetry/SunStateDto.cs` (new) | `SunStateDto(double AzimuthDeg, double ElevationDeg, DayPhase DayPhase)`. Enum `DayPhase` (six values). |
| code | `Warden.Contracts/Telemetry/WorldStateDto.cs` (modified) | Add `IReadOnlyList<RoomDto>? Rooms`, `IReadOnlyList<LightSourceDto>? LightSources`, `IReadOnlyList<LightApertureDto>? LightApertures` top-level. Extend `ClockStateDto` to carry `SunStateDto? Sun`. All optional. |
| code | `Warden.Contracts/SchemaValidation/Schema.cs` (modified) | `SchemaVersions.WorldState` becomes `"0.3.0"`. |
| code | `Warden.Contracts/SchemaValidation/WorldStateReferentialChecker.cs` (modified) | New checks: every `lightSources[].roomId` resolves to a `rooms[].id`. Every `lightApertures[].roomId` resolves. Every `rooms[].illumination.dominantSourceId` (when present) resolves to a `lightSources[].id`. All with specific reason strings (`"light-source-room-missing"`, `"aperture-room-missing"`, `"dominant-source-missing"`). Rooms with overlapping `boundsRect` are *not* rejected (overlapping rooms are physically real — a hallway can pass under a balcony) but a duplicate `id` collision is rejected. |
| code | `Warden.Contracts.Tests/Samples/world-state-v030.json` (new) | Canonical v0.3 sample: ≥2 rooms (one breakroom, one office, with distinct categories and bounds), ≥3 light sources (overhead, desk lamp, dying — covers state diversity), ≥1 light aperture (a south-facing window), `clock.sun` populated. Must also retain the v0.2.1 social/relationships/memoryEvents content for full-stack coverage. |
| code | `Warden.Contracts.Tests/SchemaRoundTripTests.cs` (modified) | Add: (a) v0.3 sample round-trips clean; (b) v0.2.1 sample round-trips clean under v0.3 schema (additive compatibility); (c) v0.1 sample round-trips clean under v0.3 schema; (d) `lightSources[].roomId` pointing to a missing room is rejected with `"light-source-room-missing"`; (e) `lightApertures[].roomId` pointing to a missing room is rejected with `"aperture-room-missing"`; (f) `rooms[].illumination.dominantSourceId` pointing to a missing source is rejected with `"dominant-source-missing"`; (g) duplicate `rooms[].id` rejected; (h) `rooms[].boundsRect.width = 0` rejected by `minimum`; (i) `clock.sun.elevationDeg = 91` rejected by `maximum`; (j) absent `clock.sun` validates fine (optional). |
| doc | `docs/c2-infrastructure/SCHEMA-ROADMAP.md` (modified) | §v0.3 rewritten as the spatial pillar. Old v0.3 (chronicle) shifts to v0.4. Old v0.4 (characters) shifts to v0.5. New v0.6 placeholder. One-paragraph rationale at the top of §v0.3 explaining the promotion. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-1.0.B.md` | Completion note. Use the standard template. Explicitly enumerate: which v0.5-shaped fields were promoted, which were deferred (room-membership-on-position), and the schemaVersion enum's three-version state. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `world-state.schema.json` declares `schemaVersion` enum `["0.1.0", "0.2.1", "0.3.0"]`. Every new top-level surface (`rooms`, `lightSources`, `lightApertures`) is optional and has explicit `maxItems`. | unit-test |
| AT-02 | The pre-existing v0.1 sample round-trips clean under v0.3. | unit-test |
| AT-03 | The pre-existing v0.2.1 sample round-trips clean under v0.3 (additive compatibility holds across two minor bumps). | unit-test |
| AT-04 | The new v0.3 sample round-trips clean: schema validates, DTO deserialises, re-serialises to JSON semantically equal to the input. | unit-test |
| AT-05 | A v0.3 sample with `lightSources[].roomId` referencing a non-existent room id is rejected with reason `"light-source-room-missing"`. | unit-test |
| AT-06 | A v0.3 sample with `lightApertures[].roomId` referencing a non-existent room id is rejected with reason `"aperture-room-missing"`. | unit-test |
| AT-07 | A v0.3 sample with `rooms[].illumination.dominantSourceId` referencing a non-existent source id is rejected with reason `"dominant-source-missing"`. | unit-test |
| AT-08 | A v0.3 sample with two `rooms[]` entries sharing an `id` is rejected with reason `"duplicate-room-id"`. | unit-test |
| AT-09 | A v0.3 sample with `rooms[].boundsRect.width = 0` is rejected by schema `minimum`. | unit-test |
| AT-10 | A v0.3 sample with `clock.sun.elevationDeg = 91` is rejected by schema `maximum`. | unit-test |
| AT-11 | A v0.3 sample with no `clock.sun` (i.e., the existing v0.2.1 clock shape) validates and round-trips clean. | unit-test |
| AT-12 | A v0.3 sample with a `lightSources[].state = "burned-out"` is rejected by enum (only `on, off, flickering, dying` valid). | unit-test |
| AT-13 | A v0.3 sample with overlapping room `boundsRect` validates fine. (Overlap is allowed; it's not a referential error.) | unit-test |
| AT-14 | `Warden.Telemetry.Tests` all pass — projector still emits `SchemaVersion = "0.1.0"` and the new arrays are absent from output. | build + unit-test |
| AT-15 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-16 | `dotnet test ECSSimulation.sln` — every existing test stays green; new tests pass. | build |

---

## Followups (not in scope)

- Spatial-engine packet (Phase 1.1): `Room` as an `APIFramework` entity, spatial index, proximity events (`entered-conversation-range`, `left-room`, `visible-from-here`), telemetry projection populating `rooms[]` and `entities[].position.roomId` (which may also be a v0.3.x patch on the wire format).
- Lighting-engine packet (Phase 1.2): sun-position system driven by `clock.dayPhase`, light-source state systems, telemetry projection populating `lightSources[]`, `lightApertures[]`, `clock.sun`.
- Lighting-to-drive coupling (Phase 1.5): the bible's mappings (flickering → irritation, sunbeam → mood, dark → suspicion). Reads room state and light state; writes drive deltas. `SimConfig.json` carries the coefficient values.
- World-bootstrap packet (Phase 1.7): `world-definition.json` loader instantiates rooms, light sources, apertures, and entities at boot. Named anchors from the world bible become concrete room/source ids.
- Polygon room bounds (deferred): rect is sufficient for the early-2000s grid-aligned office. If a future floor needs an L-shaped or curved space, a `boundsPolygon` alternative goes in alongside `boundsRect` as a v0.3.x patch.
- Day-phase length tuning: the six-phase enum is structural; the *durations* of each phase live in `SimConfig.json`, not on the wire.
