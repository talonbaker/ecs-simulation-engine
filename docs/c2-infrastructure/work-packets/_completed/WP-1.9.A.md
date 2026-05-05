# WP-1.9.A — Persistent Chronicle: v0.4 Schema + Threshold Detector + Stain/BrokenItem — Completion Note

**Executed by:** sonnet-01
**Branch:** feat/wp-1.9.A
**Started:** 2026-04-25T00:00:00Z
**Ended:** 2026-04-25T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Landed the persistent-chronicle layer that makes the world-bible's "spill stays spilled" mechanic real.

Schema v0.4 bumps `schemaVersion` to `"0.4.0"` and adds a `chronicle[]` array (maxItems: 4096) with a `chronicleEntry` definition: `{id, kind, tick, participants[], location?, description, persistent, physicalManifestEntityId?}`. The `kind` enum has eleven values (`spilledSomething | brokenItem | publicArgument | ...`).

`PersistenceThresholdDetector` subscribes to `NarrativeEventBus`, buffers candidates per tick, and applies three threshold rules: (1) relationship-changing events with intensity delta ≥ 15 persist as `PublicArgument`; (2) irritation `DriveSpike` ≥ 70 with a non-NPC entity within 2 tiles spawns a `Stain` and persists as `SpilledSomething`; (3) the same event kind from ≥ 2 distinct NPCs in one tick persists as `PublicArgument`. `WillpowerLow` always discards; drives returned to baseline (|current − baseline| ≤ 5) discard.

`EntityTemplates.Stain/BrokenItem` add `StainTag`/`BrokenItemTag` entities. `InvariantSystem` gains a chronicle ↔ entity-tree agreement check (axiom 8.4). `TelemetryProjector` now projects chronicle entries and emits `schemaVersion: "0.4.0"`.

All 707 tests pass; build has zero warnings.

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | OK | `world-state.schema.json` has `"0.4.0"` in `schemaVersion` enum and `chronicle[]` with `maxItems: 4096`. Verified by `WorldState_V04_SchemaHas040EnumAndChronicleArray`. |
| AT-02 | OK | v0.3 sample round-trips under v0.4 schema. `WorldState_V03SampleRoundTripsUnderV04Schema` passes. |
| AT-03 | OK | v0.4 sample with 3 chronicle entries round-trips. `WorldState_V04SampleRoundTrips` passes. |
| AT-04 | OK | Relationship intensity delta ≥ 15 → chronicle entry. `RelationshipImpact_AboveThreshold_Persists` passes. |
| AT-05 | OK | Minor-effect candidate produces no entry. `RelationshipImpact_BelowThreshold_DoesNotPersist` and `WillpowerLow_NeverPersists` pass. |
| AT-06 | OK | High-irritation spike + nearby item → Stain entity + `SpilledSomething` chronicle entry. `HighIrritationSpike_NearbyItem_SpawnsStainAndChronicleEntry` passes. |
| AT-07 | OK | `BrokenItemTag` entity with missing `ChronicleEntryId` → invariant violation. `BrokenItemTag_MissingChronicleEntry_ProducesViolation` passes. |
| AT-08 | OK | Chronicle entry with missing `physicalManifestEntityId` → invariant violation. `ChronicleEntry_MissingPhysicalManifestEntity_ProducesViolation` passes. |
| AT-09 | OK | Ring buffer drops oldest on overflow. `ChronicleService_RingBuffer_DropsOldestOnOverflow` passes. |
| AT-10 | OK | Same seed → same chronicle entry IDs. `Determinism_SameSeed_ProducesSameEntryId` passes. |
| AT-11 | OK | `TelemetryProjector` emits `"0.4.0"` and projects `ChronicleService` entries. `AT01_SchemaVersion_Is040` and `AT11_ChronicleService_WithEntries_ProjectsToChronicleArray` pass. |
| AT-12 | OK | All existing `Warden.Telemetry.Tests` pass (44 total). |
| AT-13 | OK | All existing `APIFramework.Tests` pass (441 total). |
| AT-14 | OK | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors. |
| AT-15 | OK | `dotnet test ECSSimulation.sln` — 707 passed, 0 failed, 0 skipped. |

## Files added

```
docs/c2-infrastructure/schemas/world-state.schema.json     — v0.4 schema additions
Warden.Contracts/SchemaValidation/world-state.schema.json  — embedded mirror
Warden.Contracts/Telemetry/ChronicleEntryDto.cs            — DTO record
Warden.Contracts/Telemetry/ChronicleEventKind.cs           — contract-layer enum (11 values)
APIFramework/Components/StainComponent.cs                   — component + BreakageKind enum
APIFramework/Components/BrokenItemComponent.cs              — component
APIFramework/Systems/Chronicle/ChronicleEventKind.cs       — engine-layer enum
APIFramework/Systems/Chronicle/ChronicleEntry.cs           — engine record
APIFramework/Systems/Chronicle/ChronicleService.cs         — ring-buffer singleton
APIFramework/Systems/Chronicle/PersistenceThresholdDetector.cs
APIFramework/Systems/Chronicle/PhysicalManifestSpawner.cs
APIFramework.Tests/Systems/Chronicle/PersistenceThresholdDetectorTests.cs
APIFramework.Tests/Systems/Chronicle/InvariantTests.cs
APIFramework.Tests/Components/StainComponentTests.cs
APIFramework.Tests/Components/BrokenItemComponentTests.cs
Warden.Contracts.Tests/Samples/world-state-v040.json       — v0.4 sample (3 chronicle entries)
docs/c2-infrastructure/work-packets/_completed/WP-1.9.A.md
```

## Files modified

```
docs/c2-infrastructure/schemas/world-state.schema.json   — schemaVersion enum + chronicle[]
Warden.Contracts/SchemaValidation/Schema.cs              — WorldState = "0.4.0"
Warden.Contracts/Telemetry/WorldStateDto.cs              — Chronicle property
APIFramework/Components/Tags.cs                          — StainTag, BrokenItemTag
APIFramework/Components/EntityTemplates.cs               — Stain(), BrokenItem() factories
APIFramework/Config/SimConfig.cs                         — ChronicleConfig, ChronicleThresholdRulesConfig
APIFramework/Core/SimulationBootstrapper.cs              — Chronicle singleton; PersistenceThresholdDetector registration
APIFramework/Systems/InvariantSystem.cs                  — CheckChronicleIntegrity()
Warden.Telemetry/TelemetryProjector.cs                   — SchemaVersion = "0.4.0"; ProjectChronicle()
Warden.Telemetry.Tests/TelemetryProjectorTests.cs        — AT-01 updated; AT-11 chronicle tests added
Warden.Contracts.Tests/SchemaRoundTripTests.cs           — MakeMinimalWorldState default "0.4.0"; v0.4 tests
SimConfig.json                                           — chronicle section added
ECSCli.Tests/AiVerbTests.cs                              — schemaVersion assertion updated to "0.4.0"
docs/c2-infrastructure/SCHEMA-ROADMAP.md                 — v0.4 marked landed
```

## Diff stats

39 files changed, ~900 insertions(+), ~35 deletions(-)

(15 files modified via `git diff --stat HEAD`, 24 new files.)

## Chronicle event kinds (canonicalized)

The eleven kinds that can be recorded in the chronicle:

| Kind | Engine trigger |
|:---|:---|
| `spilledSomething` | Irritation DriveSpike ≥ 70 with a non-NPC entity within 2 tiles |
| `brokenItem` | Reserved — no engine trigger yet (future PhysicalManifestSpawner extension) |
| `publicArgument` | Relationship-changing candidate with intensity delta ≥ 15; or talk-about (≥ 2 NPCs same tick) |
| `publicHumiliation` | Reserved |
| `affairRevealed` | Reserved |
| `promotion` | Reserved |
| `firing` | Reserved |
| `kindnessInCrisis` | Reserved |
| `betrayal` | Reserved |
| `deathOrLeaving` | Reserved |
| `other` | Reserved |

## Physical manifestations

| Tag | Created by | Spawned when |
|:---|:---|:---|
| `StainTag` + `StainComponent` | `PhysicalManifestSpawner.SpawnStain()` | Irritation spike ≥ 70 + nearby item |
| `BrokenItemTag` + `BrokenItemComponent` | `PhysicalManifestSpawner.SpawnBrokenItem()` | Reserved — not triggered yet |

Both carry `ChronicleEntryId` for referential integrity, validated by `InvariantSystem.CheckChronicleIntegrity()`.

## Followups (not in scope)

- **Per-pair memory recording.** Reads narrative-event candidates involving two NPCs and writes to a per-pair memory ring buffer on the relationship entity. Axiom 8.3 "secondary channel." Phase-1.4 follow-up.
- **`initialChronicleEntries[]` on world-definition schema.** Bootstrap the world with pre-existing chronicle history. Small content-authoring follow-up.
- **`narrative-event-emit` command.** The schema roadmap reserved this for design-time content authoring. Deferred until the content workflow needs it.
- **`scope: "global"` guard removal.** The roadmap said v0.4 removes the `WorldStateReferentialChecker` guard. Not in the WP-1.9.A deliverables table; deferred.
- **More physical manifest kinds.** `BurnMark`, `TornPaper`, `MissingItem`. Extend `PhysicalManifestSpawner` when authored anchors expand.
- **Physical manifest decay.** Stain fades over time; broken item gets cleaned up. Behavior layer concern.
