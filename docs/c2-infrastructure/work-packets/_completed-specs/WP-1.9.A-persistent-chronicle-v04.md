# WP-1.9.A — Persistent Chronicle: v0.4 Schema + Threshold Detector + Stain/BrokenItem

**Tier:** Sonnet
**Depends on:** WP-1.6.A (narrative-event candidates — merged), WP-1.4.A (memory recording substrate — merged), WP-1.7.A (world bootstrap — merged), WP-1.0.B (v0.3 schema base — merged).
**Parallel-safe with:** WP-1.8.A (Cast generator), WP-1.10.A (Dialog implementation). Different file footprints.
**Timebox:** 105 minutes
**Budget:** $0.50

---

## Goal

Land the persistent-chronicle layer that makes the world-bible's "spill stays spilled" mechanic real. Four pieces:

1. **Schema v0.4 bump.** Adds top-level `chronicle[]` to `world-state.schema.json` (the array shape was reserved for v0.3 in the roadmap; this packet *populates* it). Each entry carries `id`, `kind`, `participants[]`, `location`, `tick`, `description`, `persistent: bool`, plus a new `physicalManifestEntityId: string?` that references a concrete entity (a Stain, a BrokenItem) when the chronicle event has visible consequence.

2. **`PersistenceThresholdDetector`** — runs each tick after the narrative-event detector. Reads `NarrativeEventCandidate` records from the bus and applies the world bible's persistence threshold ("would the staff still be talking about this in a month?") to decide which candidates persist. Persisted candidates become full `ChronicleEntry` records and either:
   - Append to a global `ChronicleService` ring buffer (the world's global memory).
   - Optionally spawn a physical manifest entity (Stain, BrokenItem) when the event has visible consequence.

3. **`Stain` and `BrokenItem` entity templates.** New `EntityTemplates.Stain(...)` and `EntityTemplates.BrokenItem(...)` factories that mirror chronicle entries as concrete entities. The chronicle is the *narrative* index; the entity tree is the *spatial* index. Both must agree (per axiom 8.4) — enforced by an invariant check that verifies each chronicle entry with `physicalManifestEntityId` resolves to an entity tagged `StainTag` or `BrokenItemTag`.

4. **Projector update.** `Warden.Telemetry/TelemetryProjector.cs` reads the `ChronicleService` and projects entries to `WorldStateDto.Chronicle[]`. Bumps emitted `SchemaVersion` from `"0.3.0"` to `"0.4.0"`.

What this packet does **not** do: per-NPC memory recording on the relationship entity (that's a separate Phase-1.4 follow-up). The chronicle is the *global thin* memory channel per axiom 8.3; per-pair memories are a different concern.

---

## Reference files

- `docs/c2-content/DRAFT-world-bible.md` — **read first**. §"Persistence threshold" is the source of the detector's logic ("would the staff still be talking about this in a month?").
- `docs/c2-infrastructure/00-SRD.md` §8.4 (charm is curated, not simulated; persistent events flow through chronicle channel).
- `docs/c2-infrastructure/SCHEMA-ROADMAP.md` — for the v0.4 minor-bump conventions.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.6.A.md` — confirms the narrative-event bus and `NarrativeEventCandidate` shape.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.4.A.md` — confirms social state and relationship entities.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.0.B.md` — confirms the v0.3 schema baseline this packet bumps.
- `docs/c2-infrastructure/schemas/world-state.schema.json` — the schema this packet modifies.
- `Warden.Contracts/Telemetry/WorldStateDto.cs` — the DTO this packet extends.
- `Warden.Contracts/SchemaValidation/Schema.cs` — version constants.
- `APIFramework/Systems/Narrative/NarrativeEventBus.cs`, `NarrativeEventCandidate.cs` — the candidate stream the detector reads.
- `APIFramework/Components/EntityTemplates.cs` — factory site.
- `Warden.Telemetry/TelemetryProjector.cs` — the projector this packet updates.

## Non-goals

- Do **not** add per-pair memory event recording on the relationship entity. That's a separate Phase-1.4 follow-up. This packet is purely the *global thin chronicle* channel per axiom 8.3.
- Do **not** add a `narrative-event-emit` command to `ai-command-batch.schema.json`. The schema roadmap mentions it for design-time content authoring but it's not needed for the runtime chronicle. Defer to a content-authoring packet later.
- Do **not** modify `world-definition.schema.json` to add `initialChronicleEntries[]`. That's a v0.2 of the world definition; small follow-up after this lands.
- Do **not** populate the chronicle from any existing engine state at boot. Chronicle starts empty unless the world-bootstrap packet adds initial entries (it doesn't yet — that's the followup).
- Do **not** modify any file under `Warden.Anthropic/` or `Warden.Orchestrator/`. The chronicle is engine-internal; orchestrator integration happens via the projector.
- Do **not** change the existing `NarrativeEventCandidate` shape. The detector consumes it as-is.
- Do **not** introduce a NuGet dependency.
- Do **not** use `System.Random`. `SeededRandom` only.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (Architectural axiom 8.1.)

---

## Design notes

### v0.4 schema additions

`world-state.schema.json`:

- `schemaVersion` enum becomes `["0.1.0", "0.2.1", "0.3.0", "0.4.0"]`.
- Add top-level `chronicle[]` (`maxItems: 4096`):

```jsonc
{
  "chronicle": {
    "type": "array",
    "maxItems": 4096,
    "items": { "$ref": "#/$defs/chronicleEntry" }
  }
}
```

- New `$defs/chronicleEntry`:

```jsonc
{
  "type": "object",
  "additionalProperties": false,
  "required": ["id", "kind", "tick", "participants", "description", "persistent"],
  "properties": {
    "id":           { "type": "string", "format": "uuid" },
    "kind":         { "type": "string",
                      "enum": ["spilledSomething", "brokenItem",
                               "publicArgument", "publicHumiliation",
                               "affairRevealed", "promotion",
                               "firing", "kindnessInCrisis",
                               "betrayal", "deathOrLeaving",
                               "other"] },
    "tick":         { "type": "integer", "minimum": 0 },
    "participants": { "type": "array", "maxItems": 8,
                      "items": { "type": "string" } },
    "location":     { "type": "string", "maxLength": 64 },
    "description":  { "type": "string", "maxLength": 280 },
    "persistent":   { "type": "boolean" },
    "physicalManifestEntityId": { "type": "string" }
  }
}
```

`additionalProperties: false`. v0.3 consumers ignore the new field.

### The persistence-threshold detector

`PersistenceThresholdDetector` runs after `NarrativeEventDetector` in the tick pipeline. It subscribes to the narrative-event bus and applies the bible's threshold rules to each candidate:

**Sticks (becomes a chronicle entry):**

1. **Relationship-changing.** Candidate of kind `WillpowerCollapse`, `LeftRoomAbruptly`, or `ConversationStarted` where the resulting drive deltas pushed at least one relationship's `Intensity` by ≥ 15 points.
2. **Standing-changing.** Candidate involves a firing, promotion, or public failure (signaled by candidate kinds — currently absent; reserved for future event kinds the engine will produce in later packets).
3. **Physically hard to undo.** Candidate of kind `DriveSpike` for `irritation` ≥ 70 points where the spike was associated with a physical interaction (proximity-bus had `EnteredConversationRange` in the same tick involving an item — coffee, food, paperwork — and the irritation produced a "drop" outcome). Triggers a `Stain` or `BrokenItem` manifestation.
4. **Recurring talk-about.** A candidate referenced by ≥ 2 NPCs across the recent narrative stream. (Tracked via a small per-tick aggregation buffer.)

**Doesn't stick:**

- `ConversationStarted` candidates with no follow-up drive impact.
- `DriveSpike` candidates where the drive returns to baseline within 60 sim-seconds.
- `WillpowerLow` candidates without a follow-up `WillpowerCollapse`.

The detector is configurable via `SimConfig.chronicle.thresholdRules` so tuning happens without code changes. Defaults match the bible's intent.

### Chronicle service

`ChronicleService` is a singleton in DI:

```csharp
public sealed class ChronicleService
{
    private readonly List<ChronicleEntry> _entries = new();

    public IReadOnlyList<ChronicleEntry> All => _entries;

    public void Append(ChronicleEntry entry);
}
```

Bounded at `SimConfig.chronicle.maxEntries` (default 4096 to match schema). Oldest entries drop on overflow. Sorted by tick (insertion order for events at the same tick is `id` ascending).

### Stain and BrokenItem entity templates

`EntityTemplates.Stain(roomId, position, source, magnitude, rng)` creates an entity with:
- `StainTag`
- `PositionComponent` at the spill location
- `StainComponent` carrying `(string Source, int Magnitude, long CreatedAtTick, string ChronicleEntryId)` — magnitude is the "is it a tiny dot or a real puddle" axis 0–100.
- An `ObstacleTag` if magnitude is high enough to make NPCs route around it.

`EntityTemplates.BrokenItem(originalItemId, roomId, position, breakageKind)` creates an entity with:
- `BrokenItemTag`
- `PositionComponent`
- `BrokenItemComponent` carrying `(string OriginalKind, BreakageKind Breakage, long CreatedAtTick, string ChronicleEntryId)`.

The `ChronicleEntryId` reference in both component shapes is what the invariant check validates.

### Invariant: chronicle ↔ entity-tree agreement

`WorldStateInvariantSystem` (existing or new in this packet) gains a check: every chronicle entry with `physicalManifestEntityId != null` resolves to an existing entity, and that entity has matching tags. The reverse: every `StainTag` / `BrokenItemTag` entity's `ChronicleEntryId` resolves to an existing chronicle entry. Mismatches log to the existing invariant-event channel (already wired from earlier packets).

### Projector update

`TelemetryProjector` reads `ChronicleService.All`, sorts by tick, and projects to `WorldStateDto.Chronicle[]`. Bumps `SchemaVersion` to `"0.4.0"`.

### Determinism

The detector is deterministic — no RNG in the threshold logic. Manifestation choices (which item gets stained, how big the stain is) use `SeededRandom`. Tests verify the chronicle ledger is byte-identical across two runs with the same seed.

### SimConfig additions

```jsonc
{
  "chronicle": {
    "maxEntries": 4096,
    "thresholdRules": {
      "intensityChangeMinForRelationshipStick": 15,
      "irritationSpikeMinForPhysicalManifest": 70,
      "driveReturnToBaselineWindowSeconds":    60,
      "talkAboutMinReferenceCount":             2
    },
    "stainMagnitudeRange":     [10, 80],
    "brokenItemMagnitudeRange": [20, 100]
  }
}
```

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| schema | `docs/c2-infrastructure/schemas/world-state.schema.json` (modified) | `schemaVersion` enum gains `"0.4.0"`. Add `chronicle[]` top-level. Add `$defs/chronicleEntry`. |
| schema | `Warden.Contracts/SchemaValidation/world-state.schema.json` (modified) | Embedded mirror. |
| code | `Warden.Contracts/Telemetry/ChronicleEntryDto.cs` | Record per Design notes. |
| code | `Warden.Contracts/Telemetry/ChronicleEventKind.cs` | Enum mirroring schema. |
| code | `Warden.Contracts/Telemetry/WorldStateDto.cs` (modified) | Add `IReadOnlyList<ChronicleEntryDto>? Chronicle`. |
| code | `Warden.Contracts/SchemaValidation/Schema.cs` (modified) | `SchemaVersions.WorldState = "0.4.0"`. |
| code | `APIFramework/Systems/Chronicle/ChronicleService.cs` | The singleton ring buffer. |
| code | `APIFramework/Systems/Chronicle/ChronicleEntry.cs` | Engine record — `(string Id, ChronicleEventKind Kind, long Tick, IReadOnlyList<int> ParticipantIds, string Location, string Description, bool Persistent, string? PhysicalManifestEntityId)`. |
| code | `APIFramework/Systems/Chronicle/PersistenceThresholdDetector.cs` | The detector per Design notes. |
| code | `APIFramework/Systems/Chronicle/PhysicalManifestSpawner.cs` | Logic that, given a chronicle entry of physical kind, calls `EntityTemplates.Stain(...)` or `BrokenItem(...)`. |
| code | `APIFramework/Components/StainComponent.cs`, `StainTag` (added to Tags.cs) | Per Design notes. |
| code | `APIFramework/Components/BrokenItemComponent.cs`, `BrokenItemTag` (added to Tags.cs) | Per Design notes. |
| code | `APIFramework/Components/EntityTemplates.cs` (modified) | Add `Stain(...)` and `BrokenItem(...)` factories. |
| code | `APIFramework/Systems/InvariantSystem.cs` (modified) | Add chronicle ↔ entity-tree agreement check. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register `ChronicleService` (singleton), `PersistenceThresholdDetector` system in correct phase (after narrative detector). |
| code | `Warden.Telemetry/TelemetryProjector.cs` (modified) | Bump `SchemaVersion = "0.4.0"`. Read `ChronicleService.All`, sort by tick, project to `WorldStateDto.Chronicle`. |
| code | `Warden.Telemetry.Tests/TelemetryProjectorTests.cs` (modified) | Update version-stamp assertion to `"0.4.0"`. Add chronicle-projection test. |
| code | `SimConfig.json` (modified) | Add the `chronicle` section per Design notes. |
| code | `APIFramework.Tests/Systems/Chronicle/PersistenceThresholdDetectorTests.cs` | (1) Relationship-impact candidate persists; minor-effect candidate doesn't. (2) High-irritation spike with physical manifestation produces a Stain entity. (3) Drive-return-to-baseline candidate doesn't persist. (4) Recurring talk-about candidates persist after threshold. (5) Determinism: same seed → byte-identical chronicle ledger. |
| code | `APIFramework.Tests/Components/StainComponentTests.cs`, `BrokenItemComponentTests.cs` | Component invariants, magnitude bounds. |
| code | `APIFramework.Tests/Systems/Chronicle/InvariantTests.cs` | Chronicle entry referencing missing entity → invariant violation. Stain entity with missing chronicle id → invariant violation. |
| doc | `docs/c2-infrastructure/SCHEMA-ROADMAP.md` (modified) | Mark v0.4 as landed. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-1.9.A.md` | Completion note. Standard template. Enumerate (a) which event kinds can chronicle, (b) which physical manifestations exist, (c) what's deferred (per-pair memory, narrative-event-emit command). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `world-state.schema.json` declares `schemaVersion` enum including `"0.4.0"` and adds the `chronicle[]` field with `maxItems: 4096`. | unit-test |
| AT-02 | A v0.3 sample (no chronicle) round-trips clean under v0.4 schema (additive compatibility). | unit-test |
| AT-03 | A new v0.4 sample with 3 chronicle entries round-trips clean. | unit-test |
| AT-04 | `PersistenceThresholdDetector` produces a chronicle entry when a candidate's drive impact pushes a relationship's intensity by ≥ 15 points. | unit-test |
| AT-05 | A candidate that doesn't meet any threshold produces no chronicle entry. | unit-test |
| AT-06 | A high-irritation `DriveSpike` paired with proximity to a coffee item produces a `Stain` entity AND a chronicle entry whose `physicalManifestEntityId` references the stain. | unit-test |
| AT-07 | A `BrokenItem` entity with a missing `ChronicleEntryId` triggers an invariant violation. | unit-test |
| AT-08 | A chronicle entry with `physicalManifestEntityId` referring to a missing entity triggers an invariant violation. | unit-test |
| AT-09 | `ChronicleService` overflow drops oldest entries first; never exceeds `SimConfig.chronicle.maxEntries`. | unit-test |
| AT-10 | Determinism: two runs with same seed produce byte-identical chronicle ledgers and physical manifest entities over 5000 ticks. | unit-test |
| AT-11 | `TelemetryProjector` emits `SchemaVersion = "0.4.0"` and `chronicle[]` populated from `ChronicleService`. | unit-test |
| AT-12 | All existing `Warden.Telemetry.Tests` continue to pass. | build + unit-test |
| AT-13 | All existing `APIFramework.Tests` continue to pass. | build + unit-test |
| AT-14 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-15 | `dotnet test ECSSimulation.sln` — every existing test stays green; new tests pass. | build |

---

## Followups (not in scope)

- Per-pair memory recording on relationship entities (axiom 8.3 says "primary"). Reads narrative-event candidates involving two NPCs and writes to a per-pair memory ring buffer on the relationship entity. Phase-1.4 follow-up.
- `initialChronicleEntries[]` on the world-definition schema — bootstrap the world with pre-existing chronicle ("there's already a stain on the breakroom counter from last year"). Small content-authoring follow-up.
- More physical manifest kinds: `BurnMark`, `TornPaper`, `MissingItem`, etc. Add as the bible's authored anchors expand.
- Decay of physical manifests: a stain that fades over time (per-tick magnitude decay), a broken item that gets cleaned up by an NPC. Behavior layer concern.
- Chronicle queries: "show me events at this location" or "show me events involving this NPC." Indexing layer if the chronicle gets large.
- `narrative-event-emit` command for design-time content authoring (the schema roadmap reserves this; deferred until content workflow needs it).
