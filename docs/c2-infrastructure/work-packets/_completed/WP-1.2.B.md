# WP-1.2.B — projector-spatial-state-population — Completion Note

**Executed by:** sonnet-4.6
**Branch:** feat/wp-1.2.B
**Started:** 2026-04-25T00:00:00Z
**Ended:** 2026-04-25T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Closed the v0.3 spatial wire-format loop. Four fields move from absent to populated: `rooms[]`, `lightSources[]`, `lightApertures[]`, and `clock.sun`. All four project field-by-field from the corresponding engine components and the `SunStateService` singleton. Each list is sorted by entity Id (Guid) ascending to guarantee deterministic output given the same seed.

**Signature change:** Added `SunStateService? sunStateService = null` as an optional 7th parameter to the primary `Project` overload. The 5-parameter overload (no entity manager) is unchanged. Updated all three `ECSCli` call sites (`AiSnapshotCommand`, `AiStreamCommand`, `AiReplayCommand`) to pass `sim.SunState`, so real snapshots now include sun state. Call sites that don't pass the parameter fall back to `clock.sun = null`, which is schema-valid.

**SchemaVersion** bumped from `"0.2.1"` to `"0.3.0"` in `TelemetryProjector` and the corresponding assertion updated across all test projects.

**Deferred (as specified by packet):** facing direction, narrative-event candidates, `entities[].position.roomId` (via `EntityRoomMembership`).

All enum casts use the integer-mirror guarantee that every `APIFramework.Components` enum shares with its `Warden.Contracts.Telemetry` counterpart, so no lookup tables are needed.

---

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | ✓ | `AT01_SchemaVersion_Is030` asserts `dto.SchemaVersion == "0.3.0"`. |
| AT-02 | ✓ | `AT02_TwoRoomEntities_ProjectsRoomsArrayOfLength2` — verifies id, name, category, floor, bounds X/Y/W/H, illumination ambient/colorTemp/dominantSourceId. |
| AT-03 | ✓ | `AT03_ThreeLightSourceEntities_ProjectsLightSourcesArrayOfLength3` — verifies kind, state, intensity, colorTemperatureK, position, roomId for all three. |
| AT-04 | ✓ | `AT04_OneLightApertureEntity_ProjectsLightAperturesArrayOfLength1` — verifies id, position, roomId, facing (South), areaSqTiles. |
| AT-05 | ✓ | `AT05_NoonSunState_ProjectsCorrectAzimuthElevationDayPhase` — 180° azimuth, elevation > 0°, `DayPhase.Afternoon`. |
| AT-06 | ✓ | `AT06_MidnightSunState_ProjectsNegativeElevationAndNightPhase` — elevation < 0°, `DayPhase.Night`. |
| AT-07 | ✓ | `AT07_NoRoomEntities_RoomsIsNull` — empty entity manager → `dto.Rooms == null`. |
| AT-08 | ✓ | `AT08_FullSpatialSnapshot_ValidatesAgainstSchema` — one room, one source, one aperture, noon sun → `SchemaValidator.Validate` passes. |
| AT-09 | ✓ | `AT09_TwoProjectionsOfSameSnapshot_ProduceBytIdenticalJson` — byte-identical JSON for same inputs. |
| AT-10 | ✓ | `AT10_RoomsLightSourcesAndApertures_SortedByIdAscending` — two rooms, two sources, two apertures each confirm ascending entity-Guid order. |
| AT-11 | ✓ | All 30 pre-existing `TelemetryProjectorTests` pass; all 633 solution tests pass. |
| AT-12 | ✓ | `Warden.Contracts.Tests` — 50 passed, 0 failed. DTOs unchanged. |
| AT-13 | ✓ | `ECSCli.Tests` — 18 passed; `AiVerbTests` schema assertion updated to `"0.3.0"`. `Warden.Orchestrator.Tests` — 121 passed. |
| AT-14 | ✓ | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors. |
| AT-15 | ✓ | `dotnet test ECSSimulation.sln` — 633 passed, 0 failed. |

---

## Files added

```
docs/c2-infrastructure/work-packets/_completed/WP-1.2.B.md
```

## Files modified

```
Warden.Telemetry/TelemetryProjector.cs             — (1) SchemaVersion "0.3.0". (2) SunStateService? optional param. (3) ProjectClock takes sunService. (4) ProjectSunState helper. (5) ProjectRooms, ProjectLightSources, ProjectLightApertures. (6) New using aliases for spatial contract enums.
Warden.Telemetry.Tests/TelemetryProjectorTests.cs  — (1) AT01 renamed/updated to assert "0.3.0". (2) New tests AT-02 through AT-10 for spatial state. (3) New using aliases for engine+contract spatial enums.
ECSCli/Ai/AiSnapshotCommand.cs                     — Pass sim.SunState as sunStateService to Project.
ECSCli/Ai/AiStreamCommand.cs                       — Pass sim.SunState as sunStateService to Project.
ECSCli/Ai/AiReplayCommand.cs                       — Pass sim.SunState as sunStateService to Project.
ECSCli.Tests/AiVerbTests.cs                        — Update schemaVersion assertion from "0.2.1" to "0.3.0".
```

## Diff stats

This packet's changes touch 6 files modified, 1 file added (~300 insertions(+), ~50 deletions(-) for this packet's own delta on top of staging).

## Followups

- v0.3.x patch: add `entities[].position.roomId` via `EntityRoomMembership`; schema doesn't carry it yet.
- Project facing direction when a future schema bump reserves it.
- Per-tick projection caching: cache room/source/aperture lists when no entities changed (premature; profile first).
- Narrative-event candidates remain on the separate `ai narrative-stream` channel; no change needed here.
