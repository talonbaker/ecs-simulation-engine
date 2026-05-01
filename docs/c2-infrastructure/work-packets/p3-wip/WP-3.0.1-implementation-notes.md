# WP-3.0.1 — Choking-on-Food Scenario — Implementation Notes

## Overview

Implements the choking scenario: a tough food bolus + a distracted NPC → incapacitation → death. Built on top of the WP-3.0.0 life-state substrate.

---

## Key Design Decisions

### 1. Iterate boluses, not NPCs

The WP pseudocode shows `em.Query<EsophagusTransitComponent>()` which iterates food bolus transit entities. These are food entities, not NPC entities. The consumer NPC is resolved via `EsophagusTransitComponent.TargetEntityId`. This is the correct pattern — iterating NPCs and checking whether they have a bolus in flight would require a reverse lookup with no clean component path.

### 2. Bolus "size" = `BolusComponent.Toughness`

`EsophagusTransitComponent` has no `BolusSize` field. The WP spec referenced a field that doesn't exist. `BolusComponent.Toughness` (0..1, already present) is semantically equivalent — a tough bolus is more likely to cause choking. Default banana toughness is 0.2; the threshold is 0.65 so normal eating is safe.

### 3. `IncapacitationTicksOverride` added to `LifeStateTransitionRequest`

Choking kills faster than the generic `DefaultIncapacitatedTicks` (180 ticks ≈ 3 minutes). Rather than hardcoding the budget in `LifeStateTransitionSystem`, `RequestTransition` was given an optional `incapacitationTicksOverride` parameter, propagated through `LifeStateTransitionRequest` as `int? IncapacitationTicksOverride`. This is additive-only — no existing call sites changed.

### 4. `MoodComponent.PanicLevel` scale is 0..1, not 0..100

Unlike the eight Plutchik emotions (0–100), `PanicLevel` is set directly from `ChokingConfig.PanicMoodIntensity` which defaults to 0.85. The WP spec specified `panicMoodIntensity = 0.85` as a 0..1 value. MoodSystem decays it toward 0 using `NegativeDecayRate` per game-second.

### 5. ChokeStarted emitted BEFORE RequestTransition

The narrative candidate is raised before enqueueing the incapacitation request, so the order within the tick is:
1. `ChokeStarted` candidate raised → `MemoryRecordingSystem` bus handler fires immediately (synchronous) → witnesses record with both NPC and witness still Alive.
2. `RequestTransition(Incapacitated)` enqueued.
3. `LifeStateTransitionSystem.Update` drains queue → `ApplyIncapacitation` fires → state flips.

This preserves the memory-routing guarantee from WP-3.0.0.

### 6. Witness finding is local, not delegated to LifeStateTransitionSystem

`LifeStateTransitionSystem.FindClosestWitness` is private. `ChokingDetectionSystem` contains its own equivalent that returns an `int?` (EntityIntId) rather than a `Guid`, matching what `NarrativeEventCandidate.ParticipantIds` expects.

### 7. System registration order in Cleanup phase

```
StressSystem → WorkloadSystem → MaskCrackSystem
→ ChokingDetectionSystem     ← (enqueues request)
→ LifeStateTransitionSystem  ← (drains queue)
→ ChokingCleanupSystem       ← (removes tag after death flip)
```

`LifeStateTransitions` is instantiated before `AddSystem(ChokingDetectionSystem)` but registered after, so the reference is valid and execution order is preserved.

---

## Component Map

| Component / Tag | Added by | Removed by |
|:---|:---|:---|
| `IsChokingTag` | ChokingDetectionSystem | ChokingCleanupSystem (on Deceased) |
| `ChokingComponent` | ChokingDetectionSystem | ChokingCleanupSystem (on Deceased) |
| `MoodComponent.PanicLevel` | ChokingDetectionSystem (spike) | MoodSystem (decay each tick) |

---

## Config Defaults

| Key | Value | Rationale |
|:---|:---|:---|
| `bolusSizeThreshold` | 0.65 | Below banana (0.2); above soft foods |
| `energyThreshold` | 40.0 | Fatigued NPC |
| `stressThreshold` | 70 | Near OverwhelmedTag |
| `irritationThreshold` | 65 | Moderate-high irritation |
| `incapacitationTicks` | 90 | ~1.5 min @ TimeScale 120 |
| `panicMoodIntensity` | 0.85 | Acute panic (0..1 scale) |
| `emitChokeStartedNarrative` | true | Witnesses always record onset |

---

## Files Changed

### Created
- `APIFramework/Systems/LifeState/ChokingDetectionSystem.cs`
- `APIFramework/Systems/LifeState/ChokingCleanupSystem.cs`

### Modified
- `APIFramework/Systems/MemoryRecordingSystem.cs` — `ChokeStarted → true` in IsPersistent switch
- `APIFramework/Systems/LifeState/LifeStateTransitionRequest.cs` — added `int? IncapacitationTicksOverride`
- `APIFramework/Systems/LifeState/LifeStateTransitionSystem.cs` — updated `RequestTransition` signature and `ApplyIncapacitation` to use override
- `APIFramework/Config/SimConfig.cs` — added `ChokingConfig` class; added `Choking` property to `SimConfig`
- `SimConfig.json` — added `"choking"` section
- `APIFramework/Core/SimulationBootstrapper.cs` — registered ChokingDetectionSystem, LifeStateTransitions, ChokingCleanupSystem in correct order; updated pipeline doc comment
- `APIFramework/Components/MoodComponent.cs` — added `PanicLevel` field (done in prior context)
- `APIFramework/Components/Tags.cs` — added `IsChokingTag` (done in prior context)
- `APIFramework/Components/ChokingComponent.cs` — created (done in prior context)
- `APIFramework/Systems/Narrative/NarrativeEventKind.cs` — added `ChokeStarted` (done in prior context)

---

## What WP-3.0.2 Needs From This

- `IsChokingTag` on Incapacitated NPCs — useful for UI/witness reactions in future
- `ChokingComponent.RemainingTicks` — mirrors IncapacitatedTickBudget for the rescue mechanic stub
- `MoodComponent.PanicLevel` — queryable hook for animation systems
