# WP-3.0.2 — Deceased-Entity Handling + Bereavement — Implementation Notes

## Overview

Attaches `CorpseComponent`/`CorpseTag` on death, then propagates immediate bereavement impact to witnesses and colleagues, and a second-tier proximity hit when an NPC physically enters a corpse's room.

---

## Key Design Decisions

### 1. `RelationshipComponent` has no `Affection` field

The WP spec referenced a "Affection" axis for bereavement amplitude. This field does not exist. `RelationshipComponent` uses only `Intensity` (0–100) as its strength metric. Bereavement amplitude is scaled by `Intensity / 100f` throughout.

### 2. `GriefLevel` is 0–100 (same scale as Plutchik emotions)

Unlike `PanicLevel` (0..1), `GriefLevel` uses the 0–100 scale to match the eight primary emotions in `MoodComponent`. MoodSystem will decay it via `NegativeDecayRate` per game-second without modification (it shares the same scale). If separate tuning is needed, a `GriefDecayRate` config knob can be added to `MoodSystemConfig` later.

### 3. `WitnessedDeathEventsToday` / `BereavementEventsToday` are one-shot counters

Unlike `OverdueTaskEventsToday` (which accumulates per-tick), these bereavement counters are applied once by `StressSystem` and immediately cleared. This prevents per-tick stress accumulation from a single death event.

### 4. `CorpseSpawnerSystem.OnDeathEvent` fires before `LifeStateComponent.State == Deceased`

The death narrative event is raised before the state flip (WP-3.0.0 contract). `CauseOfDeathComponent` may or may not be attached yet at event-fire time. `CorpseSpawnerSystem` reads it opportunistically and falls back to `ev.Tick` if absent. By the next tick, both are guaranteed present.

### 5. `IWorldMutationApi.MoveCorpse` deferred

WP-3.0.4 is not merged (`IWorldMutationApi` file does not exist). The corpse-drag verb is deferred. `CorpseComponent.HasBeenMoved` is the substrate for this mechanic.

### 6. `BereavementByProximitySystem` runs in Cleanup before `LifeStateTransitions`

New deaths this tick have `CorpseTag` attached synchronously by `CorpseSpawnerSystem` (via the NarrativeBus callback inside `LifeStateTransitionSystem.ApplyDeath`). Since `LifeStateTransitions` runs at the end of Cleanup, the new corpse's tag is visible starting next tick. This is correct — proximity bereavement on a just-died corpse fires one tick later, which is the natural real-time perception delay.

### 7. Witness already has persistent memory from the death event

`MemoryRecordingSystem` marks `Choked`/`SlippedAndFell`/`StarvedAlone`/`Died` as persistent. The witness (second participant) already has a permanent memory entry. `BereavementSystem` does NOT emit a `BereavementImpact` event for the witness — only direct stress and grief modifications. Emitting a duplicate would create two persistent memory entries for the same event.

### 8. Bereavement colleague iteration is deterministic

`BereavementSystem` sorts matching relationship components by colleague EntityIntId (`OrderBy(rc => ...)`) before iterating. `BereavementByProximitySystem` sorts Alive NPCs by EntityIntId. No System.Random.

---

## Component Map

| Component / Tag | Added by | Notes |
|:---|:---|:---|
| `CorpseTag` | CorpseSpawnerSystem (on death narrative) | Persists for entity lifetime |
| `CorpseComponent` | CorpseSpawnerSystem (on death narrative) | Metadata mirror |
| `BereavementHistoryComponent` | BereavementByProximitySystem (lazy) | Tracks encountered corpses |
| `MoodComponent.GriefLevel` | BereavementSystem | 0–100; decays via MoodSystem |
| `StressComponent.WitnessedDeathEventsToday` | BereavementSystem | Cleared by StressSystem |
| `StressComponent.BereavementEventsToday` | BereavementSystem | Cleared by StressSystem |

---

## Files Changed

### Created
- `APIFramework/Components/CorpseComponent.cs`
- `APIFramework/Components/BereavementHistoryComponent.cs`
- `APIFramework/Systems/LifeState/CorpseSpawnerSystem.cs`
- `APIFramework/Systems/LifeState/BereavementSystem.cs`
- `APIFramework/Systems/LifeState/BereavementByProximitySystem.cs`

### Modified
- `APIFramework/Components/Tags.cs` — added `CorpseTag`
- `APIFramework/Components/MoodComponent.cs` — added `GriefLevel` float (0–100)
- `APIFramework/Components/StressComponent.cs` — added `WitnessedDeathEventsToday`, `BereavementEventsToday`
- `APIFramework/Systems/Narrative/NarrativeEventKind.cs` — added `BereavementImpact`
- `APIFramework/Systems/MemoryRecordingSystem.cs` — `BereavementImpact → true`
- `APIFramework/Systems/StressSystem.cs` — added BereavementConfig constructor arg; two new stress branches; day-reset of new counters
- `APIFramework/Config/SimConfig.cs` — `BereavementConfig`, `CorpseConfig` classes; `Bereavement`, `Corpse` properties on root
- `SimConfig.json` — `"bereavement"` and `"corpse"` sections
- `APIFramework/Core/SimulationBootstrapper.cs` — registered CorpseSpawnerSystem, BereavementSystem (Narrative), BereavementByProximitySystem (Cleanup); updated pipeline doc comment; StressSystem now receives Config.Bereavement

## Deferred / Followups

- `IWorldMutationApi.MoveCorpse` — WP-3.0.4 not merged; `HasBeenMoved` stub is in place
- Ambient drift (Cubicle-12 slow suspicion/loneliness on adjacent NPCs) — substrate shipped; drift packet is future
- Per-archetype bereavement bias — config-only tuning; future `archetype-bereavement-bias.json`
- Corpse decay / smell — future packet using existing `RotComponent` if appropriate
