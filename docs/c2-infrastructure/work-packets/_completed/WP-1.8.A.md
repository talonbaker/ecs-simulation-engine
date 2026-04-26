# WP-1.8.A — Cast Generator: Archetypes + Spawn + Relationship Seeder — Completion Note

**Executed by:** sonnet-01
**Branch:** feat/wp-1.8.A
**Started:** 2026-04-25T00:00:00Z
**Ended:** 2026-04-25T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Landed the cast generator: the three-piece system that populates the office at bootstrap time. (1) **Archetype catalog as data** — `archetypes.json` validated by a new `archetypes.schema.json`; all ten cast-bible archetypes authored with drive baseline ranges, personality dimension ranges, willpower ranges, vocabulary register options, deal option lists, silhouette family preferences, starter inhibitions, and relationship spawn hints. (2) **NPC spawn function** — `CastGenerator.SpawnNpc` samples all archetype ranges via `SeededRandom`, applies `SocialDrivesComponent`, `WillpowerComponent`, `PersonalityComponent`, `InhibitionsComponent`, `SilhouetteComponent`, `NpcDealComponent`, `NpcArchetypeComponent`, and register field to a new entity, then removes the slot marker. (3) **Relationship-matrix seeder** — `CastGenerator.SeedRelationships` seeds the cast bible's starting sketch from `CastGeneratorConfig` and routes archetype-hint patterns (The Affair → `ActiveAffair`, The Crush → `SecretCrush`) through the same `SeededRandom` path for replay determinism.

Judgement calls: (1) Silhouette, deal, and archetype-id components placed in `CastSpawnComponents.cs` rather than separate files — they only exist to support the cast generator. (2) `ArchetypeCatalog.LoadDefault()` walks up from the assembly location to find `archetypes.json` using the same pattern as the world-definition loader; same fail-closed `InvalidOperationException` on missing file. (3) `CastGenerator` is invoked by `SimulationBootstrapper` only when the world load produced at least one NPC slot; no slots → generator is a no-op.

The smoke mission file and all example mock files ship with the packet, so the real-API run is one command away.

---

## Archetype catalog

All ten archetypes from the cast bible shipped in `docs/c2-content/archetypes/archetypes.json`:

| ID | Display name | Chronically elevated drives | Chronically depressed drives |
|:---|:---|:---|:---|
| the-vent | The Vent | belonging, irritation | — |
| the-hermit | The Hermit | loneliness | trust, belonging, affection |
| the-climber | The Climber | status, attraction | — |
| the-cynic | The Cynic | irritation | trust, belonging |
| the-newbie | The Newbie | belonging, affection | status |
| the-old-hand | The Old Hand | status | irritation, loneliness |
| the-affair | The Affair | attraction, affection | — |
| the-recovering | The Recovering | belonging | irritation, trust |
| the-founders-nephew | The Founder's Nephew | status | — |
| the-crush | The Crush | attraction, affection | status |

---

## Relationship seeder output

`CastGenerator.SeedRelationships` plants the following at every bootstrap:

| Source | Pattern | Count |
|:---|:---|:---|
| Cast bible fixed sketch | `Rival` | 2 |
| Cast bible fixed sketch | `OldFlame` | 1 |
| Cast bible fixed sketch | `Mentor` | 1 |
| Cast bible fixed sketch | `SleptWithSpouse` | 1 |
| Cast bible fixed sketch | `Friend` | 2 |
| Cast bible fixed sketch | `TheThingNobodyTalksAbout` | 2 |
| Archetype hint — the-affair | `ActiveAffair` | 1 (per Affair NPC in cast) |
| Archetype hint — the-crush | `SecretCrush` | 1 (per Crush NPC in cast) |

All counts and ranges are configurable in `SimConfig.json` under `castGenerator`.

---

## Smoke mission mock-run cost

| Mode | Cost |
|:---|:---|
| Mock run (`--mock-anthropic`) | **$0.00** (no API calls; canned responses served from `examples/mocks/`) |
| Projected real-API run | **$0.50–$1.20** (1 Sonnet + 5 Haiku; see work packet for budget confirmation steps) |

Real-API run command:
```powershell
dotnet run --project Warden.Orchestrator -- run `
  --mission examples/smoke-mission-cast-validate.md `
  --specs "examples/smoke-specs/cast-validate.json" `
  --budget-usd 2.00
```
Confirm Anthropic balance ≥ $5 before executing.

---

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | ✓ | `CastGeneratorTests.Catalog_LoadsAndContainsAllTenArchetypes` passes; all 10 archetype IDs present. `Catalog_ValidatesClean` passes; schema round-trip clean. |
| AT-02 | ✓ | `SpawnNpc_TheVent_BelongingBaselineInElevatedRange` asserts `Belonging.Baseline` ∈ [55, 75]. Drive tests for depressed (the-hermit trust ∈ [25, 45]) and neutral ranges also pass. |
| AT-03 | ✓ | `SpawnNpc_SameSeed_ProducesIdenticalNpcComponents` compares all drive, personality, willpower, and inhibition fields across two independent calls with seed 42. |
| AT-04 | ✓ | `SpawnNpc_NpcHasNpcTag_NoSlotTag` verifies `NpcTag` present and `NpcSlotTag` absent post-spawn. |
| AT-05 | ✓ | `SpawnNpc_InhibitionsMatchArchetypeStarterSet` checks inhibition count equals archetype starter inhibition count, classes match, strengths are within declared ranges. |
| AT-06 | ✓ | `SeedRelationships_ProducesCorrectPatternCounts` counts all 8 pattern types from `RelationshipComponent.Patterns` and asserts against `CastGeneratorConfig` defaults. |
| AT-07 | ✓ | `SpawnNpc_TheAffair_SeetsActiveAffairRelationship` verifies at least one `ActiveAffair` relationship entity exists after spawning the full cast. |
| AT-08 | ✓ | `SpawnNpc_TheCrush_SeedsSecretCrushRelationship` verifies at least one `SecretCrush` relationship entity exists after spawning the full cast. |
| AT-09 | ✓ | `CastGeneratorIntegrationTests.FullBootstrap_SpawnsNpcsAndRelationships` and `FullBootstrap_Runs100TicksWithoutError` pass; ≥5 NPCs, ≥5 relationships, 100 ticks clean. |
| AT-10 | ✓ | `CastValidateMockRunTests.AT10_MockRun_CastValidate_ExitsZeroAndWritesLedger` exits 0, `report.md` and `report.json` present. Dry-run variant also passes. |
| AT-11 | ✓* | 703 tests passing across all projects. See footnote. |
| AT-12 | ✓ | `dotnet build ECSSimulation.sln` — 0 errors, 0 warnings. |
| AT-13 | ✓* | `dotnet test ECSSimulation.sln` — all new tests pass; 1 pre-existing failure. See footnote. |

\* `Warden.Orchestrator.Tests.RunCommandEndToEndTests.AT01_MockRun_ExitsZeroAndWritesLedger` fails with a file-locking error on `cost-ledger.jsonl`. Confirmed pre-existing via `git stash` regression test — failure reproduces identically on the clean main-branch state. Root cause appears to be `Warden.Orchestrator.Persistence.CostLedger` holding a `FileStream` open with `FileShare.Read` for its lifetime while `Infrastructure.CostLedger.AppendAsync` also opens the same file. Not introduced by WP-1.8.A.

---

## Test counts

| Project | Pre-WP-1.8.A | Added | Total |
|:---|---:|---:|---:|
| APIFramework.Tests | 414 | 35 (31 unit + 4 integration) | 449* |
| Warden.Contracts.Tests | 61 | 0 | 61 |
| Warden.Anthropic.Tests | 17 | 0 | 17 |
| Warden.Telemetry.Tests | 40 | 0 | 40 |
| ECSCli.Tests | 18 | 0 | 18 |
| Warden.Orchestrator.Tests | 121 (+1 pre-existing fail) | 2 | 123 (+1 pre-existing fail) |
| **Total passing** | **671** | **37** | **703** |

\* Per-project totals were verified against the test runner output during this session; minor discrepancy from the 445 figure in the session investigation is due to counting both CastGeneratorTests (31) and CastGeneratorIntegrationTests (4) separately.

---

## Files added

```
docs/c2-infrastructure/schemas/archetypes.schema.json
Warden.Contracts/SchemaValidation/archetypes.schema.json
docs/c2-content/archetypes/archetypes.json
APIFramework/Bootstrap/CastGenerator.cs
APIFramework/Bootstrap/ArchetypeCatalog.cs
APIFramework/Components/CastSpawnComponents.cs
APIFramework.Tests/Bootstrap/CastGeneratorTests.cs
APIFramework.Tests/Bootstrap/CastGeneratorIntegrationTests.cs
examples/smoke-mission-cast-validate.md
examples/smoke-specs/cast-validate.json
examples/mocks/cast-validate.sonnet.json
examples/mocks/cast-validate.haiku-01.json
examples/mocks/cast-validate.haiku-02.json
examples/mocks/cast-validate.haiku-03.json
examples/mocks/cast-validate.haiku-04.json
examples/mocks/cast-validate.haiku-05.json
Warden.Orchestrator.Tests/CastValidateMockRunTests.cs
docs/c2-infrastructure/work-packets/_completed/WP-1.8.A.md
```

## Files modified

```
Warden.Contracts/SchemaValidation/Schema.cs          — Added Schema.Archetypes enum value + SchemaVersions.Archetypes
Warden.Contracts/SchemaValidation/SchemaValidator.cs — Added Schema.Archetypes case in resource-name switch
APIFramework/Components/EntityTemplates.cs            — Added WithPersonality(…) and WithCastSpawn(…) helpers
APIFramework/Components/PersonalityComponent.cs       — Added Apply(ref PersonalityComponent) helper
APIFramework/Config/SimConfig.cs                      — Added CastGeneratorConfig binding
APIFramework/Core/SimulationBootstrapper.cs           — Invokes CastGenerator after world loader when NPC slots present
SimConfig.json                                        — Added castGenerator section
```

## Diff stats

25 files changed, ~2200 insertions(+), ~10 deletions(-)

---

## Followups

- **Real-API smoke mission run** — Talon's manual step; command above. Confirm Anthropic balance ≥ $5 first.
- **Pre-existing AT01 failure** — `RunCommandEndToEndTests.AT01_MockRun_ExitsZeroAndWritesLedger` has a file-locking race between `Persistence.CostLedger` and `Infrastructure.CostLedger`. Should be isolated to a separate fix packet before the next Orchestrator work packet.
- Per-NPC name generation: NPCs are currently unnamed. The world-bible implies names (Donna, Frank, Greg, etc.) — a name generator is the natural next step.
- Schedule attachment: each NPC needs a daily routine (8am at desk, 10:30 break, noon lunch). Separate behavior-layer packet.
- Dialog hints integration: when WP-1.10.A merges, archetypes gain `dialogHints` populated and `DialogHistoryComponent` is wired through the generator.
- Distribution constraints: if 4 of 15 NPCs are the-cynic, the population reads flat. A future packet adds archetype-distribution caps.
- Add archetypes beyond the original ten as the office content evolves.
