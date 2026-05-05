# Phase 3 — Handoff Document
**For:** Testing agent / next LLM session  
**State as of:** 2026-04-27  
**Branch:** main (no feature branch; operator instruction)

---

## What Has Been Built

### WP-3.0.0 — Death State Machine
*Pre-existing before this session; validated by previous agent.*

`LifeStateTransitionSystem` owns the `LifeStateComponent.State` state machine.  
Allowed transitions: `Alive → Incapacitated`, `Incapacitated → Deceased`.  
Single-writer pattern: only `LifeStateTransitionSystem` writes `State`.  
Narrative-emit contract: death narrative is raised **before** the state flip (so MemoryRecordingSystem sees Alive participants).

Files: `APIFramework/Systems/LifeState/LifeStateTransitionSystem.cs`, `LifeStateTransitionRequest.cs`, `LifeStateGuard.cs`, `LifeStateInitializerSystem.cs`

---

### WP-3.0.1 — Choking-on-Food Scenario
**Status: Code written, tests written, build+tests pending operator run.**

#### What it does
- `ChokingDetectionSystem` (Cleanup phase, runs BEFORE `LifeStateTransitions`):
  - Iterates food entities with `EsophagusTransitComponent`.
  - If `BolusComponent.Toughness >= ChokingConfig.BolusSizeThreshold` AND the consumer NPC is Alive AND has at least one distraction condition (low energy, high stress, or high irritation) AND doesn't already have `IsChokingTag`:
    - Attaches `IsChokingTag`
    - Attaches `ChokingComponent` (mirrors tick budget and bolus toughness)
    - Sets `MoodComponent.PanicLevel = ChokingConfig.PanicMoodIntensity`
    - Optionally emits `ChokeStarted` narrative candidate
    - Calls `_transition.RequestTransition(npcId, Incapacitated, CauseOfDeath.Choked, ChokingConfig.IncapacitationTicks)`

- `ChokingCleanupSystem` (Cleanup phase, runs AFTER `LifeStateTransitions`):
  - Removes `IsChokingTag` and `ChokingComponent` from NPCs that transitioned to `Deceased`.

- `LifeStateTransitionRequest` gained optional `IncapacitationTicksOverride` (4th field).
- `LifeStateTransitionSystem.RequestTransition` gained matching optional 4th parameter.
- `MemoryRecordingSystem` now marks `ChokeStarted` as persistent.

#### Config
`SimConfig.Choking` (class `ChokingConfig` in `SimConfig.cs`):
```json
"choking": {
  "bolusSizeThreshold": 0.65,
  "energyThreshold": 40.0,
  "stressThreshold": 70,
  "irritationThreshold": 65,
  "incapacitationTicks": 90,
  "panicMoodIntensity": 0.85,
  "emitChokeStartedNarrative": true
}
```

#### Files added/modified
**Added:**
- `APIFramework/Systems/LifeState/ChokingDetectionSystem.cs`
- `APIFramework/Systems/LifeState/ChokingCleanupSystem.cs`

**Modified:**
- `APIFramework/Systems/LifeState/LifeStateTransitionRequest.cs` — `IncapacitationTicksOverride` field
- `APIFramework/Systems/LifeState/LifeStateTransitionSystem.cs` — signature update; passthrough
- `APIFramework/Systems/MemoryRecordingSystem.cs` — `ChokeStarted => true` in IsPersistent
- `APIFramework/Config/SimConfig.cs` — `ChokingConfig` class; `Choking` property
- `SimConfig.json` — `"choking"` section
- `APIFramework/Core/SimulationBootstrapper.cs` — system registrations; pipeline comment

#### Tests
`APIFramework.Tests/Systems/LifeState/`:
- `ChokingDetectionSystemTests.cs` — AT-01 through AT-13 (13 `[Fact]` methods)
- `ChokingCleanupSystemTests.cs` — AT-01 through AT-04 (4 `[Fact]` methods)

---

### WP-3.0.2 — Deceased-Entity Handling + Bereavement
**Status: Code written, tests written, build+tests pending operator run.**

#### What it does

**CorpseSpawnerSystem** (Narrative phase, NarrativeBus subscriber):
- Fires on `Choked | SlippedAndFell | StarvedAlone | Died`.
- Attaches `CorpseTag` and `CorpseComponent` to the deceased entity.
- Idempotent: if `CorpseTag` already present, does nothing.
- Reads `CauseOfDeathComponent` opportunistically for metadata (may not be present yet at event-fire time).

**BereavementSystem** (Narrative phase, NarrativeBus subscriber):
- Fires on same four death kinds.
- **Witness path** (second participant in event): `WitnessedDeathEventsToday += 1`, `GriefLevel = Max(current, WitnessGriefIntensity)`.
- **Colleague path** (all Alive NPCs with `RelationshipComponent.Intensity >= BereavementMinIntensity`):
  - `BereavementEventsToday += 1`
  - `GriefLevel = Max(current, ColleagueBereavementGriefIntensity * intensityFraction)`
  - Emits `BereavementImpact` narrative candidate → MemoryRecordingSystem routes as persistent per-pair memory.
  - Witness is excluded from colleague path to avoid double-counting.
- Deterministic: colleagues sorted by ascending EntityIntId.

**BereavementByProximitySystem** (Cleanup phase, per-tick):
- Iterates corpse entities (has `CorpseTag`) and their rooms.
- For each Alive NPC in the same room with a qualifying relationship (Intensity >= `ProximityBereavementMinIntensity`) that hasn't been hit by this corpse yet:
  - Applies `ProximityBereavementStressGain` to `StressComponent.AcuteLevel` directly.
  - Records `corpse.Id` in `BereavementHistoryComponent.EncounteredCorpseIds` (one-shot per (NPC, corpse) pair).

**StressSystem** (updated):
- Gained two new one-shot branches: applies and clears `WitnessedDeathEventsToday` and `BereavementEventsToday` in the same tick.
- Gained `BereavementConfig` constructor argument (optional; defaults to `new BereavementConfig()`).

**MoodComponent** (updated): `GriefLevel` (0–100 float) added. Decays via MoodSystem's `NegativeDecayRate`.

#### Design deviations from WP spec
- `RelationshipComponent.Affection` does not exist; `Intensity` (0–100) is used as affection proxy, scaled to 0..1.
- `IWorldMutationApi.MoveCorpse` deferred to WP-3.0.4 (that API file doesn't exist yet). `CorpseComponent.HasBeenMoved` stub is in place.

#### Config
`SimConfig.Bereavement` + `SimConfig.Corpse` (classes in `SimConfig.cs`):
```json
"bereavement": {
  "witnessedDeathStressGain": 20.0,
  "bereavementStressGain": 5.0,
  "witnessGriefIntensity": 80.0,
  "colleagueBereavementGriefIntensity": 40.0,
  "bereavementMinIntensity": 20,
  "proximityBereavementMinIntensity": 30,
  "proximityBereavementStressGain": 8.0
},
"corpse": {}
```

#### Files added/modified
**Added:**
- `APIFramework/Components/CorpseComponent.cs`
- `APIFramework/Components/BereavementHistoryComponent.cs`
- `APIFramework/Systems/LifeState/CorpseSpawnerSystem.cs`
- `APIFramework/Systems/LifeState/BereavementSystem.cs`
- `APIFramework/Systems/LifeState/BereavementByProximitySystem.cs`

**Modified:**
- `APIFramework/Components/Tags.cs` — `CorpseTag`
- `APIFramework/Components/MoodComponent.cs` — `GriefLevel` (0–100)
- `APIFramework/Components/StressComponent.cs` — `WitnessedDeathEventsToday`, `BereavementEventsToday`
- `APIFramework/Systems/Narrative/NarrativeEventKind.cs` — `BereavementImpact`
- `APIFramework/Systems/MemoryRecordingSystem.cs` — `BereavementImpact => true`
- `APIFramework/Systems/StressSystem.cs` — `BereavementConfig` arg; two stress branches; day-reset
- `APIFramework/Config/SimConfig.cs` — `BereavementConfig`, `CorpseConfig`; root properties
- `SimConfig.json` — `"bereavement"` + `"corpse"` sections
- `APIFramework/Core/SimulationBootstrapper.cs` — three new registrations; pipeline comment

#### Tests
`APIFramework.Tests/Systems/LifeState/`:
- `CorpseSpawnerSystemTests.cs` — AT-01 through AT-05 (5 `[Fact]` methods)
- `BereavementSystemTests.cs` — AT-04 through AT-10 (10 `[Fact]` methods)
- `BereavementByProximitySystemTests.cs` — AT-01 through AT-06 (8 `[Fact]` methods)

---

## Bootstrapper System Pipeline (Updated)

Phase order for Phase 3 systems in Cleanup (80):

```
[StressSystem]                    ← applies + clears stress counters including bereavement
[WorkloadSystem]
[MaskCrackSystem]
[BereavementByProximitySystem]    ← WP-3.0.2: per-tick proximity bereavement
[ChokingDetectionSystem]          ← WP-3.0.1: enqueues incapacitation requests
[LifeStateTransitionSystem]       ← WP-3.0.0: drains queue; only writer of State
[ChokingCleanupSystem]            ← WP-3.0.1: removes tags on Deceased
```

Narrative (70) — NarrativeBus subscribers (no registered Update order dependency):
```
[CorpseSpawnerSystem]             ← WP-3.0.2: attaches CorpseTag on death event
[BereavementSystem]               ← WP-3.0.2: witness + colleague grief
```

---

## Known Issues / Code Review Findings

All have been resolved. One was fixed during this session:

- **[FIXED] `BereavementSystem.cs` — `EntityIntIdFromGuid(Guid id)` was an unused private method.**  
  The method was defined but never called (code used `EntityIntId(Entity entity)` everywhere).  
  Removed. No other callers existed. Build warning would have been IDE0051.

- **[VERIFIED OK] `entity.Remove<T>()`** — exists in `Entity.cs` line 64. Used by `ChokingCleanupSystem`. OK  
- **[VERIFIED OK] `RelationshipComponent.ParticipantA/B` canonical ordering** — guaranteed by constructor (`Math.Min`/`Math.Max`). `BereavementByProximitySystem.FindRelationshipIntensity` applies same normalization. OK  
- **[VERIFIED OK] `BereavementHistoryComponent` struct + `HashSet<Guid>`** — HashSet is a reference type; struct copies share the same heap object. `.Add()` mutates in-place before `.npc.Add(history)` writes the struct back. Correct across multiple corpses per tick. OK  
- **[VERIFIED OK] Re-entrant narrative bus** — `BereavementSystem` emits `BereavementImpact` from within its own `OnDeathEvent` handler. Safe: `BereavementImpact` is not a death kind, so the early-return fires immediately on the recursive call. OK

---

## What's Next

### Deferred (blocked or pending)

| Packet | What | Blocked on |
|:---|:---|:---|
| WP-3.0.3 | Slip-and-fall + locked-in-starved | WP-3.0.4 (IWorldMutationApi) |
| WP-3.0.4 | IWorldMutationApi + StructuralChangeBus + PathfindingCache | Nothing — unblocks 3.0.3 |
| WP-3.0.5 | ComponentStore typed-array refactor | All prior WPs |

### Proposed: Fainting System (WP-3.0.6 candidate)

**Design discussion held. Key decisions needed before implementation:**

#### What fainting is
- NPC becomes temporarily unconscious (Incapacitated state) for a short duration (~15–30 ticks).
- Unlike choking (Incapacitated → Deceased), fainting recovers to Alive.
- Vitals during faint: unconscious, reduced metabolism (like sleep). All cognitive/social guards block.
- The NPC "falls" when fainting — same spatial effect as slip-and-fall (position preserved, NPC is floor-level).

#### Triggers (proposed)
1. `MoodComponent.Fear` (Plutchik) exceeds a threshold (primary: "so scared they faint")
2. Witnessing a death (`WitnessedDeathEventsToday > 0`) + high panic level compound
3. Extreme `AcuteLevel` spike in a single tick (vasovagal: blood pressure drop)

#### Architectural change required: Recovery path in LifeStateTransitionSystem

`LifeStateTransitionSystem` currently forbids `Alive` as a target in `RequestTransition`. The single-writer pattern must be maintained. Proposed extension:

```csharp
// New method on LifeStateTransitionSystem:
public void RequestRecovery(Guid npcId)
// Adds to a separate recovery queue.
// Drained after the main transition queue.
// Only applies Incapacitated → Alive (silently drops on any other state).
```

`FaintingRecoverySystem` (Cleanup, before `LifeStateTransitions`) checks `FaintingComponent.RecoveryTick <= clock.CurrentTick` and calls `_transition.RequestRecovery(npcId)`.

#### New components/tags needed
- `IsFaintingTag` (like `IsChokingTag`)
- `FaintingComponent { long FaintStartTick; long RecoveryTick; FaintCause Cause; }`
- `FaintCause` enum: `Fear | WitnessedDeath | VasovagalShock`

#### New systems needed
- `FaintingDetectionSystem` (Cleanup, before LifeStateTransitions) — detects trigger conditions
- `FaintingRecoverySystem` (Cleanup, before LifeStateTransitions) — queues recovery when RecoveryTick passed
- `FaintingCleanupSystem` (Cleanup, after LifeStateTransitions) — removes tags on recovery (analogous to ChokingCleanupSystem)

#### New narrative event
`NarrativeEventKind.Fainted` — emitted before state flip. `MemoryRecordingSystem` marks it persistent.

#### Config
```json
"fainting": {
  "fearThreshold": 75.0,
  "panicLevelThreshold": 0.80,
  "faintDurationTicks": 20,
  "faintStressGain": 15,
  "emitFaintedNarrative": true
}
```

#### What needs deciding (open questions for Talon)
1. Should fainting use `CauseOfDeath.Fainted` (a misnomer) or a new `CauseOfIncapacitation` enum alongside `CauseOfDeath`?
2. Should a fainting NPC be treated as a "corpse" for `BereavementByProximitySystem` purposes? (Probably not — they're going to wake up.)
3. Should witnessing a faint (as opposed to a death) cause a smaller grief/panic spike in bystanders?
4. Should a fainted NPC who is not rescued within some extended window eventually die? (Or is fainting always a guaranteed recovery?)

---

## How to Run the Tests

```bash
cd C:\repos\_ecs-simulation-engine
dotnet test APIFramework.Tests --filter "FullyQualifiedName~LifeState"
```

Or run everything:
```bash
dotnet test
```

Test files for Phase 3 all live in:
`APIFramework.Tests/Systems/LifeState/`

---

## Entity IntId Convention

All Phase 3 systems use a consistent `EntityIntId(Entity)` helper:
```csharp
private static int EntityIntId(Entity entity)
{
    var b = entity.Id.ToByteArray();
    return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
}
```
First 4 bytes of the entity's Guid, little-endian. Used as the participant ID in `NarrativeEventCandidate.ParticipantIds`. Same calculation is in the test helpers — search for `EntityIntId` in test files to see usage.

---

*Document generated 2026-04-27. Update when WP-3.0.3+ are implemented.*
