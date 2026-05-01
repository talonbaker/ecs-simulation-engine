# WP-3.0.0 — Implementation Notes

**Work packet:** WP-3.0.0 — `LifeStateComponent` + Cause-of-Death Events  
**Phase:** 3.0.x engine substrate (pre-Unity scaffolding)  
**Status:** Implementation complete; pending integration test by operator

---

## What was built

This packet lays the death substrate: every NPC now carries a `LifeStateComponent` at boot, and a `LifeStateTransitionSystem` owns the only legal writes to that component's `State` field. No system outside `LifeStateTransitionSystem` may flip life state.

The substrate is deliberately inert until WP-3.0.1 enqueues the first real transition request. All existing scenario logic continues unchanged; the guards silently pass through when no NPC has been killed.

---

## New files

### `APIFramework/Components/LifeStateComponent.cs`

Contains two enums and two structs:

- `LifeState` enum: `Alive=0`, `Incapacitated=1`, `Deceased=2`
- `CauseOfDeath` enum: `Unknown=0`, `Choked=1`, `SlippedAndFell=2`, `StarvedAlone=3`
- `LifeStateComponent` struct: `State`, `LastTransitionTick` (long), `IncapacitatedTickBudget` (int), `PendingDeathCause` (CauseOfDeath)
- `CauseOfDeathComponent` struct: `Cause`, `DeathTick` (long), `WitnessedByNpcId` (Guid), `LocationRoomId` (string?)

**Deviation from spec:** The WP shows `CauseOfDeathComponent.LocationRoomId` as `Guid`. In the actual codebase, `RoomComponent.Id` is a `string` UUID (not a `Guid`). The field was implemented as `string?` and `EntityRoomMembership` (the service) is used to look up room, not a `RoomMembershipComponent`. This is the correct pattern for this codebase.

### `APIFramework/Systems/LifeState/LifeStateGuard.cs`

Static helper. Two methods:

- `IsAlive(Entity)` — true only when `LifeState.Alive`. Returns `true` for entities without the component (non-NPCs safely pass through).
- `IsBiologicallyTicking(Entity)` — true for `Alive` and `Incapacitated`; false only for `Deceased`. Returns `true` for entities without the component.

### `APIFramework/Systems/LifeState/LifeStateTransitionRequest.cs`

`internal sealed record LifeStateTransitionRequest(Guid NpcId, LifeState TargetState, CauseOfDeath Cause)`

### `APIFramework/Systems/LifeState/LifeStateInitializerSystem.cs`

PreUpdate phase. Idempotent: attaches `LifeStateComponent { State=Alive, LastTransitionTick=0, IncapacitatedTickBudget=0, PendingDeathCause=Unknown }` to every `NpcTag` entity that doesn't already have it. No RNG.

### `APIFramework/Systems/LifeState/LifeStateTransitionSystem.cs`

Cleanup phase (after WorkloadSystem, MaskCrackSystem, StressSystem).

Constructor: `(NarrativeEventBus, EntityManager, SimulationClock, LifeStateConfig, EntityRoomMembership)`

Public API:
- `RequestTransition(Guid npcId, LifeState target, CauseOfDeath cause)` — enqueues; duplicate NpcId in same tick logs a warning and last request wins.
- `LifeStateTransitions` property exposed on `SimulationBootstrapper` for scenario access.
- `PeekQueue(Guid npcId)` — internal, for tests.

Per-tick logic:
1. Drains queue `OrderBy(NpcId)` for determinism.
2. Skips Deceased→Deceased and Deceased→Alive (no resurrection).
3. For Deceased transitions: finds witness via `FindClosestWitness`, gets room string via `EntityRoomMembership.GetRoom(entity)?.Get<RoomComponent>().Id`, emits `NarrativeEventCandidate` **before** flipping state (so MemoryRecordingSystem sees Alive participants), attaches `CauseOfDeathComponent`, then flips `LifeStateComponent`.
4. After draining: ticks down `IncapacitatedTickBudget` for all Incapacitated NPCs; if budget hits zero, enqueues `(npcId, Deceased, PendingDeathCause)` for next tick.

`FindClosestWitness`: iterates `NpcTag` entities, skips self and non-Alive, computes XZ Euclidean distance using `PositionComponent`, returns smallest `EntityIntId` whose distance ≤ `ProximityComponent.ConversationRangeTiles` (or `Guid.Empty` if none).

`CauseToNarrativeKind`:
```
Choked          → NarrativeEventKind.Choked
SlippedAndFell  → NarrativeEventKind.SlippedAndFell
StarvedAlone    → NarrativeEventKind.StarvedAlone
_               → NarrativeEventKind.Died
```

---

## Modified files

### `APIFramework/Systems/Narrative/NarrativeEventKind.cs`

Added at end of enum (additive only): `Choked`, `SlippedAndFell`, `StarvedAlone`, `Died`.

### `APIFramework/Systems/MemoryRecordingSystem.cs`

Added four cases to `IsPersistent` switch, all returning `true`: `Choked`, `SlippedAndFell`, `StarvedAlone`, `Died`.

### `APIFramework/Config/SimConfig.cs`

Added `public LifeStateConfig LifeState { get; set; } = new();` to `SimConfig`.

Added `LifeStateConfig` class:
```csharp
public class LifeStateConfig
{
    public int  DefaultIncapacitatedTicks        = 180;
    public bool EmitDeathInvariantOnTransition   = true;
    public bool IncapacitatedAllowsBladderVoid   = true;
    public bool DeceasedFreezesPosition          = true;
}
```

### `SimConfig.json`

Added `"lifeState"` section before `"actionSelection"`.

### `APIFramework/Core/SimulationBootstrapper.cs`

- Added `using APIFramework.Systems.LifeState;`
- Added `public LifeStateTransitionSystem LifeStateTransitions { get; private set; } = null!;`
- Registered `new LifeStateInitializerSystem()` at `SystemPhase.PreUpdate` (last in phase)
- Created and registered `LifeStateTransitions` at `SystemPhase.Cleanup` (last in phase, after WorkloadSystem and MaskCrackSystem)

### `APIFramework/Systems/InvariantSystem.cs`

Added deceased-invariant branch in `Update` loop:
- If `LifeStateComponent.State == Deceased`: runs only `CheckLifeStateComponent` and `CheckCauseOfDeath`, then `continue`
- All other entities run `CheckLifeStateComponent` + the full existing check suite

New methods:
- `CheckLifeStateComponent`: validates `State` is in [0,2]; validates `CauseOfDeathComponent` exists on Deceased entities
- `CheckCauseOfDeath`: validates `Cause` is in [0,3]; validates `DeathTick > 0`

---

## Guard sweep — complete system table

### Group A — `IsAlive` (skip Incapacitated and Deceased)

| File | Guard location |
|:---|:---|
| `ActionSelectionSystem.cs` | Per-NPC loop |
| `WillpowerSystem.cs` | RestTick NpcTag loop |
| `MoodSystem.cs` | MoodComponent loop |
| `BrainSystem.cs` | MetabolismComponent loop |
| `ScheduleSystem.cs` | NpcTag+ScheduleComponent LINQ query |
| `StressSystem.cs` | NpcTag loop |
| `SocialMaskSystem.cs` | NpcTag loop |
| `MaskCrackSystem.cs` | NpcTag loop |
| `WorkloadSystem.cs` | NpcTag loop |
| `TaskGeneratorSystem.cs` | LINQ `.Where(... && IsAlive(e))` on assignment pool |
| `MovementSystem.cs` | PositionComponent loop (after Has<MovementComponent>) |
| `Dialog/DialogContextDecisionSystem.cs` | Both speaker and listener |
| `DesireSystem.cs` | MetabolismComponent loop |
| `Coupling/LightingToDriveCouplingSystem.cs` | NPC buffer loop |
| `Movement/IdleMovementSystem.cs` | PositionComponent loop |
| `Movement/StepAsideSystem.cs` | MovementComponent loop |
| `Movement/MovementSpeedModifierSystem.cs` | MovementComponent loop |
| `Movement/PathfindingTriggerSystem.cs` | MovementTargetComponent loop |
| `Movement/FacingSystem.cs` | FacingComponent loop |
| `RelationshipLifecycleSystem.cs` | RelationshipTag loop |
| `Dialog/DialogFragmentRetrievalSystem.cs` | Speaker guard |
| `Dialog/DialogCalcifySystem.cs` | DialogHistoryComponent loop |
| `PhysiologyGateSystem.cs` | InhibitionsComponent loop |
| `DriveDynamicsSystem.cs` | NpcTag loop |
| `FeedingSystem.cs` | MetabolismComponent loop |
| `DrinkingSystem.cs` | MetabolismComponent loop |
| `SleepSystem.cs` | EnergyComponent loop |
| `UrinationSystem.cs` | DriveComponent loop (autonomous trigger) |
| `DefecationSystem.cs` | DriveComponent loop (autonomous trigger) |
| `InteractionSystem.cs` | MetabolismComponent loop |
| `Spatial/ProximityEventSystem.cs` | NpcA (observer) in ProximityComponent loop |
| `Spatial/RoomMembershipSystem.cs` | PositionComponent loop (after RoomTag skip) |

**No guard needed:**
- `Coupling/SocialDriveAccumulator.cs` — not an ISystem; no entity loop; guarded at call site
- `MemoryRecordingSystem.cs` — does not gate the bus subscription; deceased can be a participant (their own death); added 4 new IsPersistent=true cases instead
- `Spatial/SpatialIndexSyncSystem.cs` — Group B (see below)

### Group B — `IsBiologicallyTicking` (skip only Deceased)

| File | Guard location |
|:---|:---|
| `EnergySystem.cs` | EnergyComponent loop |
| `MetabolismSystem.cs` | MetabolismComponent loop |
| `BladderFillSystem.cs` | BladderComponent loop |
| `BladderSystem.cs` | BladderComponent loop |
| `EsophagusSystem.cs` | Consumer entity check (transit entity still destroyed on deceased consumer) |
| `DigestionSystem.cs` | StomachComponent loop |
| `SmallIntestineSystem.cs` | SmallIntestineComponent loop |
| `LargeIntestineSystem.cs` | LargeIntestineComponent loop |
| `ColonSystem.cs` | ColonComponent loop |
| `BiologicalConditionSystem.cs` | MetabolismComponent loop |
| `Spatial/SpatialIndexSyncSystem.cs` | PositionComponent loop (Deceased position frozen by MovementSystem; prior index entry remains valid) |

**No guard needed:**
- `RotSystem.cs` — iterates `RotComponent` on food entities (not NPCs); `IsBiologicallyTicking` would be a no-op since food entities have no `LifeStateComponent`. Food continues to rot on a deceased NPC's desk by design.
- Lighting systems (`SunSystem`, `ApertureBeamSystem`, `IlluminationAccumulationSystem`, `LightSourceStateSystem`) — iterate world geometry/lights, not NPCs.

### Group C — InvariantSystem (special)

`InvariantSystem.cs` now branches: Deceased entities run `CheckLifeStateComponent` + `CheckCauseOfDeath` only; all other entities run `CheckLifeStateComponent` + the full existing live-NPC check suite.

---

## Architectural judgement calls

**`EsophagusSystem` guard placement.** Rather than guarding the transit entity (food bolus), the guard was placed on the consumer lookup: `if (consumer != null && consumer.Has<StomachComponent>() && LifeStateGuard.IsBiologicallyTicking(consumer))`. A transit entity headed for a Deceased NPC still gets destroyed (food in mid-air when you die should not float forever), but the deposit is skipped. Incapacitated consumers still receive the bolus — a choking NPC's esophagus continues to advance the problem bolus, which is exactly what WP-3.0.1 depends on.

**Witness distance comparison.** The `FindClosestWitness` method uses `ProximityComponent.ConversationRangeTiles` as the range threshold (not `AwarenessRangeTiles`). The WP says "first NPC in conversation range" — this matches the death-witnessed-in-conversation-range framing in the aesthetic bible.

**`LocationRoomId` as `string?`.** The WP shows `Guid`, but `RoomComponent.Id` is a `string` UUID in this codebase. Using `string?` throughout (CauseOfDeathComponent, transition system, InvariantSystem) is the correct adaptation.

**InvariantViolation for MissingCauseOfDeath.** The `Guard()` helper only handles float ranges. The "Deceased without CauseOfDeathComponent" check was added as a direct `_violations.Add(...)` call with `ActualValue=0, ClampedTo=0, ValidMin=0, ValidMax=0`. This is consistent with how `CheckChronicleIntegrity` handles structural violations (e.g., StainComponent with missing chronicle entry).

---

## What WP-3.0.1 needs from this packet

1. Call `LifeStateTransitions.RequestTransition(npc.Id, LifeState.Incapacitated, CauseOfDeath.Choked)` to begin a choking episode.
2. The transition system handles the 180-tick countdown and the eventual `Deceased(Choked)` transition automatically.
3. The `EsophagusSystem` Group B guard ensures the problem bolus continues to advance while the NPC is Incapacitated — the choking scenario's core mechanic.
4. `LifeStateTransitions` is exposed as a public property on `SimulationBootstrapper`, so scenario systems can enqueue requests without constructor injection.
