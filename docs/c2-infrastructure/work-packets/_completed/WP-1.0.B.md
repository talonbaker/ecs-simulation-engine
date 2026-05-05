# WP-1.0.B — schema-v03-spatial-additions — Completion Note

**Executed by:** sonnet-4.6
**Branch:** ecs-p1-10B
**Started:** 2026-04-25T00:00:00Z
**Ended:** 2026-04-25T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Landed v0.3.0 on `world-state.schema.json` — the first move on the spatial axis. Four surfaces added, all optional, all `additionalProperties: false`:

1. **`rooms[]`** (maxItems: 64) — room entities with id, name, category (16-value enum), floor (4-value enum), boundsRect (required, integers, width/height minimum 1), and illumination (required, with optional `dominantSourceId`).
2. **`lightSources[]`** (maxItems: 256) — interior sources with kind (10-value enum), state (`on | off | flickering | dying`), intensity, colorTemperatureK, tilePoint position, roomId.
3. **`lightApertures[]`** (maxItems: 64) — windows with tilePoint position, roomId, facing (5-value enum), areaSqTiles (0.5–64).
4. **`clock.sun`** — optional sun sub-object with azimuthDeg (0–360), elevationDeg (−90–90), dayPhase (6-value enum).

`schemaVersion` enum is now `["0.1.0", "0.2.1", "0.3.0"]`. v0.1 and v0.2.1 samples continue to validate clean (additive compatibility across two minor bumps verified by tests).

`WorldStateReferentialChecker` gained four new checks: light-source room resolution, aperture room resolution, dominant-source resolution, and duplicate-room-id detection. Overlapping bounds are explicitly allowed.

**Key judgement call:** `room-membership-on-position` (`entities[].position.roomId`) was explicitly deferred per the Non-goals — room membership is derivable from position + boundsRect, and adding it to the wire now would require engine-side population that is Phase 1.1 work.

`SCHEMA-ROADMAP.md` rewritten: v0.3 is now spatial, v0.4 is chronicle, v0.5 is character definitions, v0.6 is a placeholder.


## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|

| AT-01 | OK | `schemaVersion` enum `["0.1.0", "0.2.1", "0.3.0"]`; all new surfaces optional with `maxItems` and `min`/`max` on every numeric field. Verified by build (0 warnings) and schema inspection. |
| AT-02 | OK | `WorldState_V01SampleRoundTripsUnderV03Schema` and existing `WorldState_V01SampleRoundTripsUnderV021Schema` both pass. |
| AT-03 | OK | `WorldState_V021SampleRoundTripsUnderV03Schema` passes. |
| AT-04 | OK | `WorldState_V03SampleRoundTrips` passes full round-trip. |
| AT-05 | OK | `WorldState_V03_LightSourceMissingRoom_RejectedByReferentialChecker` passes with exact reason `"light-source-room-missing"`. |
| AT-06 | OK | `WorldState_V03_ApertureMissingRoom_RejectedByReferentialChecker` passes with exact reason `"aperture-room-missing"`. |
| AT-07 | OK | `WorldState_V03_DominantSourceMissing_RejectedByReferentialChecker` passes with exact reason `"dominant-source-missing"`. |
| AT-08 | OK | `WorldState_V03_DuplicateRoomId_RejectedByReferentialChecker` passes with exact reason `"duplicate-room-id"`. |
| AT-09 | OK | `WorldState_V03_BoundsRectWidthZero_FailsMinimum` passes — width=0 rejected by `minimum: 1`. |
| AT-10 | OK | `WorldState_V03_SunElevationDegTooHigh_FailsMaximum` passes — elevationDeg=91 rejected by `maximum: 90`. |
| AT-11 | OK | `WorldState_V03_AbsentSun_ValidatesClean` passes — no `clock.sun` is valid. |
| AT-12 | OK | `WorldState_V03_LightSourceInvalidState_FailsEnum` passes — `"burned-out"` rejected by enum. |
| AT-13 | OK | `WorldState_V03_OverlappingRoomBounds_ValidatesClean` passes — overlapping bounds pass both schema and referential checker. |
| AT-14 | OK | All 24 `Warden.Telemetry.Tests` pass. Projector still emits `SchemaVersion = "0.1.0"`; new arrays absent from output. |
| AT-15 | OK | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors. |
| AT-16 | OK | `dotnet test ECSSimulation.sln` — 400 passed, 0 failed across all test projects. |

## Files added

```
Warden.Contracts/Telemetry/RoomDto.cs
Warden.Contracts/Telemetry/LightSourceDto.cs
Warden.Contracts/Telemetry/LightApertureDto.cs
Warden.Contracts/Telemetry/SunStateDto.cs
Warden.Contracts.Tests/Samples/world-state-v030.json
docs/c2-infrastructure/work-packets/_completed/WP-1.0.B.md
```

## Files modified

```
docs/c2-infrastructure/schemas/world-state.schema.json      — canonical schema bumped to v0.3.0
Warden.Contracts/SchemaValidation/world-state.schema.json   — embedded resource mirror (matches canonical)
Warden.Contracts/Telemetry/WorldStateDto.cs                 — added Rooms/LightSources/LightApertures to WorldStateDto; Sun to ClockStateDto; default SchemaVersion = "0.3.0"
Warden.Contracts/SchemaValidation/Schema.cs                 — SchemaVersions.WorldState = "0.3.0"
Warden.Contracts/SchemaValidation/WorldStateReferentialChecker.cs — added four spatial referential checks
Warden.Contracts.Tests/SchemaRoundTripTests.cs              — added AT-02 through AT-13 (v0.3 tests); updated MakeMinimalWorldState helper
docs/c2-infrastructure/SCHEMA-ROADMAP.md                    — §v0.3 rewritten as spatial pillar; chronicle → v0.4; characters → v0.5; v0.6 placeholder added
```

## Diff stats

13 files changed (6 added counting completion note, 7 modified).

## v0.5-shaped fields promoted to v0.3

The original SCHEMA-ROADMAP §v0.5 described a basic `rooms[]` array with `id`, `name`, `boundsPolygon/boundsRect`, and `category`. This packet promotes and expands that surface to v0.3 with:

- **Promoted:** `rooms[]` with `id`, `name`, `category`, `boundsRect` (rect only — polygons deferred).
- **Added beyond v0.5 draft:** `floor` enum, `illumination` object (required on room), `lightSources[]`, `lightApertures[]`, and `clock.sun` — all driven by the aesthetic bible's lighting-as-priority-1 requirement.
- **Deferred from v0.5 draft:** `entities[].position.roomId` explicit field. Room membership is derivable from position + boundsRect; adding it to the wire now would require the engine to populate it (Phase 1.1 work). The spatial engine packet may add it as a v0.3.x patch.

## schemaVersion enum state

Three accepted versions: `["0.1.0", "0.2.1", "0.3.0"]`. `"0.2.0"` was collapsed into `"0.2.1"` by WP-1.0.A.1 and is not re-added. v0.1 and v0.2.1 documents validate clean under the v0.3 schema (all new fields optional).

## Deliberate variances from packet spec

1. **`TilePointDto` defined in `SunStateDto.cs` rather than as its own file.** The packet said "define a small `PointDto`." The type is named `TilePointDto` to be self-describing (tile-unit integers, not float world coordinates) and lives in `SunStateDto.cs` alongside the only other v0.3-only shared type. Both `LightSourceDto` and `LightApertureDto` reference it from there.

2. **`MakeMinimalWorldState` default `SchemaVersion` updated to `"0.3.0"`.** The helper was updated to stamp v0.3.0 by default (tests that previously passed `"0.2.1"` strings in inline JSON continue to do so explicitly). This makes new tests cleaner without breaking old ones.

## Followups

- Spatial-engine packet (Phase 1.1): `Room` as an `APIFramework` entity, spatial index, proximity events, telemetry projection populating `rooms[]`.
- Lighting-engine packet (Phase 1.2): sun-position system, light-source state systems, projection populating `lightSources[]`, `lightApertures[]`, `clock.sun`.
- `entities[].position.roomId` explicit field: may be added as a v0.3.x patch once the spatial engine populates it.
- Polygon room bounds (v0.6+): rect is sufficient for the early-2000s grid-aligned office; L-shaped spaces would need `boundsPolygon`.
- Auto-sync canonical schema → embedded resource (carry-over from WP-1.0.A; both files still updated manually).
- World-bootstrap packet (Phase 1.7): `world-definition.json` loader instantiates rooms, sources, and apertures at boot from named anchors in the world bible.
