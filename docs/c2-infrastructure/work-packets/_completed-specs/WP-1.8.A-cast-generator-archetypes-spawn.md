# WP-1.8.A — Cast Generator: Archetypes + Spawn + Relationship Seeder

**Tier:** Sonnet
**Depends on:** WP-1.4.A (social components — merged), WP-1.7.A (npcSlots in world definition — merged), WP-1.1.A (spatial — merged).
**Parallel-safe with:** WP-1.9.A (Chronicle), WP-1.10.A (Dialog implementation). Different file footprints.
**Timebox:** 120 minutes
**Budget:** $0.60

---

## Goal

Land the cast generator. Phase 1's payoff packet — the moment the office gets *populated*. Three pieces:

1. **Archetype catalog as data.** The ten archetypes from the cast bible (`The Vent`, `The Hermit`, `The Climber`, `The Cynic`, `The Newbie`, `The Old Hand`, `The Affair`, `The Recovering`, `The Founder's Nephew`, `The Crush`) become a JSON file `archetypes.json` validated by a new schema. Each archetype carries its drive baseline ranges, personality dimension ranges, vocabulary register, deal options, silhouette family, and starter inhibition set.

2. **NPC spawn function.** A new `CastGenerator.SpawnNpc(archetypeId, npcSlotEntity, seed)` reads the archetype, samples specific values from each range, applies them to a real entity in `EntityManager` — drives, willpower, personality, inhibitions, register. Replaces the marker `NpcSlotComponent` with full NPC state.

3. **Relationship-matrix seeder.** After all NPCs are spawned, `CastGenerator.SeedRelationships(seed)` instantiates the cast bible's starting sketch: most pairs neutral, a few rivalries, one old flame, one mentor/mentee, one slept-with-their-spouse, a couple "the thing nobody talks about." Pattern selections are seeded for replay determinism.

What this packet does **not** do: dispatch the smoke mission (the orchestrator run that validates the population). That's a separate operate-the-factory step Talon runs manually after this packet merges. The smoke mission file is shipped as part of this packet's deliverables, but the *run* is Talon's. The packet does include a smoke mission file (`mission-cast-validate.md`) and the matching mock-mode example specs (`examples/mocks/cast-validate.*.json`) so the orchestrator can run end-to-end in mock mode without API spend.

---

## Reference files

- `docs/c2-content/DRAFT-cast-bible.md` — **read first**. The archetype catalog, drive baseline conventions, relationship pattern library, starting-relationship sketch.
- `docs/c2-content/DRAFT-action-gating.md` — for how willpower and inhibitions feed each archetype.
- `docs/c2-content/DRAFT-world-bible.md` — the named anchors and the office context the cast inhabits.
- `docs/c2-content/world-definitions/office-starter.json` (from WP-1.7.A) — the source of `npcSlots[]`.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.4.A.md` — confirms social components.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.7.A.md` — confirms world loader and `NpcSlotComponent`.
- `APIFramework/Components/SocialDrivesComponent.cs`, `WillpowerComponent.cs`, `PersonalityComponent.cs`, `InhibitionsComponent.cs` — target shapes.
- `APIFramework/Components/NpcSlotComponent.cs` — the marker entity the spawn replaces.
- `APIFramework/Components/EntityTemplates.cs` — has `WithSocial(...)` helper from WP-1.4.A.
- `APIFramework/Core/SimulationBootstrapper.cs` — system + service registration site.
- `APIFramework/Core/SeededRandom.cs` — RNG source for spawn variation.
- `Warden.Contracts/JsonOptions.cs` — for JSON parsing.
- `examples/smoke-mission.md` — pattern reference for the new mission file.
- `docs/c2-infrastructure/RUNBOOK.md` §7 — operate-the-factory workflow Talon will use to run the smoke mission.

## Non-goals

- Do **not** dispatch the orchestrator. The smoke-mission run is Talon's manual step. This packet ships the mission file and example mocks so the run is one command away.
- Do **not** modify `Warden.Anthropic/`, `Warden.Orchestrator/`, or any infrastructure under `Warden.*` other than the `examples/` mock files. Those are Phase-0 stable; the cast generator runs at engine boot, not through the orchestrator.
- Do **not** populate the dialog history (`DialogHistoryComponent`). That's WP-1.10.A's territory; this packet only spawns NPCs with empty dialog state. If WP-1.10.A hasn't merged yet, the component may not exist — in that case, omit it; the generator only populates what's available.
- Do **not** modify `world-definition.schema.json` or the world loader. The generator consumes existing `npcSlots[]`; no schema change required.
- Do **not** add archetypes beyond the ten in the cast bible. New archetypes are easy to add in a follow-up; this packet ships exactly the bible's list.
- Do **not** generate dialog corpus content. Phrase corpus is WP-1.10.A.
- Do **not** add chronicle entries. Chronicle is WP-1.9.A.
- Do **not** write narrative-event candidates manually. Generator places NPCs; events emerge naturally from the engine's tick loop after spawn.
- Do **not** introduce a NuGet dependency.
- Do **not** use `System.Random`. `SeededRandom` only.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (Architectural axiom 8.1.)

---

## Design notes

### Archetype data file

`docs/c2-content/archetypes/archetypes.json`. Schema validated by `archetypes.schema.json` (new in this packet). Each archetype object:

- `id` — slug like `"the-vent"`, `"the-hermit"`.
- `displayName` — `"The Vent"`.
- `chronicallyElevatedDrives` — list of drive names with elevated baselines: `["belonging", "irritation"]`.
- `chronicallyDepressedDrives` — list with depressed baselines: `["trust"]` for The Hermit.
- `personalityRanges` — Big Five: each dimension has `[min, max]` integer pair in `-2..+2`. The Vent might be `{extraversion: [0, 2], neuroticism: [0, 2]}`.
- `vocabularyRegister` — one of the bible's six values; or a list if the archetype is flexible (The Affair has `["casual", "formal"]` because they code-switch).
- `willpowerBaselineRange` — `[min, max]` `0..100`.
- `dealOptions` — list of strings drawn from the cast bible's deal catalog. The generator picks one at spawn time.
- `silhouetteFamily` — preferences across the silhouette catalog (height, build, headwear). Each preference is a list; generator picks weighted-random.
- `starterInhibitions` — list of `{class, strengthRange, awareness}`. The Affair starts with `{class: "infidelity", strengthRange: [10, 30], awareness: "known"}` (low strength because they're failing it) and `{class: "vulnerability", strengthRange: [70, 90], awareness: "hidden"}`.
- `dialogHints` (optional, references WP-1.10.A if landed) — register and calcify-priority tags per the dialog bible.
- `relationshipSpawnHints` — for archetypes that imply relationships at spawn (The Affair, The Crush). Specifies the relationship pattern and target-archetype preferences.

The Sonnet authors all ten archetypes in `archetypes.json` from the cast bible's catalog. Drive baseline numerics are starting points; tuning happens later.

### Spawn function

`CastGenerator.SpawnNpc(string archetypeId, NpcSlotEntity slot, SeededRandom rng) : NpcEntity`:

1. Look up archetype by id.
2. For each drive in `chronicallyElevatedDrives`, sample a baseline in `[55, 75]`. For `chronicallyDepressedDrives`, sample in `[25, 45]`. Other drives sample in `[40, 60]`. `Current` starts equal to `Baseline` plus a small per-drive jitter.
3. Sample personality dimensions from `personalityRanges`. Dimensions not in the archetype's range default to `[-1, +1]` (averagely).
4. Sample willpower baseline from range; current starts equal.
5. For each `starterInhibition`, sample strength from range, set awareness from spec.
6. Pick vocabulary register from archetype (random from list if multiple).
7. Pick a `deal` from `dealOptions`.
8. Pick a silhouette by sampling each silhouette dimension.
9. Use the world-loader's `EntityTemplates.WithSocial(...)` and the new `WithPersonality(...)` to apply all of the above to a new entity, replacing the slot's marker components.
10. Add `NpcTag`. Remove `NpcSlotTag` and `NpcSlotComponent` from the slot entity (or destroy the slot and create a new entity at the slot's position — Sonnet picks the cleaner approach).

### Relationship seeder

`CastGenerator.SeedRelationships(IReadOnlyList<NpcEntity> npcs, SeededRandom rng)`:

1. Iterate archetypes that carry `relationshipSpawnHints` (The Affair, The Crush). For each NPC of that archetype, find a matching target NPC per the hint's preferences and spawn the relationship entity with the appropriate pattern.
2. Roll the cast bible's "additional relationships" sketch:
   - 2 rivalries (random pairs from non-conflicting archetypes)
   - 1 old flame
   - 1 mentor/mentee asymmetric pair
   - 1 slept-with-their-spouse
   - 2 friend pairs
   - 2 "the thing nobody talks about"
3. For each, instantiate a `RelationshipEntity` with the `RelationshipComponent` from WP-1.4.A, canonical pair ordering, intensity sampled `30..70`.
4. All sampling uses `rng` so the matrix is replay-deterministic.

### Smoke mission file

`examples/smoke-mission-cast-validate.md`:

The mission brief Talon hands to the orchestrator. Asks one Sonnet to validate the cast generator's output against the cast bible (does the population look like a real office?), and dispatches a Haiku batch to score individual NPCs across drives like "internally consistent personality" and "drive distribution feels right."

The mission narrative:

```
Mission: Cast Generator Validation
Goal: Verify the v0.1 cast generator produces a population that reads as a believable office.
Inputs: The world-definition file at docs/c2-content/world-definitions/office-starter.json, post-cast-generator boot.

Tier-2 Sonnet brief:
Read the spawned WorldStateDto (provided as input). For each NPC, verify:
  - Drive values are within the archetype's chronically-elevated/depressed ranges.
  - Personality dimensions are within archetype ranges.
  - Inhibitions match the archetype's starter set.
  - Relationships seeded match the cast bible's starting sketch counts.
Produce a SonnetResult with assertions per category.

Tier-3 Haiku scenarios (5):
For each of 5 randomly sampled NPCs, dispatch a Haiku scenario:
  - Read the NPC's full social state.
  - Score 0-100 on "internally consistent personality" (drives + personality + register align with archetype).
  - Score 0-100 on "drive distribution feels right" (no NPC has all drives maxed; baselines vary).
  - Score 0-100 on "this could be a real office worker" (sanity check on the whole package).
  - Free-form note (≤ 280 chars) on what stood out.
```

### Mock files for end-to-end mock-mode validation

`examples/mocks/cast-validate.sonnet.json` and 5 `cast-validate.haiku-NN.json` files. The mocks return canned-but-plausible scores so `Warden.Orchestrator run --mock-anthropic --mission examples/smoke-mission-cast-validate.md` exits 0 and produces a valid report. This lets Talon run the orchestrator with no API spend to verify pipeline integration before committing to the real-API run.

### Determinism

Spawn sampling, relationship seeding, deal picking, silhouette selection — all through `SeededRandom`. Two runs with the same seed produce byte-identical NPC populations.

### SimConfig additions

```jsonc
{
  "castGenerator": {
    "elevatedDriveRange": [55, 75],
    "depressedDriveRange": [25, 45],
    "neutralDriveRange":   [40, 60],
    "currentJitterRange":  [-5, 5],
    "rivalryCount":              2,
    "oldFlameCount":             1,
    "mentorPairCount":           1,
    "sleptWithSpouseCount":      1,
    "friendPairCount":           2,
    "thingNobodyTalksAboutCount": 2,
    "relationshipIntensityRange": [30, 70]
  }
}
```

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| schema | `docs/c2-infrastructure/schemas/archetypes.schema.json` | New schema for the archetype catalog. |
| schema | `Warden.Contracts/SchemaValidation/archetypes.schema.json` | Embedded mirror. |
| code | `Warden.Contracts/SchemaValidation/Schema.cs` (modified) | Add `SchemaVersions.Archetypes = "0.1.0"`. |
| data | `docs/c2-content/archetypes/archetypes.json` | All ten archetypes from the cast bible. |
| code | `APIFramework/Bootstrap/CastGenerator.cs` | The spawn + relationship-seed logic. |
| code | `APIFramework/Bootstrap/ArchetypeCatalog.cs` | Loads and validates `archetypes.json`. |
| code | `APIFramework/Components/PersonalityComponent.cs` (modified) | Add `WithPersonality(...)` helper if not already present. |
| code | `APIFramework/Components/EntityTemplates.cs` (modified) | Add `WithCastSpawn(...)` helper that applies a full archetype-derived NPC bundle. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Wire CastGenerator: when world definition has `npcSlots[]`, generator runs after world loader. |
| code | `SimConfig.json` (modified) | Add the `castGenerator` section. |
| mission | `examples/smoke-mission-cast-validate.md` | Mission brief per Design notes. |
| mock | `examples/mocks/cast-validate.sonnet.json` | Canned Sonnet result. |
| mock | `examples/mocks/cast-validate.haiku-01.json` through `examples/mocks/cast-validate.haiku-05.json` | Five canned Haiku scenarios with plausible scores. |
| spec | `examples/smoke-specs/cast-validate.json` | OpusSpecPacket for the cast-validate mission. |
| code | `APIFramework.Tests/Bootstrap/CastGeneratorTests.cs` | (1) Spawn produces an entity with `SocialDrivesComponent`, `WillpowerComponent`, `PersonalityComponent`, `InhibitionsComponent`, register set. (2) Drive baselines fall within archetype ranges. (3) Inhibitions match archetype starter set with strengths in expected ranges. (4) Determinism: same archetype + same seed produces byte-identical NPC. (5) Relationship seeder produces the expected counts of each pattern type. (6) All NPCs spawned have unique entity ids. |
| code | `APIFramework.Tests/Bootstrap/CastGeneratorIntegrationTests.cs` | Full integration: load `office-starter.json`, run cast generator, assert ≥ 5 NPCs spawned, ≥ 5 relationships seeded, simulation ticks 100 ticks without error. |
| code | `Warden.Orchestrator.Tests/CastValidateMockRunTests.cs` | `Warden.Orchestrator run --mock-anthropic --mission examples/smoke-mission-cast-validate.md` exits 0 and produces a valid report.md + report.json. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-1.8.A.md` | Completion note. Standard template. Enumerate (a) which archetypes ship in the catalog, (b) which relationships seed, (c) the smoke mission's mock-run cost (zero) and projected real-API cost (estimate). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `archetypes.json` validates against `archetypes.schema.json` and contains all ten archetypes from the cast bible. | unit-test |
| AT-02 | `CastGenerator.SpawnNpc(theVentId, slot, rng)` produces an entity whose `SocialDrivesComponent.Belonging.Baseline` is within `[55, 75]` (the elevated range). | unit-test |
| AT-03 | Two `SpawnNpc` calls with the same seed produce byte-identical NPC components. | unit-test |
| AT-04 | The spawned NPC has `NpcTag`, no `NpcSlotTag`. | unit-test |
| AT-05 | The spawned NPC's inhibitions match the archetype's `starterInhibitions` count and class set. | unit-test |
| AT-06 | `CastGenerator.SeedRelationships` produces 2 rivalries, 1 old flame, 1 mentor pair, 1 slept-with-their-spouse, 2 friend pairs, 2 "the thing nobody talks about" — with all participants drawn from the spawned NPCs. | unit-test |
| AT-07 | The Affair archetype's spawn always seeds a `relationships[]` entry of pattern `activeAffair` with another NPC. | unit-test |
| AT-08 | The Crush archetype's spawn seeds `secretCrush` toward another NPC. | unit-test |
| AT-09 | Loading `office-starter.json`, running cast generator, and ticking 100 sim-ticks produces no errors. | integration-test |
| AT-10 | `Warden.Orchestrator run --mock-anthropic --mission examples/smoke-mission-cast-validate.md` exits 0 and produces `report.md` + `report.json`. | integration-test |
| AT-11 | All existing `APIFramework.Tests`, `Warden.Telemetry.Tests`, `Warden.Contracts.Tests`, `Warden.Orchestrator.Tests` stay green. | build + unit-test |
| AT-12 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-13 | `dotnet test ECSSimulation.sln` — every existing test stays green; new tests pass. | build |

---

## Followups (not in scope)

- **Real-API smoke mission run** (Talon's manual step after this packet merges). Command:

  ```powershell
  dotnet run --project Warden.Orchestrator -- run \
    --mission examples/smoke-mission-cast-validate.md \
    --specs "examples/smoke-specs/cast-validate.json" \
    --budget-usd 2.00
  ```

  Expected cost: $0.50–$1.20. **Confirm Anthropic balance ≥ $5 before this run.**
- Add archetypes beyond the original ten as the office content evolves.
- Per-NPC name generation (currently NPCs are unnamed; the world-bible already gives them implied names like "Donna," "Frank," "Greg" — needs a name generator).
- Schedule attachment: each NPC needs a daily routine (8am at desk, 10:30 break, etc.) — a separate behavior-layer packet.
- Dialog hints integration: when WP-1.10.A merges, this packet's archetypes get `dialogHints` populated and the generator wires them through `DialogHistoryComponent`.
- Per-NPC proximity range customization (the cast bible's "noticeability" angle).
- Tuning the archetype distribution for office realism: if 4 of 15 NPCs are The Cynic the population reads as flat. A future packet adds distribution constraints.
