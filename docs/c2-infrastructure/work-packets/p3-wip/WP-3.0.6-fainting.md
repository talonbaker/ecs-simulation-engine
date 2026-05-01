# WP-3.0.6 — Fainting System

**Status:** Spec complete — pending implementation  
**Depends on:** WP-3.0.0 (LifeState machine), WP-3.0.1 (choking scaffolding pattern)  
**Blocks:** nothing  
**Estimated complexity:** Low — no new architecture required

---

## Overview

An NPC who experiences extreme fear can faint. Fainting is a temporary loss of consciousness:
the NPC enters the `Incapacitated` life-state for a configurable duration, then automatically
recovers back to `Alive`. Unlike choking, fainting is **never fatal by design** — death cannot
result from a faint. The NPC wakes up.

Physiologically: vitals continue during unconsciousness (heart beats, digestion continues).
Only cognitive and volitional systems are suspended, identical to what the existing
`LifeStateGuard.IsAlive` (Group A) / `LifeStateGuard.IsBiologicallyTicking` (Group B) guards
already enforce.

---

## Architecture Note — No New Infrastructure Required

`LifeStateTransitionSystem.ApplyRequest` already handles the `LifeState.Alive` target on
an `Incapacitated` entity (lines 158–168, labelled "rescue mechanic"). This is the recovery
path. `FaintingRecoverySystem` simply calls `_transition.RequestTransition(npcId, LifeState.Alive, CauseOfDeath.Unknown)`.

The only guard is the existing "resurrection is forbidden" check, which applies only when
current state is **Deceased**. An Incapacitated → Alive transition is explicitly allowed.

**Budget safety:** `FaintingDetectionSystem` passes `FaintingConfig.FaintDurationTicks + 1`
as `incapacitationTicksOverride`. This gives the recovery system exactly one tick of headroom.
`FaintingRecoverySystem` runs **before** `LifeStateTransitionSystem` in Cleanup, so recovery
is always processed before the budget-expiry death check in the same tick.

---

## Trigger Conditions

A faint is triggered when all of the following are true:

1. NPC is `LifeState.Alive` (not already incapacitated or dead).
2. NPC does not already have `IsFaintingTag` (idempotent guard).
3. `MoodComponent.Fear >= FaintingConfig.FearThreshold`.

`Fear` is the Plutchik fear axis (0–100). At the default threshold of 85 this corresponds
to near-terror. `Fear` is not yet wired to any stimulus system at v0.1; it can be manually
set in tests and future stimulus packets.

---

## What Happens at Faint

1. `IsFaintingTag` is attached to the NPC.
2. `FaintingComponent` is attached:
   - `FaintStartTick = clock.CurrentTick`
   - `RecoveryTick = clock.CurrentTick + FaintingConfig.FaintDurationTicks`
3. Optionally a `Fainted` narrative candidate is emitted (before state flip, per WP-3.0.0
   narrative-emit contract, so MemoryRecordingSystem sees an Alive participant).
4. `_transition.RequestTransition(npcId, LifeState.Incapacitated, CauseOfDeath.Unknown,
   FaintDurationTicks + 1)` is called.
   - `PendingDeathCause = Unknown` means if the budget somehow expires, `Died` is recorded.
     This should never happen given the +1 buffer and recovery-system ordering.

## What Happens at Recovery

1. `FaintingRecoverySystem` detects `clock.CurrentTick >= FaintingComponent.RecoveryTick`.
2. Optionally emits `RegainedConsciousness` narrative candidate (before state flip).
3. Calls `_transition.RequestTransition(npcId, LifeState.Alive, CauseOfDeath.Unknown)`.
4. `LifeStateTransitionSystem` drains queue → state flips to `Alive`.
5. `FaintingCleanupSystem` (runs after `LifeStateTransitions`) removes `IsFaintingTag`
   and `FaintingComponent` from NPCs now back to `Alive`.

---

## Cleanup Phase Pipeline (after this WP)

```
[StressSystem]
[WorkloadSystem]
[MaskCrackSystem]
[BereavementByProximitySystem]     WP-3.0.2
[FaintingDetectionSystem]          WP-3.0.6  ← queues Incapacitated; emits Fainted narrative
[FaintingRecoverySystem]           WP-3.0.6  ← queues Alive recovery; emits RegainedConsciousness
[ChokingDetectionSystem]           WP-3.0.1
[LifeStateTransitionSystem]        WP-3.0.0  ← drains all queued transitions (including recovery)
[ChokingCleanupSystem]             WP-3.0.1
[FaintingCleanupSystem]            WP-3.0.6  ← strips tags from recovered NPCs
```

---

## Acceptance Tests

### FaintingDetectionSystem

| ID | Description |
|:---|:---|
| AT-01 | `Fear >= FearThreshold` (Alive NPC) → `IsFaintingTag` attached |
| AT-02 | `Fear >= FearThreshold` → `FaintingComponent.RecoveryTick = startTick + FaintDurationTicks` |
| AT-03 | `Fear < FearThreshold` → no faint, no tag |
| AT-04 | NPC already has `IsFaintingTag` → idempotent; `FaintingComponent` not overwritten |
| AT-05 | Deceased NPC with Fear=100 → not triggered (LifeStateGuard.IsAlive) |
| AT-06 | Incapacitated NPC with Fear=100 → not triggered (already non-Alive) |
| AT-07 | `EmitFaintedNarrative = true` → `Fainted` candidate on NarrativeEventBus |
| AT-08 | `EmitFaintedNarrative = false` → no `Fainted` candidate |
| AT-09 | After `FaintingDetectionSystem.Update` + `LifeStateTransitions.Update` → NPC is Incapacitated |

### FaintingRecoverySystem

| ID | Description |
|:---|:---|
| AT-10 | NPC with `IsFaintingTag` and `RecoveryTick == currentTick` → recovery queued → NPC becomes Alive after `LifeStateTransitions.Update` |
| AT-11 | NPC with `RecoveryTick > currentTick` → no recovery queued |
| AT-12 | `EmitRegainedConsciousnessNarrative = true` → `RegainedConsciousness` candidate emitted at recovery |

### FaintingCleanupSystem

| ID | Description |
|:---|:---|
| AT-13 | Alive NPC with `IsFaintingTag` → tag removed |
| AT-14 | Alive NPC with `IsFaintingTag` + `FaintingComponent` → both removed |
| AT-15 | Incapacitated NPC with `IsFaintingTag` (still out) → tag NOT removed |

### Integration

| ID | Description |
|:---|:---|
| AT-16 | Full tick sequence: set Fear=100 → tick → NPC Incapacitated; advance clock to RecoveryTick → tick → NPC Alive; tags removed |
| AT-17 | After recovery, `MoodComponent.Fear` has not been cleared by this system (it decays via MoodSystem at its own rate) |
| AT-18 | Determinism: two NPCs faint in same tick; both processed in ascending EntityIntId order |
| AT-19 | Fainted NPC does NOT receive `CorpseTag` or `CorpseComponent` (it is NOT dead) |

---

## Components

### `FaintingComponent` (new)

```csharp
public struct FaintingComponent
{
    /// <summary>Tick on which the faint began.</summary>
    public long FaintStartTick;

    /// <summary>
    /// Tick at which FaintingRecoverySystem will queue the Alive recovery.
    /// = FaintStartTick + FaintingConfig.FaintDurationTicks.
    /// </summary>
    public long RecoveryTick;
}
```

### `IsFaintingTag` (new, in Tags.cs)

```csharp
public struct IsFaintingTag { }
```

---

## NarrativeEventKind additions

| Kind | Persistent | Meaning |
|:---|:---:|:---|
| `Fainted` | true | NPC lost consciousness from extreme fear |
| `RegainedConsciousness` | false | NPC woke up; no lasting memory needed by default |

---

## Config

New class `FaintingConfig` in `SimConfig.cs`. New `"fainting"` section in `SimConfig.json`.

| Field | Type | Default | Meaning |
|:---|:---|:---|:---|
| `FearThreshold` | float | 85f | MoodComponent.Fear value that triggers a faint |
| `FaintDurationTicks` | int | 20 | How many ticks the NPC remains unconscious |
| `EmitFaintedNarrative` | bool | true | Whether to raise a Fainted candidate on the bus |
| `EmitRegainedConsciousnessNarrative` | bool | true | Whether to raise a RegainedConsciousness candidate |

---

## Files to Add

| File | Notes |
|:---|:---|
| `APIFramework/Components/FaintingComponent.cs` | New struct |
| `APIFramework/Systems/LifeState/FaintingDetectionSystem.cs` | Cleanup-phase; before LifeStateTransitions |
| `APIFramework/Systems/LifeState/FaintingRecoverySystem.cs` | Cleanup-phase; before LifeStateTransitions |
| `APIFramework/Systems/LifeState/FaintingCleanupSystem.cs` | Cleanup-phase; after LifeStateTransitions |
| `APIFramework.Tests/Systems/LifeState/FaintingDetectionSystemTests.cs` | ATs 01–09 |
| `APIFramework.Tests/Systems/LifeState/FaintingRecoverySystemTests.cs` | ATs 10–12 |
| `APIFramework.Tests/Systems/LifeState/FaintingCleanupSystemTests.cs` | ATs 13–15 |
| `APIFramework.Tests/Systems/LifeState/FaintingIntegrationTests.cs` | ATs 16–19 |

## Files to Modify

| File | Change |
|:---|:---|
| `APIFramework/Components/Tags.cs` | Add `IsFaintingTag` in life-state region |
| `APIFramework/Systems/Narrative/NarrativeEventKind.cs` | Add `Fainted`, `RegainedConsciousness` |
| `APIFramework/Systems/MemoryRecordingSystem.cs` | `Fainted => true`, `RegainedConsciousness => false` |
| `APIFramework/Config/SimConfig.cs` | `FaintingConfig` class; `Fainting` root property |
| `SimConfig.json` | `"fainting"` section with defaults |
| `APIFramework/Core/SimulationBootstrapper.cs` | Register three new systems; update pipeline comment |

---

## Followups (not in scope)

- **Compound triggers:** faint on Fear + witnessing a death + high AcuteLevel in the same tick.
- **Bystander reaction:** witnessing a faint emits a `WitnessedFaint` event; bystanders get a small Fear/Surprise spike (smaller than witnessing death).
- **Smelling salts / revival item:** an interactable entity that calls `RequestTransition(Alive)` immediately, shrinking `RecoveryTick`.
- **Per-archetype faint resistance:** some archetypes (e.g., medics) have a higher `FearThreshold` multiplier.
- **Post-faint debuff:** after recovery, NPC has elevated `Sadness`/`Fear` for several ticks (shame, disorientation).
- **Motor effect:** NPC "falls" — `PositionComponent` is unchanged but a `ProneTag` could block movement actions while fainted (future Transit-phase system).

---

## Key Invariants

1. A fainted NPC **never** receives `CorpseTag` or `CorpseComponent`. Those are reserved for Deceased.
2. A fainted NPC **cannot** die from fainting itself. The `+1` tick budget and recovery-before-budget-check ordering guarantee this.
3. A fainted NPC's vitals (digestion, energy drain, hydration drain) continue normally — `IsBiologicallyTicking` passes for Incapacitated.
4. `LifeStateTransitionSystem` remains the only writer of `LifeStateComponent.State`. `FaintingRecoverySystem` queues a request; it does not write state directly.
5. Fainting is idempotent: re-evaluating a NPC who already has `IsFaintingTag` does nothing.
