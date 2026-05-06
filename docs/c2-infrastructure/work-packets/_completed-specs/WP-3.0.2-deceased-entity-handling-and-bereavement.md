# WP-3.0.2 — Deceased-Entity Handling + Bereavement

> **DO NOT DISPATCH UNTIL WP-3.0.0 IS MERGED.**
> This packet attaches `CorpseComponent` to entities transitioning through `LifeStateComponent.State == Deceased` and reads the witness participant from cause-of-death narrative events. Both surfaces are owned by WP-3.0.0; this packet is the first downstream consumer that produces *observable consequence*. Dispatching prior will fail-closed at build time.
> **Optional dependency:** WP-3.0.4 (live-mutation hardening). If 3.0.4 is merged, the player-driven corpse-drag verb (via `IWorldMutationApi.MoveEntity`) ships in this packet. If 3.0.4 has *not* merged, the corpse-drag verb is **deferred to a follow-up packet** (the corpse stays where it fell at v0.1 — this is acceptable).

**Tier:** Sonnet
**Depends on:** WP-3.0.0 (LifeStateComponent, CauseOfDeathComponent, four new NarrativeEventKind values, persistent narrative routing); optionally WP-3.0.4 (`IWorldMutationApi.MoveEntity`)
**Parallel-safe with:** WP-3.0.1 (choking — disjoint concerns; this packet reacts to the death narrative 3.0.1 produces), WP-3.0.3 (slip-and-fall — disjoint concerns; this packet reacts to its death narrative too)
**Timebox:** 120 minutes
**Budget:** $0.50

---

## Goal

In WP-3.0.0 a deceased NPC's `LifeStateComponent.State` flips to `Deceased`, a `CauseOfDeathComponent` is attached, and a narrative event fires. The world records what happened. But the world does **not yet feel it.** A body hasn't appeared. Donna walks past Mark's cubicle that afternoon and the engine emits no irritation, no stress spike, no persistent memory of the fact that her coworker is sitting in his chair, head down, not moving. The cubicle isn't yet "Cubicle 12." Mark isn't yet the slow drift on adjacent NPCs that the world bible's named-anchor commits to.

This packet ships the *consequence* surface. After this packet:

- A `CorpseComponent` attaches at the moment of `Deceased` transition. The body is now an entity-with-a-flag the rest of the engine can recognise.
- Witnesses (who saw the death directly) accumulate +20 acute stress, a major persistent memory, and a mood drop. Not from this packet writing those values directly — from this packet *enqueueing the right signals* into the existing stress / memory / mood pipelines so they flow naturally.
- Non-witness colleagues with positive prior relationships to the deceased get a smaller (+5 acute stress, mood drop, persistent memory entry) hit when they are next in proximity to the corpse, OR when relationship-aware bereavement detection runs at end of game-day. Both paths are wired.
- The deceased's `RoomMembershipComponent` and `PositionComponent` are explicitly preserved at room-of-death. The cubicle stays empty because no NPC has the schedule to sit in a chair occupied by a corpse.
- A new player-facing verb on `IWorldMutationApi.MoveEntity` (if WP-3.0.4 is merged) lets the player drag the corpse to a destination. (If 3.0.4 hasn't merged, this is deferred — see Non-goals.)
- Cubicle-12-Mark's emotional charge — the slow `suspicion + loneliness` drift on adjacent NPCs — now has a generative source. **A future packet (3.0.x.y or 3.2.x) will wire the long-term ambient drift; this packet ships the immediate impact and the substrate (`CorpseComponent` and the deceased's preserved location) the drift packet will consume.**

This packet is **engine-internal at v0.1.** No wire-format change. No orchestrator change. The cost ledger and reports are unaffected.

---

## Reference files

- `docs/c2-infrastructure/work-packets/WP-3.0.0-life-state-component-and-cause-of-death-events.md` — **read first.** This packet's `CorpseSpawnerSystem` is a subscriber to the narrative events 3.0.0 emits. The contract: the narrative event fires *before* `LifeState` flips to `Deceased`, so subscribers can read the deceased's pre-death state if needed; by the time the next tick begins, both flags are set.
- `docs/c2-infrastructure/work-packets/WP-3.0.4-live-mutation-hardening.md` — read if merged. `IWorldMutationApi.MoveEntity` is the verb the player-driven corpse-drag uses.
- `docs/c2-content/world-bible.md` — Cubicle 12. The named-anchor's emotional charge is what this packet starts to mechanise. The slow drift on adjacent NPCs is *not* in this packet (deferred); the immediate +20 stress on witnesses is.
- `docs/c2-content/aesthetic-bible.md` — proximity is the witness surface. Bereavement-by-proximity reads the `ProximityComponent.AwarenessRange` (broader than conversation, narrower than sight) when a non-witness colleague enters the room of death.
- `docs/c2-content/cast-bible.md` — relationship structure. Bereavement amplitude scales with prior relationship affection (high affection = bigger hit). All archetypes are affected; no archetype is immune.
- `APIFramework/Components/LifeStateComponent.cs`, `CauseOfDeathComponent.cs` (from WP-3.0.0) — read-only.
- `APIFramework/Systems/Narrative/NarrativeEventBus.cs` — subscribe.
- `APIFramework/Systems/Narrative/NarrativeEventKind.cs` (from WP-3.0.0) — `Choked`, `SlippedAndFell`, `StarvedAlone`, `Died`. Subscribe to all four.
- `APIFramework/Components/StressComponent.cs` — **extend** with new source counter `WitnessedDeathEventsToday` and `BereavementEventsToday`. Two counters, two sources, both feed the per-tick stress accumulation in `StressSystem`.
- `APIFramework/Systems/StressSystem.cs` — **modified.** Add two new branches (parallel to existing `OverdueTaskEventsToday`, `SuppressionEventsToday`, etc.).
- `APIFramework/Components/MoodComponent.cs` — bereavement applies a one-shot mood reduction. Use the existing primary-emotion / mood-axis surface; if `Grief` is missing, **add it** (additive; coordinate with WP-3.0.1 if it added `Panic`).
- `APIFramework/Components/RelationshipComponent.cs` — read for relationship affinity to deceased. The bereavement amplitude scales with `Affection` (or whichever axis the existing relationship component carries).
- `APIFramework/Components/RoomMembershipComponent.cs` (Spatial layer) — preserved at deceased's room of death. WP-3.0.0 already documented this; this packet relies on it.
- `APIFramework/Components/ProximityComponent.cs` — `AwarenessRange` (per-NPC; smaller than `SightRange`). Bereavement-by-proximity uses this.
- `APIFramework/Components/Tags.cs` — **add `CorpseTag`** at end. Coordinate with WP-3.0.1 (which adds `IsChokingTag`) and 3.0.4 (which adds `StructuralTag`, `MutableTopologyTag`). All additive.
- `APIFramework/Systems/MemoryRecordingSystem.cs` — already wired to mark `Choked / SlippedAndFell / StarvedAlone / Died` as persistent (per WP-3.0.0). This packet introduces a *separate* narrative kind, `BereavementImpact`, for the indirect-relationship bereavement events. Add it to `IsPersistent` returning `true`.
- `APIFramework/Components/RelationshipMemoryComponent.cs`, `PersonalMemoryComponent.cs` — destinations.
- `APIFramework/Components/PositionComponent.cs` — read-only; the deceased's position is preserved.
- `APIFramework/Mutation/IWorldMutationApi.cs` (from WP-3.0.4, if merged) — `MoveEntity` is the verb the player-corpse-drag uses. Add a guard: `IWorldMutationApi.MoveEntity` rejects (fail-closed) on a deceased entity unless the calling context is "player explicit" (a flag on the call). The `MutableTopologyTag` mechanism extends naturally — the deceased has neither `MutableTopologyTag` nor `StructuralTag`; a one-shot tag `CorpseDragInProgressTag` can permit the move.
- `APIFramework/Core/SimulationBootstrapper.cs` (modified) — register `CorpseSpawnerSystem`, `BereavementSystem`, `BereavementByProximitySystem`. Conflict warning: 3.0.1 and 3.0.3 also touch this file. Resolution: "keep all".
- `APIFramework/Config/SimConfig.cs` (modified) — `BereavementConfig`, `CorpseConfig`.
- `SimConfig.json` (modified) — corresponding sections.
- `APIFramework.Tests/Integration/` — pattern for integration tests on memory + stress.

---

## Non-goals

- Do **not** implement the long-term ambient drift on adjacent NPCs (the Cubicle-12-Mark slow `suspicion + loneliness` accumulation per the world bible). That's a separate follow-up that consumes the `CorpseComponent` substrate this packet ships.
- Do **not** add corpse decay / smell / rot-related drives. The body persists statically. Future packet may layer this in via the existing `RotComponent` if appropriate.
- Do **not** add a funeral / burial / memorial mechanic.
- Do **not** add NPC-driven corpse handling (an NPC walking up to the body and dragging it to the parking lot autonomously). At v0.1, only the player can move a corpse — and only if WP-3.0.4 is merged. Otherwise the body stays put.
- Do **not** modify the deceased entity's existing components beyond attaching `CorpseComponent` and `CorpseTag`. Identity, memory, relationships, all preserved as 3.0.0 specified.
- Do **not** garbage-collect relationship rows that reference the deceased. They remain queryable; future systems may shift their values (a "the relationship is now to a dead person" semantic), but in this packet they're untouched.
- Do **not** modify any Phase 1 or Phase 2 system beyond the listed ones.
- Do **not** implement bereavement amplification for `Personality` traits (the Cynic stoically buries it, the Vent loudly grieves). That's per-archetype tuning; deferred to followup with `archetype-bereavement-bias.json`.
- Do **not** add a runtime LLM call. (SRD §8.1.)
- Do **not** retry, recurse, or "self-heal." Fail closed per SRD §4.1.
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing.

---

## Design notes

### `CorpseComponent` and `CorpseTag`

```csharp
// new file CorpseComponent.cs
public struct CorpseComponent
{
    public long DeathTick;          // mirrors CauseOfDeathComponent.DeathTick for fast reads
    public Guid OriginalNpcEntityId;// the deceased entity id (= the entity carrying this component, but stored for clarity in queries)
    public Guid LocationRoomId;     // mirrors CauseOfDeathComponent.LocationRoomId
    public bool HasBeenMoved;       // false at spawn; player-corpse-drag flips to true
}

// Tags.cs (additive)
public struct CorpseTag {}
```

Both attached at the moment `LifeStateComponent.State == Deceased` is observed by `CorpseSpawnerSystem`. `CorpseTag` is the cheap "is this entity a corpse" query marker; `CorpseComponent` is the typed payload.

### `CorpseSpawnerSystem`

NarrativeEventBus subscriber. Listens for `Choked / SlippedAndFell / StarvedAlone / Died`. On each event:

```csharp
public void OnDeathEvent(NarrativeEventCandidate ev)
{
    var deceased = entityManager.Get(ev.Participants[0]);
    if (deceased == null) return;
    if (deceased.Has<CorpseTag>()) return;        // idempotent

    var cause = deceased.Has<CauseOfDeathComponent>() ? deceased.Get<CauseOfDeathComponent>() : default;
    deceased.Add(new CorpseTag());
    deceased.Add(new CorpseComponent {
        DeathTick           = cause.DeathTick,
        OriginalNpcEntityId = deceased.Id,
        LocationRoomId      = cause.LocationRoomId,
        HasBeenMoved        = false
    });
}
```

The system carries no per-tick logic; it is event-driven. Subscription happens at construction; lifetime persists for the simulation.

### `BereavementSystem`

NarrativeEventBus subscriber, **same** four events. Per event, computes the immediate bereavement impact:

1. **Witness path** (when `ev.Participants.Length >= 2`): the witness (`ev.Participants[1]`) gets:
   - `StressComponent.WitnessedDeathEventsToday += 1` (read by `StressSystem` next tick).
   - `MoodComponent.GriefLevel = MathF.Max(current, witnessGriefIntensity)`.
   - A `BereavementImpact` narrative event is **not** emitted for the witness — the original `Choked` (etc.) event already carries the witness as participant 1 and is already routed by `MemoryRecordingSystem` to per-pair and personal memory with `Persistent = true`. The witness's memory of the death is the canonical record; we don't duplicate it.

2. **Non-witness colleagues** (queried at event time; iterates relationships involving the deceased):
   - For each Alive NPC `colleague` with `RelationshipComponent` row referencing the deceased:
   - Compute `affectionMagnitude = relationship.Affection` (clamp to 0..1).
   - If `affectionMagnitude < bereavementMinAffection` (e.g., 0.20 — "they barely knew each other"), skip.
   - Else: `colleague.StressComponent.BereavementEventsToday += 1`; `colleague.MoodComponent.GriefLevel = MathF.Max(current, bereavementMoodIntensity * affectionMagnitude)`.
   - Emit a `NarrativeEventKind.BereavementImpact` candidate with `participants = [colleague.Id, deceased.Id]`, tags `["bereavement", causeName, $"affection_{affectionMagnitude:F2}"]`. `MemoryRecordingSystem` routes this to per-pair + personal memory with `Persistent = true`.

The "non-witness" path runs once per death, at event time. It's a one-shot cascade — colleague NPCs across the office get hit at the moment the death happens, not when they next walk by the body. This produces immediate ambient "the office is quieter" through stress and mood rather than requiring the colleagues to physically traverse the death scene.

### `BereavementByProximitySystem`

A second-tier system that fires *additionally* when an NPC physically enters the room of a corpse. This is the "Donna walked past Mark's cubicle and was *hit* by the absence of him" effect. Per tick:

- Iterate Alive NPCs.
- For each, compute `RoomMembershipComponent.RoomId` and check if any entity with `CorpseTag` is in the same room.
- If yes AND the NPC's `RelationshipComponent` to the deceased has `Affection ≥ proximityBereavementMinAffection` AND the NPC has not been hit by this corpse's proximity-bereavement (track via a new `EncounteredCorpseIds: HashSet<Guid>` on a `BereavementHistoryComponent`):
- Apply `proximityBereavementStressGain` to `StressComponent.AcuteLevel` (one-shot, per-corpse, per-NPC); add to `EncounteredCorpseIds`.

This gives the world a second moment of bereavement: the immediate hit at death-time, then the on-physical-encounter hit afterwards. Only fires once per (NPC, corpse) pair — not every tick the NPC is in the room.

### Stress system extension

Two new source counters in `StressComponent`:

```csharp
public int WitnessedDeathEventsToday;  // set by BereavementSystem at event time
public int BereavementEventsToday;     // set by BereavementSystem at event time, for non-witness colleagues
```

`StressSystem.Update` adds two new branches (parallel to `OverdueTaskEventsToday`):

```csharp
acuteGain += stress.WitnessedDeathEventsToday * config.WitnessedDeathStressGain;
acuteGain += stress.BereavementEventsToday    * config.BereavementStressGain;
```

The counters reset at the same tick boundary as the existing source counters (per the existing per-day decay). Documented in the completion note.

### Mood + facing reactions

The existing mood pipeline supports per-axis level setting; this packet adds (or assumes) a `GriefLevel : float 0..1` axis on `MoodComponent`. If `Grief` is not already present, the packet adds it as the third or fourth axis (after the existing axes — coordinate with 3.0.1's `Panic` addition). The `FacingSystem` (existing) reads mood and may freeze facing under high `Grief`; document whether the existing system already handles this or whether a small extension is required.

### Player-driven corpse drag (conditional on WP-3.0.4 merged)

If `IWorldMutationApi` is available:

- Add a method `IWorldMutationApi.MoveCorpse(Guid corpseEntityId, int newTileX, int newTileY)`.
- Implementation: validates `corpseEntityId.Has<CorpseTag>()`; updates `PositionComponent`; emits a `StructuralChangeEvent.EntityMoved` (so the path cache invalidates if the body was on a path); sets `CorpseComponent.HasBeenMoved = true`.
- Tests: `IWorldMutationApiMoveCorpseTests.cs` — moves succeed; non-corpse target rejects; structural event emitted.

If `IWorldMutationApi` is **not** available (3.0.4 not merged): omit the `MoveCorpse` method entirely; document in completion note as deferred to follow-up.

### Determinism

- `CorpseSpawnerSystem` is event-driven, deterministic by event order.
- `BereavementSystem` iterates colleagues in `OrderBy(EntityIntId)`.
- `BereavementByProximitySystem` iterates Alive NPCs in `OrderBy(EntityIntId)`; corpse iteration is deterministic by entity id; the `EncounteredCorpseIds` HashSet uses Guid (deterministic add).
- No `System.Random`. The 5000-tick test confirms.

### Tests

- `CorpseComponentTests.cs` — construction, defaults.
- `CorpseSpawnerSystemTests.cs` — death event → CorpseTag + CorpseComponent attached; idempotent (re-sending the event does not duplicate); only fires on the four death kinds, not other narrative events.
- `BereavementSystemWitnessTests.cs` — witnessed death → witness gets `WitnessedDeathEventsToday += 1`, mood `GriefLevel` set, no duplicate narrative emit (the witness's memory comes from the original death event).
- `BereavementSystemColleagueTests.cs` — non-witness with relationship: gets `BereavementEventsToday += 1`, mood drop scaled by affection, `BereavementImpact` narrative emitted, persistent memory routed.
- `BereavementSystemMinAffectionTests.cs` — colleague with affection < threshold: no impact, no narrative.
- `BereavementSystemDeceasedExcludedTests.cs` — the deceased is not iterated as their own bereavement target; another deceased NPC is not iterated as a target.
- `BereavementSystemNoNotifyAliveLessAlive` — an Incapacitated NPC is not yet a target (they cannot form memory at v0.1; their state will resolve and they become Alive or Deceased before the day ends).
- `StressBereavementIntegrationTests.cs` — over 1 game-day, witness's stress increases by `WitnessedDeathStressGain`; colleague's by `BereavementStressGain × affection`; non-witness with no relationship sees no change.
- `BereavementByProximityFirstHitTests.cs` — Alive NPC enters room of corpse with `Affection ≥ threshold`: gets `proximityBereavementStressGain` once.
- `BereavementByProximityRepeatedExposureTests.cs` — same NPC remains in / re-enters the room over 100 ticks: only one stress hit per (NPC, corpse) pair.
- `CorpseLocationPreservedTests.cs` — corpse's `PositionComponent` and `RoomMembershipComponent` are byte-identical at tick T+1000 vs death tick (sanity check beyond 3.0.0's contract).
- `CorpseDragViaWorldMutationApiTests.cs` (conditional on 3.0.4 merged) — `IWorldMutationApi.MoveCorpse` updates position and emits structural change.
- `BereavementDeterminismTests.cs` — 5000-tick run with one mid-run death and 5 colleagues at varied affection: byte-identical state across two seeds.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/CorpseComponent.cs` | New component. |
| code | `APIFramework/Components/Tags.cs` (modified) | Add `CorpseTag`. **Coordinate with WP-3.0.1, WP-3.0.4.** |
| code | `APIFramework/Components/StressComponent.cs` (modified) | Add `WitnessedDeathEventsToday`, `BereavementEventsToday`. |
| code | `APIFramework/Components/MoodComponent.cs` (modified) | Add `GriefLevel` if not present. **Coordinate with WP-3.0.1's `PanicLevel`.** |
| code | `APIFramework/Components/BereavementHistoryComponent.cs` | Per-NPC HashSet of corpse ids the NPC has been impacted by (proximity tier). |
| code | `APIFramework/Systems/LifeState/CorpseSpawnerSystem.cs` | Bus subscriber; attaches CorpseTag + CorpseComponent. |
| code | `APIFramework/Systems/LifeState/BereavementSystem.cs` | Bus subscriber; immediate impact on witnesses + colleagues. |
| code | `APIFramework/Systems/LifeState/BereavementByProximitySystem.cs` | Per-tick check; ambient impact when an NPC enters the corpse's room. |
| code | `APIFramework/Systems/StressSystem.cs` (modified) | Two new source-counter branches. |
| code | `APIFramework/Systems/Narrative/NarrativeEventKind.cs` (modified) | Add `BereavementImpact` at end. **Coordinate with WP-3.0.0, WP-3.0.1's additions.** |
| code | `APIFramework/Systems/MemoryRecordingSystem.cs` (modified) | `BereavementImpact` → `IsPersistent = true`. |
| code | `APIFramework/Mutation/IWorldMutationApi.cs` (modified, conditional on WP-3.0.4 merged) | Add `MoveCorpse` method. |
| code | `APIFramework/Mutation/WorldMutationApi.cs` (modified, conditional) | Implementation. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register three new systems. **Conflict with 3.0.1 / 3.0.3 — keep all.** |
| code | `APIFramework/Config/SimConfig.cs` (modified) | `BereavementConfig`, `CorpseConfig` classes + properties. |
| code | `SimConfig.json` (modified) | `bereavement` and `corpse` sections. |
| code | `APIFramework.Tests/Components/CorpseComponentTests.cs` | Component shape. |
| code | `APIFramework.Tests/Systems/LifeState/CorpseSpawnerSystemTests.cs` | Spawn semantics. |
| code | `APIFramework.Tests/Systems/LifeState/BereavementSystemWitnessTests.cs` | Witness path. |
| code | `APIFramework.Tests/Systems/LifeState/BereavementSystemColleagueTests.cs` | Colleague path. |
| code | `APIFramework.Tests/Systems/LifeState/BereavementSystemMinAffectionTests.cs` | Threshold gating. |
| code | `APIFramework.Tests/Integration/StressBereavementIntegrationTests.cs` | Stress accumulation end-to-end. |
| code | `APIFramework.Tests/Systems/LifeState/BereavementByProximityFirstHitTests.cs` | First-hit semantics. |
| code | `APIFramework.Tests/Systems/LifeState/BereavementByProximityRepeatedExposureTests.cs` | Idempotent per pair. |
| code | `APIFramework.Tests/Integration/CorpseLocationPreservedTests.cs` | Position + room preserved. |
| code | `APIFramework.Tests/Mutation/CorpseDragViaWorldMutationApiTests.cs` (conditional) | MoveCorpse semantics. |
| code | `APIFramework.Tests/Determinism/BereavementDeterminismTests.cs` | 5000-tick byte-identical. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.0.2.md` | Completion note. SimConfig defaults. Whether `MoveCorpse` was wired (3.0.4 merged) or deferred. The first observed end-to-end witnessed-death + bereavement cascade (with seed). Cubicle-12-Mark drift handoff: what data this packet leaves behind for the future ambient-drift packet to consume. `ECSCli ai describe` regen + `FactSheetStalenessTests` regen. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `CorpseComponent`, `CorpseTag`, `BereavementHistoryComponent`, `NarrativeEventKind.BereavementImpact` compile and instantiate. | unit-test |
| AT-02 | On any of the four death narrative events, the deceased gains `CorpseTag` and `CorpseComponent`; the component's `DeathTick`, `LocationRoomId` mirror the cause-of-death values. | unit-test |
| AT-03 | `CorpseSpawnerSystem` is idempotent: re-emitting the same death event does not duplicate `CorpseTag` or `CorpseComponent`. | unit-test |
| AT-04 | Witness path: a witnessed `Choked` event causes the witness to gain `WitnessedDeathEventsToday += 1` and `MoodComponent.GriefLevel` rises to ≥ `witnessGriefIntensity`. | unit-test |
| AT-05 | `StressSystem` reads `WitnessedDeathEventsToday` and applies `WitnessedDeathStressGain` per count, per tick (until source-counter decay). End-to-end: witness's `StressComponent.AcuteLevel` rises by ~+20 over the post-death day. | integration-test |
| AT-06 | Colleague path: an Alive NPC with `RelationshipComponent.Affection ≥ bereavementMinAffection` to the deceased gains `BereavementEventsToday += 1`, mood `GriefLevel ≥ bereavementMoodIntensity × affection`, a `BereavementImpact` narrative emitted with the colleague + deceased as participants, persistent memory routed to per-pair + personal memory. | integration-test |
| AT-07 | Colleague with affection < threshold: no impact; no narrative emission. | unit-test |
| AT-08 | Proximity path: an Alive NPC enters the corpse's room with affection ≥ `proximityBereavementMinAffection`: gets `proximityBereavementStressGain` once; `BereavementHistoryComponent.EncounteredCorpseIds` records the corpse id. | integration-test |
| AT-09 | Proximity path is idempotent: same NPC remains in the room or re-enters over 100 ticks → only one stress hit. | unit-test |
| AT-10 | Corpse position + room preserved 1000 ticks after death (sanity beyond 3.0.0's contract). | integration-test |
| AT-11 | (Conditional on WP-3.0.4 merged) `IWorldMutationApi.MoveCorpse(corpseId, newX, newY)`: position updates, `StructuralChangeEvent.EntityMoved` emitted, `CorpseComponent.HasBeenMoved = true`, pathfinding cache invalidated. | integration-test |
| AT-12 | (Conditional) `MoveCorpse` on a non-corpse entity rejects fail-closed; no state change. | integration-test |
| AT-13 | Determinism: 5000-tick run with one mid-run death + 5 colleagues at varied affection: byte-identical state across two seeds. | unit-test |
| AT-14 | Existing relationship rows referencing the deceased remain valid 1000 ticks after death (no garbage collection). | regression |
| AT-15 | All Phase 0, 1, 2, WP-3.0.0 tests stay green. | regression |
| AT-16 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-17 | `dotnet test ECSSimulation.sln` — all green, no exclusions. | build + unit-test |
| AT-18 | `ECSCli ai describe` regenerates with three new systems and the new components/tags listed; `FactSheetStalenessTests` updated. | build + unit-test |

---

## Followups (not in scope)

- **Cubicle-12 ambient drift.** The slow `suspicion + loneliness` accumulation on NPCs adjacent to a corpse's preserved location, per the world bible. This packet ships the `CorpseComponent` substrate the drift consumes; the drift packet runs at ~3.0.x.y or 3.2.x. Reads `CorpseComponent.LocationRoomId` and applies decay-and-spread logic.
- **Corpse decay / smell.** Use the existing `RotComponent` if appropriate. NPCs in proximity get an additional irritation drive from the smell. Mature; future.
- **NPC-driven corpse handling.** An NPC autonomously walks up to the body and drags it to a destination (the parking lot, the morgue, the loading dock). Substantial design — interacts with action-selection, relationship-to-deceased, archetype (the Newbie won't touch it; the Old Hand will). Future packet.
- **Funeral / memorial.** A scripted ceremony. The Smoking Bench fills up. Donna brings flowers. Mature; later.
- **Per-archetype bereavement bias.** `archetype-bereavement-bias.json` — the Cynic stoically buries it (suppression × +stress over time); the Vent loudly grieves (+mood drop, +talking dialog). Trivial JSON follow-up.
- **Personality × bereavement.** Neuroticism amplifies bereavement; conscientiousness preserves work-discipline through it. Tunable in SimConfig.
- **Bereavement-by-narrative.** Right now the bereavement cascade fires *at death event time*. A future variant: bereavement-by-rumor — colleagues who learn of the death from a third party (via dialog) get a bereavement hit at *that* moment. Couples to the dialog system.
- **Cross-day decay of bereavement memory.** The persistent memory entries this packet writes are forever. Real grief decays. A `MemoryDecayComponent` per archetype could weight bereavement memory differently than other persistent events. Future polish.
- **Multi-corpse handling.** What happens when the office has 3 corpses simultaneously. Substrate handles N transparently; tuning may need a cap on simultaneous bereavement hits. Future stress.
- **Player-facing UI.** Diegetic per the design philosophy: the empty cubicle, the silent phone, the colleagues' visibly slumped postures (mood-driven). Notification surface (HR finds out, the company makes an announcement) — UX/UI bible-driven; 3.1.E.
