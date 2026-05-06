# WP-1.2.B — Projector: Populate Spatial State on the Wire

**Tier:** Sonnet
**Depends on:** WP-1.2.A (lighting engine — merged on `staging`), WP-1.4.B (social projector update — merged; this packet bumps the version stamp on top of 0.2.1).
**Parallel-safe with:** WP-1.5.A (Lighting-to-drive — different file footprint), WP-1.7.A (World bootstrap — different file footprint).
**Timebox:** 60 minutes
**Budget:** $0.20

---

## Goal

Close the v0.3 spatial wire-format loop. WP-1.0.B added the schema fields for rooms, light sources, light apertures, and sun state. WP-1.1.A and WP-1.2.A ship the engine state for them. This packet teaches the projector to read that state and emit it on the wire.

Four fields move from absent to populated:

1. `rooms[]` — projected from `RoomComponent` entities tagged with `RoomTag`.
2. `lightSources[]` — projected from `LightSourceComponent` entities tagged with `LightSourceTag`.
3. `lightApertures[]` — projected from `LightApertureComponent` entities tagged with `LightApertureTag`.
4. `clock.sun` — projected from the `SunStateService` singleton.

Bump emitted `SchemaVersion` from `"0.2.1"` to `"0.3.0"`.

What this packet does **not** do: project facing direction (engine-internal at v0.3 — the schema doesn't reserve a field for it); project narrative-event candidates (those go through their own `ai narrative-stream` channel, not the world-state telemetry); add `entities[].position.roomId` to the wire (deferred — engine maintains it via `EntityRoomMembership`, but the schema-level optimization is a v0.3.x patch).

---

## Reference files

- `docs/c2-infrastructure/work-packets/_completed/WP-1.2.A.md` — confirms which engine components and services are available.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.4.B.md` — confirms the projector currently emits `SchemaVersion = "0.2.1"` and populates social state. This packet builds on top.
- `Warden.Telemetry/TelemetryProjector.cs` — primary file modified.
- `Warden.Telemetry.Tests/TelemetryProjectorTests.cs` — primary test file updated.
- `Warden.Contracts/Telemetry/RoomDto.cs`, `LightSourceDto.cs`, `LightApertureDto.cs`, `SunStateDto.cs`, `WorldStateDto.cs` — DTO target shapes.
- `APIFramework/Components/RoomComponent.cs`, `LightSourceComponent.cs`, `LightApertureComponent.cs`, `RoomIllumination.cs`, `BoundsRect.cs`, `Tags.cs` — engine sources.
- `APIFramework/Systems/Lighting/SunStateService.cs` — the singleton this packet reads sun state from.

## Non-goals

- Do **not** modify any file under `APIFramework/`. The engine is the source; this packet only reads.
- Do **not** modify any file under `Warden.Contracts/`. DTOs are stable.
- Do **not** modify any file under `docs/c2-infrastructure/schemas/`. No schema bump.
- Do **not** project facing direction, idle-jitter state, path waypoints, or any other engine-internal-only state. Wire format only carries what the v0.3 schema reserves.
- Do **not** add `entities[].position.roomId` to the wire. Even though `EntityRoomMembership` exposes it, the schema doesn't carry that field. v0.3.x patch.
- Do **not** populate narrative-event candidates on the world-state telemetry. They have a separate channel.
- Do **not** introduce a NuGet dependency.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (Architectural axiom 8.1.)

---

## Design notes

### Reading the sources

For `rooms[]`: iterate entities tagged with `RoomTag`. For each, read `RoomComponent` (id, name, category, floor, bounds, illumination) and project to `RoomDto`. Field-by-field mapping by name. `Illumination` projects to `IlluminationDto(AmbientLevel, ColorTemperatureK, DominantSourceId)`. `Bounds` projects to `BoundsRectDto(X, Y, Width, Height)`.

For `lightSources[]`: iterate entities tagged with `LightSourceTag`. Read `LightSourceComponent` (id, kind, state, intensity, colorTemperatureK, position, roomId) → `LightSourceDto`. `Position` uses the existing `PositionStateDto` shape.

For `lightApertures[]`: iterate entities tagged with `LightApertureTag`. Read `LightApertureComponent` → `LightApertureDto`.

For `clock.sun`: read `SunStateService.Current` (a `SunStateRecord`) and project to `SunStateDto(AzimuthDeg, ElevationDeg, DayPhase)`.

### Determinism

Sort each list by id ascending before projection. The same seed must produce the same wire output bit-for-bit.

### Projector signature change

The `Project` method currently takes `(SimulationSnapshot, EntityManager?, DateTimeOffset, long, int, string)`. It may need an additional input — `SunStateService` — since the snapshot doesn't carry sun state. Either inject it as an optional parameter (matching the existing `EntityManager?` pattern) or have the Sonnet check whether `SimulationSnapshot` already exposes sun state from WP-1.2.A's wiring (if it does, use that path).

If the `Project` method's signature changes, callers in `Warden.Orchestrator` and `ECSCli` need a small update. That's expected — schema-bump packets routinely touch the call sites. Keep changes minimal.

### The version-stamp ladder

The projector's `SchemaVersion` constant has been:
- WP-1.0.A through WP-1.4.A: `"0.1.0"`
- WP-1.4.B: `"0.2.1"`
- This packet: `"0.3.0"`

The schema's `schemaVersion` enum (set by WP-1.0.B) accepts `["0.1.0", "0.2.1", "0.3.0"]`. After this packet, the projector emits the highest version. Older v0.1 / v0.2.1 consumers ignore unknown fields per the additive-compatibility rule.

### Test updates

`TelemetryProjectorTests` currently asserts `SchemaVersion == "0.2.1"` (set by WP-1.4.B). Update to `"0.3.0"`. Add positive-case tests:

- A snapshot with two rooms, three light sources, one aperture, and a noon sun state produces a populated wire-format with all four fields. The output validates clean against the v0.3 schema.
- Sorting determinism: two projections of the same snapshot produce byte-identical bytes.
- Empty arrays: a snapshot with no rooms produces `rooms = []` (or null/absent — match what existing fields do for empty cases; the schema treats both as valid).

### When the engine has no rooms or lights

If the simulation hasn't been bootstrapped with any rooms/sources/apertures (e.g., a unit-test scenario with bare entities), the projected arrays are empty/absent and the schema still validates. The projector does not synthesize rooms.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Telemetry/TelemetryProjector.cs` (modified) | (1) Bump `SchemaVersion = "0.3.0"`. (2) Iterate `RoomTag` entities, project to `IReadOnlyList<RoomDto>`, sorted by id. (3) Iterate `LightSourceTag` entities, project to `IReadOnlyList<LightSourceDto>`, sorted by id. (4) Iterate `LightApertureTag` entities, project to `IReadOnlyList<LightApertureDto>`, sorted by id. (5) Read `SunStateService.Current` and project to `SunStateDto` on `clock.sun`. (6) If the `Project` method needs the `SunStateService`, add it as an optional parameter following the `EntityManager?` pattern. |
| code | `Warden.Telemetry.Tests/TelemetryProjectorTests.cs` (modified) | Update `SchemaVersion` assertions from `"0.2.1"` to `"0.3.0"`. Add: rooms-populated test, light-sources-populated test, light-apertures-populated test, sun-state-populated test, sort-determinism test, schema-validation test against v0.3. |
| code | `Warden.Orchestrator/*` (modified, only if signature changes) | If `Project`'s signature changes, update call sites. Likely one or two lines. |
| code | `ECSCli/Ai/AiSnapshotCommand.cs`, `AiStreamCommand.cs` (modified, only if signature changes) | Same — call site updates only. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-1.2.B.md` | Completion note. Standard template. Enumerate (a) which fields are now projected, (b) what's deferred (facing, narrative, position.roomId), (c) any signature changes and where call sites updated. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | Projector emits `SchemaVersion = "0.3.0"`. | unit-test |
| AT-02 | A snapshot with two `RoomTag` entities produces a `rooms[]` array of length 2 with correct fields. | unit-test |
| AT-03 | A snapshot with three `LightSourceTag` entities produces a `lightSources[]` array of length 3 with correct fields including state and color temperature. | unit-test |
| AT-04 | A snapshot with one `LightApertureTag` entity produces a `lightApertures[]` array of length 1 with the correct facing direction and area. | unit-test |
| AT-05 | A snapshot at noon (`circadianFactor = 0.5`) produces `clock.sun` with elevation near +90°, azimuth near 180°, and `dayPhase = afternoon`. | unit-test |
| AT-06 | A snapshot at midnight (`circadianFactor = 0.0`) produces `clock.sun` with negative elevation and `dayPhase = night`. | unit-test |
| AT-07 | A snapshot with no rooms produces an empty or absent `rooms[]` (whichever the schema and existing convention prefer; consistent with how empty `entities[]` is currently handled). | unit-test |
| AT-08 | The output validates clean against the v0.3 schema using `SchemaValidator`. | unit-test |
| AT-09 | Two projections of the same snapshot produce byte-identical JSON output. | unit-test |
| AT-10 | Rooms, light sources, and light apertures are emitted sorted by id ascending. | unit-test |
| AT-11 | All other existing `TelemetryProjectorTests` continue to pass with their updates. | build + unit-test |
| AT-12 | `Warden.Contracts.Tests` all pass — DTOs unchanged. | build + unit-test |
| AT-13 | `Warden.Orchestrator.Tests` and `ECSCli.Tests` all pass — call-site updates compile and run. | build + unit-test |
| AT-14 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-15 | `dotnet test ECSSimulation.sln` — every existing test stays green; updated tests pass. | build |

---

## Followups (not in scope)

- v0.3.x patch: add optional `entities[].position.roomId` to the wire format; populate from `EntityRoomMembership`. Eliminates the per-Haiku-tick room-membership recomputation.
- Project facing direction (when a future schema bump reserves it).
- Project narrative-event candidates onto the world-state telemetry (alternative: keep them in the separate stream channel; current architecture is the latter and probably correct).
- Per-tick projection caching: when the projector becomes a hot path, cache the room/source/aperture lists across ticks if no entities changed. Premature; revisit when profiling demands it.
