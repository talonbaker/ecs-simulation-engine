# WP-1.7.A — world-bootstrap-definition-loader — Completion Note

**Executed by:** sonnet-01
**Branch:** feat/wp-1.7.A
**Started:** 2026-04-25T00:00:00Z
**Ended:** 2026-04-25T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Implemented the data-driven world-spawn path from a JSON world-definition file. The schema (`world-definition.schema.json`) follows project discipline: every object `additionalProperties: false`, every array bounded with `maxItems`, every numeric with `minimum`/`maximum`. The loader validates before deserializing (fail-closed per SRD §4.1) via the existing `SchemaValidator` infrastructure, then fans out to existing `EntityTemplates` factory calls for rooms, light sources, and apertures. NPC slots and anchor objects required new `NpcSlotTag`/`AnchorObjectTag` marker tags and corresponding component structs.

Judgement calls: (1) `archetypeHint` is deliberately unconstrained in the schema per AT-07 — the cast generator (WP-1.8.A) owns archetype validation, not the world schema. (2) `NoteComponent.Notes` is `IReadOnlyList<string>?` nullable because structs zero-initialize and calling code must null-check at load boundaries. (3) `APIFramework.csproj` gained a `ProjectReference` to `Warden.Contracts` so `WorldDefinitionLoader` can use `SchemaValidator` and `JsonOptions.Wire`; this is an intentional new intra-solution dependency. (4) Anchor-object tile positions are computed from room-center using a `roomBoundsMap` tracked during room iteration — avoids a second entity-manager pass.

The starter `office-starter.json` covers the 3-floor office bible (8 rooms, 8 light sources, 2 apertures, 6 NPC slots, 4 anchor objects, seed 19990101).

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | ✓ | Schema: `additionalProperties: false` on all 9 object types; all 6 arrays have `maxItems`; all numeric fields have `minimum`/`maximum`. Verified by `WorldDefinitionSchemaTests.Schema_HasNoUnsupportedKeywords`. |
| AT-02 | ✓ | `office-starter.json` validates clean in `WorldDefinitionSchemaTests.StarterFile_ValidatesClean`. |
| AT-03 | ✓ | Starter produces 8 rooms (≥6), 8 sources (≥8), 2 apertures (≥2), 6 NPC slots (≥5). Both `LoadFromFile_StarterJson_ProducesMinimumEntityCounts` and entity-manager tag-count variant pass. |
| AT-04 | ✓ | `LoadFromFile_BreakroomEntity_HasCorrectRoomComponentFields` validates all 10 fields; `LoadFromFile_ConferenceRoom_HasCorrectFloor` validates top-floor mapping. |
| AT-05 | ✓ | `LoadFromFile_BreakroomEntity_HasNamedAnchorComponent_WithCorrectTag` checks tag, smellTag, non-empty description. `LoadFromFile_BreakroomEntity_HasNoteComponent_WithExpectedNotes` checks ≥2 notes and "PLEASE LABEL" content. `LoadFromFile_CubicleGridWest_HasNoNamedAnchor` confirms negative case. |
| AT-06 | ✓ | Missing `schemaVersion` → `WorldDefinitionInvalidException` with "schemaVersion" in `ValidationErrors`. Negative seed → exception with "seed" or "minimum" in errors. Both cases tested. |
| AT-07 | ✓ | `WorldDefinitionSchemaTests.FreeArchetypeHint_ValidatesClean` passes arbitrary string `"not-a-real-archetype"` through schema validation without error. |
| AT-08 | ✓ | `LoadFromFile_TwoRunsSameSeed_ProduceIdenticalRoomComponents` compares all room fields across two independent loads with seed 42. `LoadFromFile_SeedIsPreservedInLoadResult` confirms `SeedUsed == 19990101`. |
| AT-09 | ✓ | `LoaderIntegrationTests.WorldDefinitionBootstrap_Runs100TicksWithoutError` ticks the full system pipeline 100 times with the starter file; no invariant violations or exceptions. |
| AT-10 | ✓ | `LoaderIntegrationTests.NoWorldDefinition_UsesSpawnWorldFallback` confirms `WorldLoadResult == null` and entity manager has humans via `SpawnWorld` path. |
| AT-11 | ✓ | 650 passing (17 Anthropic + 61 Contracts + 31 Telemetry + 402 APIFramework + 18 ECSCli + 121 Orchestrator), 0 failures. |
| AT-12 | ✓ | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors. |
| AT-13 | ✓ | `dotnet test ECSSimulation.sln` — 650 passed, 0 failed, 0 skipped. |

## Files added

```
docs/c2-infrastructure/schemas/world-definition.schema.json
Warden.Contracts/SchemaValidation/world-definition.schema.json
APIFramework/Bootstrap/WorldDefinitionDto.cs
APIFramework/Bootstrap/WorldDefinitionInvalidException.cs
APIFramework/Bootstrap/LoadResult.cs
APIFramework/Bootstrap/WorldDefinitionLoader.cs
APIFramework/Components/NamedAnchorComponent.cs
APIFramework/Components/NoteComponent.cs
APIFramework/Components/NpcSlotComponent.cs
APIFramework/Components/AnchorObjectComponent.cs
docs/c2-content/world-definitions/office-starter.json
Warden.Contracts.Tests/SchemaValidation/WorldDefinitionSchemaTests.cs
APIFramework.Tests/Bootstrap/WorldDefinitionLoaderTests.cs
APIFramework.Tests/Bootstrap/LoaderIntegrationTests.cs
docs/c2-infrastructure/work-packets/_completed/WP-1.7.A.md
```

## Files modified

```
Warden.Contracts/SchemaValidation/Schema.cs           — Added WorldDefinition to Schema enum + SchemaVersions constant
Warden.Contracts/SchemaValidation/SchemaValidator.cs  — Added Schema.WorldDefinition case in SchemaResourceName switch
APIFramework/APIFramework.csproj                       — Added ProjectReference to Warden.Contracts
APIFramework/Components/Tags.cs                        — Added NpcSlotTag, AnchorObjectTag marker structs
APIFramework/Components/EntityTemplates.cs             — Added WorldObject(…) factory
APIFramework/Core/SimulationBootstrapper.cs            — Added worldDefinitionPath param; WorldLoadResult property
ECSCli/Ai/AiStreamCommand.cs                           — Added --world-definition <path> option
ECSCli/Ai/AiReplayCommand.cs                           — Added --world-definition <path> option
```

## Diff stats

22 files changed, 1816 insertions(+), 17 deletions(-)

(From `git diff --stat --cached HEAD` before committing WP-1.7.A changes.)

## Followups

- WP-1.8.A cast generator should validate `archetypeHint` strings against its archetype registry at spawn time (schema intentionally defers this).
- `objectsAtAnchors` tile-position is currently room-center; a future packet could support explicit `tileX`/`tileY` fields in the schema for precise anchor placement.
- `SimulationBootstrapper` wires `--world-definition` into `AiStreamCommand` and `AiReplayCommand` only; `AiDescribeCommand` (if it also bootstraps) may need the same option.
- Chronicle / memory-event seeding from world-definition deferred (not in scope for this packet).
