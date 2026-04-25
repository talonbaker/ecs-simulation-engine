# WP-1.4.B — Projector: Populate Social State on the Wire

**Tier:** Sonnet
**Depends on:** WP-1.4.A (engine social components must be merged — already true on `staging`).
**Parallel-safe with:** WP-1.2.A (Lighting), WP-1.3.A (Movement), WP-1.6.A (Narrative). All three list `Warden.Telemetry/*` as non-goal; this is the only packet currently authorized to touch the projector.
**Timebox:** 45 minutes
**Budget:** $0.20

---

## Goal

Close the social-engine loop on the wire format. WP-1.4.A landed runtime drives, willpower, personality, inhibitions, and relationship entities in `APIFramework`, but `Warden.Telemetry/TelemetryProjector.cs` still emits `SchemaVersion = "0.1.0"` and ignores all of it. This packet:

1. Reads the social components (`SocialDrivesComponent`, `WillpowerComponent`, `PersonalityComponent`, `InhibitionsComponent`) from each NPC entity in the snapshot and projects them into `entities[].social` per the v0.2.1 wire format.
2. Reads relationship entities (entities tagged with `RelationshipTag` carrying `RelationshipComponent`) and projects them into the top-level `relationships[]` array.
3. Bumps the emitted `SchemaVersion` stamp from `"0.1.0"` to `"0.2.1"`.
4. Updates the existing projector tests to assert the new fields are populated and to remove the "social fields are absent" assertions that WP-1.4.A's AT-12 introduced.

What this packet does **not** do: project spatial state (rooms, lights, apertures, sun, room-membership). Those engine surfaces don't all exist yet (WP-1.2.A is in flight), and projecting them is a separate small follow-up that lands after WP-1.2.A merges (call it WP-1.2.B). This packet stays narrow.

---

## Reference files

- `docs/c2-infrastructure/work-packets/_completed/WP-1.4.A.md` — confirms which engine components exist and their field names.
- `Warden.Telemetry/TelemetryProjector.cs` — the file this packet primarily modifies.
- `Warden.Telemetry/SpeciesClassifier.cs` — read-only context. Determines which entities are NPCs (already classifies humans by tag/components). Use this classifier or its results to decide which entities get `social` projected.
- `Warden.Telemetry.Tests/TelemetryProjectorTests.cs` — the test file this packet updates.
- `Warden.Contracts/Telemetry/WorldStateDto.cs` — the wire-format target shape.
- `Warden.Contracts/Telemetry/SocialStateDto.cs`, `RelationshipDto.cs` — DTO shapes.
- `APIFramework/Components/SocialDrivesComponent.cs`, `WillpowerComponent.cs`, `PersonalityComponent.cs`, `InhibitionsComponent.cs`, `RelationshipComponent.cs`, `Tags.cs` (for `NpcTag`, `RelationshipTag`) — the engine components to read.
- `APIFramework/Core/SimulationSnapshot.cs` — the snapshot the projector reads from. Verify the snapshot exposes the components the projector needs; if it only exposes a subset, the projector may need the `EntityManager` parameter (already an optional argument) for richer access.

## Non-goals

- Do **not** modify any file under `APIFramework/`. The engine is the source; this packet only reads from it.
- Do **not** modify any file under `Warden.Contracts/`. DTOs are stable.
- Do **not** modify any file under `docs/c2-infrastructure/schemas/`. No schema bump.
- Do **not** project spatial state (`rooms[]`, `lightSources[]`, `lightApertures[]`, `clock.sun`). Those land in a follow-up after WP-1.2.A merges.
- Do **not** project narrative-event candidates (the WP-1.6.A bus is engine-internal at this stage). Those don't go through the wire format — there's a separate `ai narrative-stream` CLI verb.
- Do **not** populate `relationships[].historyEventIds`. The schema permits it but no producer exists yet (memory recording is a deferred follow-up).
- Do **not** populate `currentMood` from a hardcoded value. Read it from `PersonalityComponent.CurrentMood`. If empty/null, omit the field (it's optional in the schema).
- Do **not** add a NuGet dependency.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (Architectural axiom 8.1.)

---

## Design notes

### What "an NPC" means in projection

Use the existing `SpeciesClassifier` or check for `NpcTag` (introduced in WP-1.4.A) to identify which entities get `social` projected. Non-NPC entities (food, furniture, the office cat) do not have `SocialDrivesComponent` and should not have an empty `social` field — leave it absent.

### Drive shape on the wire

`SocialStateDto.Drives` is a `DrivesDto` carrying eight `DriveValueDto { Current, Baseline }` sub-fields. The engine's `SocialDrivesComponent` carries the same eight in the same order. Map field-by-field by name; clamp values to the schema's `0–100` (the engine should already enforce this but a defensive clamp at the projector boundary is cheap insurance).

### Inhibition shape

`InhibitionsComponent` carries `IReadOnlyList<Inhibition>`. Project to `IReadOnlyList<InhibitionDto>` by mapping each entry's `Class`, `Strength`, `Awareness` directly. Enum names are identical between engine and DTO (camelCase JSON via `JsonStringEnumConverter`).

### Relationship projection

Iterate entities tagged with `RelationshipTag`. For each, read `RelationshipComponent` and project to `RelationshipDto`. The schema requires `id`, `participantA`, `participantB`, `patterns[]`, `intensity`, `historyEventIds[]`. The component carries all but `historyEventIds`; emit it as an empty array.

`participantA` and `participantB` in the schema are entity *id strings* (UUIDs in the v0.3 schema for rooms/sources/apertures, but for entities the existing `WorldStateDto.Entities[].Id` is the engine's int id stringified — confirm by reading what the existing projector does for `EntityStateDto.Id`). Use the same convention here for consistency.

### Determinism

Iteration order over entities and relationships must be deterministic. Sort by entity id ascending before projection. The existing projector should already sort entities; relationships need the same treatment.

### Schema version stamp

```csharp
SchemaVersion = "0.2.1"
```

Replaces the current `"0.1.0"`. This is the only place the version constant appears in the projector.

### Test updates

Existing `TelemetryProjectorTests` asserts on shape consistent with v0.1 emission. After this packet:

- Tests that asserted `SchemaVersion == "0.1.0"` are updated to assert `"0.2.1"`.
- Tests that asserted `entities[N].social` is absent are removed or inverted to assert it's populated when the entity has `NpcTag`.
- New tests added: one positive case (NPC with full social state round-trips through projector and the output validates against v0.2.1 schema), one relationship case (a `RelationshipTag` entity produces a corresponding `relationships[]` entry).
- The schema-validation harness already exists in `Warden.Contracts.Tests` — projector tests can use it via project reference if it isn't already.

### What about WP-1.4.A's AT-12

WP-1.4.A's AT-12 read: "`Warden.Telemetry.Tests` all pass — projector still emits `SchemaVersion = "0.1.0"` and the social fields are absent." That assertion is now obsolete by design. The corresponding tests are updated by this packet. The completion note explicitly calls out this test-deltas list so the audit trail shows what changed and why.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Telemetry/TelemetryProjector.cs` (modified) | (1) Bump `SchemaVersion = "0.2.1"`. (2) For each NPC entity (via `SpeciesClassifier` or `NpcTag`), construct a `SocialStateDto` from `SocialDrivesComponent` + `WillpowerComponent` + `PersonalityComponent` + `InhibitionsComponent` and assign to `EntityStateDto.Social`. (3) Iterate `RelationshipTag` entities, construct `RelationshipDto` for each, sort by id, assign to `WorldStateDto.Relationships`. (4) `historyEventIds` emits as empty array. |
| code | `Warden.Telemetry.Tests/TelemetryProjectorTests.cs` (modified) | Update existing assertions for new `SchemaVersion`. Remove "social absent" assertions. Add positive social projection test (an NPC with full social state projects correctly and validates against v0.2.1 schema). Add relationship projection test. |
| code | `Warden.Telemetry/SpeciesClassifier.cs` (read-only or minor adjustment) | Only modify if classifier doesn't already expose what the projector needs to identify NPCs. Ideally untouched. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-1.4.B.md` | Completion note. Standard template. Explicitly enumerate (a) which fields are now projected, (b) which fields are still absent (spatial — deferred to follow-up after WP-1.2.A merges), (c) which prior tests changed. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | Projector emits `SchemaVersion = "0.2.1"`. | unit-test |
| AT-02 | An NPC entity with full social components produces a populated `entities[N].social` block: drives (all 8 fields with current+baseline), willpower (current+baseline), personality (Big Five + register + currentMood), inhibitions (entries with class/strength/awareness). | unit-test |
| AT-03 | A non-NPC entity (no `NpcTag`) produces an `entities[N]` block with `social` absent (null). | unit-test |
| AT-04 | A `RelationshipTag` entity with a `RelationshipComponent` produces a corresponding `relationships[]` entry with id, participantA (canonical), participantB (canonical), patterns, intensity, and empty historyEventIds. | unit-test |
| AT-05 | Relationships are sorted by id ascending in the output for determinism. | unit-test |
| AT-06 | The projected DTO validates clean against the v0.2.1 schema using the existing `SchemaValidator`. | unit-test |
| AT-07 | Two projections of the same snapshot with the same inputs produce byte-identical JSON output. | unit-test |
| AT-08 | All other existing `TelemetryProjectorTests` continue to pass with their updates. | build + unit-test |
| AT-09 | `Warden.Contracts.Tests` all pass — DTOs unchanged. | build + unit-test |
| AT-10 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-11 | `dotnet test ECSSimulation.sln` — every existing test stays green; updated tests pass. | build |

---

## Followups (not in scope)

- WP-1.2.B (after WP-1.2.A merges): project spatial state (rooms, light sources, apertures, sun) onto the wire format. Bumps emitted `SchemaVersion` to `"0.3.0"`.
- WP-1.3.B (after WP-1.3.A merges, optional): project facing direction onto the wire — only needed if a future schema bump reserves a `facing` field. Currently facing is engine-internal only.
- Relationship `historyEventIds` population — wired up when memory recording lands.
- `currentMood` enum tightening — the schema currently allows free-form strings up to 32 chars. If practice produces a stable vocabulary, a future schema patch could enumerate.
