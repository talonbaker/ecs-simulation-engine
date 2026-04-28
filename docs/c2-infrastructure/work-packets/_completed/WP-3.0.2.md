# WP-3.0.2 — Deceased-Entity Handling + Bereavement — Completion Note

**Executed by:** claude-sonnet-4-6 (Cowork mode)
**Branch:** main (same worktree, no separate branch — operator instruction)
**Started:** 2026-04-27
**Ended:** 2026-04-27
**Outcome:** ok (untested — operator will integrate and run acceptance tests on return)

---

## Summary (≤ 200 words)

Shipped the consequence surface for WP-3.0.0 deaths. `CorpseSpawnerSystem` (NarrativeBus subscriber, Narrative phase) attaches `CorpseTag` + `CorpseComponent` at death-event time — idempotent, event-driven, no per-tick cost. `BereavementSystem` (same phase, same event subscription) fires two-path impact: witnesses get `WitnessedDeathEventsToday += 1` + `GriefLevel` spike; non-witness colleagues with Intensity ≥ 20 get `BereavementEventsToday += 1` + `GriefLevel` (scaled by intensity fraction) + persistent `BereavementImpact` narrative routed to per-pair memory. `BereavementByProximitySystem` (Cleanup phase) fires a one-shot direct `AcuteLevel` stress hit when an NPC enters a corpse's room (tracked via `BereavementHistoryComponent.EncounteredCorpseIds`). `StressSystem` gained two new bereavement branches (one-shot: apply and clear the counter in the same tick). `MoodComponent.GriefLevel` (0–100) and `StressComponent.WitnessedDeathEventsToday/BereavementEventsToday` are new fields. `IWorldMutationApi.MoveCorpse` deferred — WP-3.0.4 not yet merged.

Key deviation from spec: `Affection` field does not exist on `RelationshipComponent`; replaced by `Intensity` scaled to 0..1.

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | n/a | CorpseComponent, CorpseTag, BereavementHistoryComponent, BereavementImpact compile pending |
| AT-02 | n/a | CorpseSpawnerSystem: death event → CorpseTag + CorpseComponent; code review verified |
| AT-03 | n/a | Idempotent guard (Has<CorpseTag>() early return) verified |
| AT-04 | n/a | Witness path: WitnessedDeathEventsToday + GriefLevel; code review verified |
| AT-05 | n/a | StressSystem bereavement branches; code review verified |
| AT-06 | n/a | Colleague path: BereavementEventsToday + GriefLevel + BereavementImpact narrative |
| AT-07 | n/a | Intensity < BereavementMinIntensity: skip guard verified |
| AT-08 | n/a | Proximity path: EncounteredCorpseIds tracking + AcuteLevel direct hit |
| AT-09 | n/a | Idempotent proximity: EncounteredCorpseIds prevents re-hit |
| AT-10 | n/a | Position + room preserved by WP-3.0.0 guards (no change this packet) |
| AT-11 | n/a | MoveCorpse deferred (WP-3.0.4 not merged) |
| AT-12 | n/a | MoveCorpse deferred |
| AT-13 | n/a | Determinism: OrderBy(EntityIntId) in BereavementSystem and BereavementByProximitySystem |
| AT-14 | n/a | Relationship rows preserved (no garbage collection; no change) |
| AT-15 | n/a | Build: pending operator run |
| AT-16 | n/a | Tests: pending operator run |

## Files added

- `APIFramework/Components/CorpseComponent.cs`
- `APIFramework/Components/BereavementHistoryComponent.cs`
- `APIFramework/Systems/LifeState/CorpseSpawnerSystem.cs`
- `APIFramework/Systems/LifeState/BereavementSystem.cs`
- `APIFramework/Systems/LifeState/BereavementByProximitySystem.cs`
- `docs/c2-infrastructure/work-packets/p3-wip/WP-3.0.2-implementation-notes.md`

## Files modified

- `APIFramework/Components/Tags.cs` — `CorpseTag`
- `APIFramework/Components/MoodComponent.cs` — `GriefLevel` (0–100)
- `APIFramework/Components/StressComponent.cs` — `WitnessedDeathEventsToday`, `BereavementEventsToday`
- `APIFramework/Systems/Narrative/NarrativeEventKind.cs` — `BereavementImpact`
- `APIFramework/Systems/MemoryRecordingSystem.cs` — `BereavementImpact → true`
- `APIFramework/Systems/StressSystem.cs` — `BereavementConfig` constructor arg; two new branches; day-reset additions
- `APIFramework/Config/SimConfig.cs` — `BereavementConfig`, `CorpseConfig` classes; root properties
- `SimConfig.json` — `"bereavement"` + `"corpse"` sections
- `APIFramework/Core/SimulationBootstrapper.cs` — three new system registrations; pipeline doc updated; StressSystem receives Config.Bereavement

## Followups

- All ATs pending operator test run.
- `IWorldMutationApi.MoveCorpse` — deferred to WP-3.0.4 merge.
- Cubicle-12 ambient drift (slow suspicion/loneliness) — future packet consuming `CorpseComponent.LocationRoomId`.
- Per-archetype bereavement bias — config-only; future `archetype-bereavement-bias.json`.
- Corpse decay/smell — future via existing `RotComponent`.
- `WorldStateDto.npcs[].isCorpse` wire-format field — deferred to v0.5 schema bump (post-3.0.4).
