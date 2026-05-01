# WP-2.3.A — Memory Recording on Relationship Entities

**Tier:** Sonnet
**Depends on:** WP-1.4.A (relationship entities, social drives), WP-1.6.A (narrative event detector + bus), WP-1.9.A (chronicle channel), WP-2.1.A (action-selection scaffold provides the activity that triggers most narrative events)
**Parallel-safe with:** WP-2.0.C (different project), WP-2.2.A (different system surface), WP-2.4.A (different component surface)
**Timebox:** 90 minutes
**Budget:** $0.40

---

## Goal

Wire the narrative event bus to the per-pair memory surfaces that v0.4 schemas already reserve. Phase 1's `NarrativeEventDetector` (WP-1.6.A) emits structured candidates onto a singleton `NarrativeEventBus`; Phase 1's chronicle (WP-1.9.A) listens for office-wide-significant ones and persists them as Stains/BrokenItems. Nothing yet captures the *interpersonal* layer — the per-pair memories that turn a relationship from a static pattern into a narrative arc.

This packet adds `MemoryRecordingSystem`: a subscriber on the narrative bus that classifies each candidate by participant count, derives the relationship entity for each two-participant candidate (canonical pair lookup), and appends a structured `MemoryEntry` to a new `RelationshipMemoryComponent` on that relationship entity. Solo candidates (one participant) go to a `PersonalMemoryComponent` on the participant. The buffers are bounded ring-buffers — old memories age out with use, mirroring how real human memory works under load.

The wire format is already reserved: the v0.4 `world-state.schema.json` carries `memoryEvents[]` (top-level) and `relationships[].historyEventIds[]`. Currently the projector emits empty arrays. This packet populates them.

After this packet, two NPCs who have an argument in the breakroom both *remember* it on their relationship entity; a week later (game-time), if the relationship pattern transitions, the memory entries are the audit trail that justifies the transition. Memories are also what the dialog calcify mechanism (WP-1.10.A) and any future affinity/avoidance computations will reference.

---

## Reference files

- `docs/c2-content/world-bible.md` — persistence threshold ("would the staff still be talking about this in a month?"). Per-pair memory threshold is *lighter* than chronicle-global: more events stick to a pair than to the whole office.
- `docs/c2-content/cast-bible.md` — relationship-pattern library. Memory entries can carry a hint that, in aggregate, justifies a pattern transition (Rival → Friend after enough positive interactions).
- `docs/c2-infrastructure/00-SRD.md` §8.3 (memory model: per-pair primary, global thin), §8.5 (social state is first-class).
- `docs/c2-infrastructure/SCHEMA-ROADMAP.md` — confirms v0.4 surfaces are in place; no schema bump.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.4.A.md` — the relationship-entity shape this packet attaches memory to.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.6.A.md` — the narrative event bus this packet subscribes to.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.9.A.md` — the chronicle's persistence-threshold detector. Per-pair memory uses a *similar but lighter* threshold; read this to understand the parallel mechanism.
- `APIFramework/Systems/Narrative/NarrativeEventBus.cs` — the singleton with `OnCandidateEmitted` event. Subscribe in the new system's constructor.
- `APIFramework/Systems/Narrative/NarrativeEventCandidate.cs` — `Tick`, `Kind`, `ParticipantIds: IReadOnlyList<int>`, `RoomId`, `Detail`. Note `ParticipantIds` uses int form (`WillpowerSystem.EntityIntId`).
- `APIFramework/Systems/Narrative/NarrativeEventKind.cs` — the kinds. Read to know which candidates are pair-scoped vs solo vs global.
- `APIFramework/Components/RelationshipComponent.cs` — relationship entity carries `(ParticipantA, ParticipantB)` ints (canonical: lower id first), Patterns, Intensity. New `RelationshipMemoryComponent` is a *sibling* on the same entity.
- `APIFramework/Systems/RelationshipLifecycleSystem.cs` — already iterates relationship entities each tick. Read for context; do not modify (this packet adds a new system, not extending the lifecycle).
- `APIFramework/Components/Tags.cs` — `RelationshipTag` marks relationship entities. Use it for queries.
- `APIFramework/Systems/WillpowerSystem.cs` — `EntityIntId(entity)` static helper. Use to look up entities from `ParticipantIds[i]`.
- `APIFramework/Systems/Chronicle/PersistenceThresholdDetector.cs` — chronicle's filter. Read for parallel-pattern reference: this packet's per-pair filter is *lighter* (more events pass).
- `Warden.Contracts/Telemetry/MemoryEventDto.cs` — the wire shape. Read to know what the projector eventually serialises. Fields: `Id`, `Tick`, `Participants[]`, `Kind`, `Scope` (enum: Pair/Global), `Description`, `Persistent`, `RelationshipId?`. Engine-side `MemoryEntry` mirrors these field-for-field where applicable.
- `Warden.Contracts/Telemetry/RelationshipDto.cs` — has `HistoryEventIds: IReadOnlyList<string>`. The projector populates this from `RelationshipMemoryComponent`.
- `Warden.Telemetry/TelemetryProjector.cs` — current emitter. Modify to populate `relationships[].historyEventIds[]` and the top-level `memoryEvents[]`. Note: line ~403 currently emits empty `HistoryEventIds`; that's the modification site.
- `APIFramework/Core/SimulationBootstrapper.cs` — register `MemoryRecordingSystem` here. The system needs the `NarrativeEventBus` injected; bootstrapper already provides DI for similar singletons.
- `SimConfig.json` — runtime tuning lives here.
- `APIFramework/Core/SeededRandom.cs` — for any tie-breaking. No `System.Random`.

## Non-goals

- Do **not** modify the `NarrativeEventBus`, `NarrativeEventDetector`, or `NarrativeEventCandidate`. They are the producer surface; this packet is a consumer. Adding new candidate kinds or fields is a separate packet.
- Do **not** modify the chronicle (WP-1.9.A): `PersistenceThresholdDetector`, `PhysicalManifestSpawner`, `Stain*`, `BrokenItem*`. The chronicle is the *global* memory channel; this packet is the *per-pair* channel. They run side by side off the same bus, with different thresholds and different sinks.
- Do **not** modify `RelationshipComponent`, `RelationshipLifecycleSystem`, or any social engine surface from WP-1.4.A. Memory is a *new* component on the same entity; the lifecycle system continues unchanged.
- Do **not** modify the dialog calcify mechanism. Memory entries may eventually feed dialog scoring (e.g., "remember when X said Y"), but at v0.1 dialog reads its own per-NPC `DialogHistoryComponent` and ignores `RelationshipMemoryComponent`.
- Do **not** introduce a wire-format change. v0.4 already reserves `memoryEvents[]` and `historyEventIds[]`; the projector populates the existing fields.
- Do **not** modify the `MemoryEventDto`, `MemoryScope` enum, or any DTO. Mirror engine-side; serialise to existing wire shape.
- Do **not** add player-facing memory UI, debug overlays, or memory-replay. Memory is engine state at v0.1; observability is the projector's empty-no-longer arrays.
- Do **not** retroactively populate memories for events that happened before this system was registered. The system starts recording from the tick it's registered onward.
- Do **not** introduce a NuGet dependency.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (SRD §8.1.)
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing.

---

## Design notes

### The new components

```csharp
/// <summary>
/// One recorded memory entry. Mirrors Warden.Contracts.Telemetry.MemoryEventDto
/// in shape so projection is field-for-field.
/// </summary>
public readonly record struct MemoryEntry(
    string                Id,                  // ULID-style; deterministic per (Tick, Kind, Participants)
    long                  Tick,
    NarrativeEventKind    Kind,
    IReadOnlyList<int>    ParticipantIds,      // canonical order (lower id first)
    string?               RoomId,
    string                Detail,              // ≤ 280 chars (matches NarrativeEventCandidate)
    bool                  Persistent           // true if the per-pair persistence threshold flagged it
);

/// <summary>
/// Lives on the relationship entity. Bounded ring buffer of recent memory entries.
/// Older entries age out as new ones arrive past Capacity.
/// </summary>
public struct RelationshipMemoryComponent
{
    public IReadOnlyList<MemoryEntry> Recent;   // capacity-bounded, newest last
}

/// <summary>Lives on each NPC. Solo memories (no peer) go here.</summary>
public struct PersonalMemoryComponent
{
    public IReadOnlyList<MemoryEntry> Recent;
}
```

`MemoryEntry.Id` is generated deterministically: `$"mem-{Tick:D8}-{Kind}-{ParticipantIds[0]:D8}-{ParticipantIds.Count}"` or similar. The Sonnet picks a stable scheme that survives replay (no GUIDs, no timestamps).

### The new system

`MemoryRecordingSystem` (runs at the existing `Cleanup` phase or a new `Memory` phase late in the tick — Sonnet picks; the constraint is "after `NarrativeEventDetector` but before `TelemetryProjector` snapshots state"):

```
Constructor: subscribes to NarrativeEventBus.OnCandidateEmitted.

OnCandidateEmitted(candidate):
  if candidate.ParticipantIds.Count == 0:
      return  // detector shouldn't emit these, but be defensive
  if candidate.ParticipantIds.Count == 1:
      AppendToPersonal(candidate.ParticipantIds[0], candidate)
      return
  if candidate.ParticipantIds.Count == 2:
      var (a, b) = canonical pair (lower int first)
      var rel = FindOrCreateRelationship(a, b)
      AppendToRelationshipMemory(rel, candidate)
      return
  // 3+ participants: append to all participants' personal logs
  foreach pid in candidate.ParticipantIds:
      AppendToPersonal(pid, candidate)

AppendToRelationshipMemory(rel, candidate):
  var entry = BuildEntry(candidate)
  var buf = rel.Get<RelationshipMemoryComponent>().Recent.Append(entry)
              .TakeLast(maxRelationshipMemoryCount).ToList()
  rel.Add(new RelationshipMemoryComponent { Recent = buf })
```

`FindOrCreateRelationship(a, b)`: query for a relationship entity whose canonical (ParticipantA, ParticipantB) matches `(min(a,b), max(a,b))`. If none exists, the system *creates* one with default `Intensity = 50` and no patterns. (Two NPCs who only have a notable event become a "relationship" by virtue of that event — a real-world dynamic.)

### Per-pair persistence threshold (the lighter filter)

WP-1.9.A's chronicle uses a strict threshold ("would the staff still be talking about this in a month?"). Per-pair memory uses a lighter threshold: "would these two people remember this next week?" Concretely, every candidate is recorded; the `Persistent` flag distinguishes lasting vs ephemeral:

```csharp
private static bool IsPersistent(NarrativeEventCandidate c) => c.Kind switch
{
    NarrativeEventKind.RelationshipShift  => true,
    NarrativeEventKind.WillpowerCollapse  => true,
    NarrativeEventKind.AbruptDeparture    => true,
    NarrativeEventKind.ProlongedConflict  => true,
    NarrativeEventKind.SharedSecret       => true,
    NarrativeEventKind.Affair             => true,
    // ... per the actual NarrativeEventKind values
    _                                     => false   // mood spike, drive flicker, etc.
};
```

The Sonnet reads `NarrativeEventKind.cs` to confirm the actual enum values and picks a sensible `Persistent` mapping. The buffer holds *all* entries (persistent + ephemeral); the `Persistent` flag is metadata, not a filter. The projector serialises only persistent entries to `historyEventIds[]` (matching the schema's intent that `historyEventIds[]` are the memorable ones).

### Ring buffer behaviour

`RelationshipMemoryComponent.Recent` is a bounded list. Default capacity: `maxRelationshipMemoryCount = 32`. When a 33rd entry arrives, the oldest entry drops. Persistent entries get *no special protection* — old persistent memories fade just like ephemeral ones, mirroring how real long-term memory consolidates only a fraction of the persistent stream.

`PersonalMemoryComponent.Recent` capacity defaults to 16 — solo memories are noisier (every drive spike could record one), so a smaller window keeps the data tractable.

Both capacities live in `SimConfig.memory.*`.

### Projector population

`TelemetryProjector.cs` currently emits `HistoryEventIds = Array.Empty<string>()` on each `RelationshipDto`. Modification:

```csharp
HistoryEventIds = rel.Has<RelationshipMemoryComponent>()
    ? rel.Get<RelationshipMemoryComponent>().Recent
        .Where(m => m.Persistent)
        .Select(m => m.Id)
        .ToList()
    : Array.Empty<string>()
```

The top-level `WorldStateDto.MemoryEvents` is populated by aggregating across all entities:

```csharp
MemoryEvents = entities
    .Where(e => e.Has<RelationshipMemoryComponent>() || e.Has<PersonalMemoryComponent>())
    .SelectMany(e => GetAllMemoriesFromEntity(e).Select(m => ProjectToDto(m, e)))
    .DistinctBy(dto => dto.Id)   // a pair-scoped memory appears on both participants' relationship; DTO list dedups
    .ToList()
```

`MemoryScope.Pair` for entries on relationship entities; `MemoryScope.Global` is reserved for the chronicle (WP-1.9.A) and stays unused at v0.4 (per the DTO comment). Personal memories use `MemoryScope.Pair` with a single-element participants list — there's no `Personal` scope in the DTO; the schema accepts a single participant with `RelationshipId = null`.

### Determinism

The narrative bus emits candidates in deterministic order (entity-id ascending within a tick, per `NarrativeEventBus.cs`). MemoryRecordingSystem processes them in the order received. Memory IDs are deterministic. The 5000-tick determinism test (parallel to WP-2.1.A's AT-10) verifies byte-identical memory state.

### Tests

- `MemoryEntryTests.cs` — id-generation determinism, equality, JSON round-trip.
- `RelationshipMemoryComponentTests.cs` — ring-buffer overflow, Persistent flag preservation across overflow, equality.
- `PersonalMemoryComponentTests.cs` — same shape as Relationship.
- `MemoryRecordingSystemTests.cs` — bus subscription; pair candidate routes to relationship; solo to personal; 3+ participants fan out to personal; relationship auto-created when none exists.
- `MemoryRecordingSystemBufferTests.cs` — 50 candidates against capacity-32 buffer leaves the most recent 32; oldest 18 dropped.
- `MemoryRecordingSystemPersistenceTests.cs` — `Persistent` flag matches the kind→bool mapping table.
- `MemoryProjectionTests.cs` — projector populates `relationships[].historyEventIds[]` with persistent ids only; populates `worldState.memoryEvents[]` with deduplicated entries; engine-side and DTO-side counts match.
- `MemoryDeterminismTests.cs` — 5000-tick run, two seeds with the same world: byte-identical memory state.

### SimConfig additions

```jsonc
{
  "memory": {
    "maxRelationshipMemoryCount": 32,
    "maxPersonalMemoryCount":     16
  }
}
```

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/MemoryEntry.cs` | The `MemoryEntry` record per Design notes. |
| code | `APIFramework/Components/RelationshipMemoryComponent.cs` | The bounded ring-buffer struct. |
| code | `APIFramework/Components/PersonalMemoryComponent.cs` | Same shape, on NPCs. |
| code | `APIFramework/Systems/MemoryRecordingSystem.cs` | Subscribes to the narrative bus; routes candidates per Design notes. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register the new system; wire it to the existing `NarrativeEventBus` singleton. |
| code | `APIFramework/Config/SimConfig.cs` (modified) | Add `MemoryConfig` class + property. |
| code | `SimConfig.json` (modified) | Add `memory` section. |
| code | `Warden.Telemetry/TelemetryProjector.cs` (modified) | Populate `relationships[].historyEventIds[]` and `worldState.memoryEvents[]` per Design notes. |
| code | `Warden.Contracts/` | **No changes.** DTOs already exist. |
| code | `APIFramework.Tests/Components/MemoryEntryTests.cs` | Construction, id determinism, JSON round-trip. |
| code | `APIFramework.Tests/Components/RelationshipMemoryComponentTests.cs` | Ring-buffer behaviour. |
| code | `APIFramework.Tests/Components/PersonalMemoryComponentTests.cs` | Same shape. |
| code | `APIFramework.Tests/Systems/MemoryRecordingSystemTests.cs` | Bus subscription + routing. |
| code | `APIFramework.Tests/Systems/MemoryRecordingSystemBufferTests.cs` | Overflow tests. |
| code | `APIFramework.Tests/Systems/MemoryRecordingSystemPersistenceTests.cs` | Kind→Persistent mapping. |
| code | `APIFramework.Tests/Systems/MemoryDeterminismTests.cs` | 5000-tick byte-identical. |
| code | `Warden.Telemetry.Tests/MemoryProjectionTests.cs` | Projection populates the wire fields. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-2.3.A.md` | Completion note. Standard template. List the `NarrativeEventKind` → `Persistent` mapping the Sonnet committed to; confirm SimConfig defaults; note any `NarrativeEventKind` values the Sonnet found that weren't in the bibles' lists. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | All new components compile, instantiate, equality round-trip. `MemoryEntry.Id` is deterministic for the same `(Tick, Kind, ParticipantIds, RoomId)`. | unit-test |
| AT-02 | `MemoryRecordingSystem` subscribed to the bus receives candidates emitted from the detector during a 100-tick simulation pass. | unit-test |
| AT-03 | A two-participant candidate is appended to the relationship entity's `RelationshipMemoryComponent.Recent`. The relationship is canonical (lower id first). | unit-test |
| AT-04 | A two-participant candidate where no relationship entity exists yet auto-creates one with default `Intensity=50` and no patterns. | unit-test |
| AT-05 | A solo candidate (one participant) is appended to the participant's `PersonalMemoryComponent.Recent`. | unit-test |
| AT-06 | A 3+-participant candidate fans out: each participant's `PersonalMemoryComponent.Recent` gains the entry. | unit-test |
| AT-07 | Ring-buffer overflow: 50 candidates against capacity-32 buffer leaves the most recent 32 entries; oldest 18 dropped. Equally for capacity-16 personal buffer. | unit-test |
| AT-08 | `Persistent` flag matches the documented `NarrativeEventKind → bool` mapping for every kind. | unit-test |
| AT-09 | Projector populates `relationships[].historyEventIds[]` with the IDs of persistent memories only; ephemeral memories live engine-side but don't appear in `historyEventIds`. | unit-test |
| AT-10 | Projector populates top-level `worldState.memoryEvents[]` with all engine-side memories (persistent + ephemeral), deduplicated by id. Engine-side memory count and DTO list count match (modulo dedup). | unit-test |
| AT-11 | Determinism: 5000-tick run, two seeds with the same world: byte-identical memory state across runs. | unit-test |
| AT-12 | All WP-1.x and WP-2.1.A acceptance tests stay green. The narrative bus and chronicle continue to operate; this packet only adds a new subscriber. | regression |
| AT-13 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-14 | `dotnet test ECSSimulation.sln --filter "FullyQualifiedName!~RunCommandEndToEndTests.AT01"` — every existing test stays green; new tests pass. | build |

---

## Followups (not in scope)

- **Cross-pair memory propagation** — when two NPCs gossip about a third party's memory, the listener's relationship-with-the-third-party should gain a derived memory entry. Not v0.1; needs the dialog system's gossip mechanic which doesn't exist yet.
- **Memory decay over game-time** — currently memories age out by ring-buffer overflow only. Real memory fades with time, not just with new event volume. A weekly decay pass that drops oldest entries unless reinforced would be more realistic. Defer to playtest evidence.
- **Affinity / avoidance derivation** — count of positive vs negative memories on a relationship could feed an affinity score that ActionSelectionSystem reads. Phase 3 polish.
- **Pattern transition triggers** — `RelationshipLifecycleSystem` currently has a transition table but no trigger conditions. Now that memories exist, transitions can fire based on memory aggregates ("after N positive memories, Rival → AlliesOfConvenience"). Separate packet.
- **Memory-driven dialog selection** — calcify mechanism could prefer fragments that match recent memory contexts. Currently dialog reads only `DialogHistoryComponent`. Phase 3+.
- **Long-term consolidation** — distinguish working memory (last day) from long-term memory (consolidated, lossy). A "sleep tick" pass that compresses N short-term entries into 1 long-term summary. Mimics how real memory consolidates during sleep. Speculative.
