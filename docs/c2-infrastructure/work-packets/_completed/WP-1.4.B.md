# WP-1.4.B — Projector: Populate Social State on the Wire — Completion Note

**Executed by:** sonnet-4.6
**Branch:** feat/wp-1.4.B
**Started:** 2026-04-25T00:00:00Z
**Ended:** 2026-04-25T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Closed the social-engine loop on the wire format. Three changes: (1) `TelemetryProjector` bumped to `SchemaVersion = "0.2.1"`, (2) NPC entities (those carrying `NpcTag`) now get a populated `entities[N].social` block drawn from `SocialDrivesComponent`, `WillpowerComponent`, `PersonalityComponent`, and `InhibitionsComponent`, (3) relationship entities (those carrying `RelationshipTag`) are projected into the top-level `relationships[]` array sorted by entity Id ascending.

**Key judgement call — participant ID encoding.** `RelationshipComponent.ParticipantA/B` are `int` counter values; the schema requires UUID strings. The `EntityManager` encodes counter N as a Guid with bytes[0..3] = N little-endian. I reconstruct the Guid from the int using the same scheme and document it in the code comment. Tests verify the round-trip by creating entities with known counters and asserting the projected participant IDs match the entities' actual `Id.ToString()`.

**Fields now projected:** all 8 social drives with current+baseline, willpower current+baseline, all 5 Big Five traits, VocabularyRegister, CurrentMood (omitted when empty), inhibitions (class/strength/awareness). Empty inhibitions list → null (optional field omitted).

**Fields still absent (spatial — deferred):** `rooms[]`, `lightSources[]`, `lightApertures[]`, `clock.sun`. These land after WP-1.2.A merges as WP-1.2.B.

`historyEventIds` emits as an empty array per packet spec (no memory producer yet).

---

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | OK | `AT01_SchemaVersion_Is021` asserts `dto.SchemaVersion == "0.2.1"`. |
| AT-02 | OK | `AT02_NpcWithFullSocialState_ProjectsAllFields` asserts all drives, willpower, personality traits, mood, vocab register, and inhibitions are populated with correct values. |
| AT-03 | OK | `AT03_NonNpcEntity_SocialIsAbsent` asserts `Social == null` for entities without `NpcTag` (regular humans from `SpawnHuman`). |
| AT-04 | OK | `AT04_RelationshipTagEntity_ProducesRelationshipEntry` asserts id, participantA, participantB (as UUID strings matching entity Guids), patterns, intensity, and empty historyEventIds. |
| AT-05 | OK | `AT05_Relationships_SortedByIdAscending` asserts two relationship entries appear in ascending Guid-string order. |
| AT-06 | OK | `AT06_NpcWithSocialState_ValidatesAgainstSchema` — full JSON validates clean against world-state.schema.json (NJsonSchema). |
| AT-07 | OK | `AT07_SameInputsWithSocial_ProduceBytIdenticalJson` — two projections of the same snapshot produce byte-identical JSON. |
| AT-08 | OK | All 31 `Warden.Telemetry.Tests` pass; all 510 tests across the solution pass. |
| AT-09 | OK | `Warden.Contracts.Tests` — 50 passed, 0 failed. DTOs unchanged. |
| AT-10 | OK | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors. |
| AT-11 | OK | `dotnet test ECSSimulation.sln` — 510 passed, 0 failed across all 6 test projects. |

---

## Files added

```
docs/c2-infrastructure/work-packets/_completed/WP-1.4.B.md
```

## Files modified

```
Warden.Telemetry/TelemetryProjector.cs           — (1) SchemaVersion "0.2.1". (2) ProjectSocial for NPC entities. (3) ProjectRelationships + ParticipantIntIdToGuidString. (4) Added System.Linq + contract enum aliases.
Warden.Telemetry.Tests/TelemetryProjectorTests.cs — Added AT-01 through AT-07 tests; kept all prior tests unchanged.
ECSCli.Tests/AiVerbTests.cs                       — Updated AiSnapshot_WritesValidJsonAndPassesSchema to assert "0.2.1" instead of "0.1.0".
```

## Diff stats

3 files changed, 374 insertions(+), 46 deletions(-).

## Followups

- WP-1.2.B (after WP-1.2.A merges): project `rooms[]`, `lightSources[]`, `lightApertures[]`, `clock.sun`; bump SchemaVersion to "0.3.0".
- `relationships[].historyEventIds` population — wire up when memory recording lands.
- `currentMood` enum tightening — currently free-form string ≤ 32 chars; stable vocabulary would allow schema enumeration.
- Participant ID decoding is coupled to `EntityManager`'s counter-Guid scheme; if that scheme changes, `ParticipantIntIdToGuidString` must change too. Low risk, but worth noting for future refactors.
