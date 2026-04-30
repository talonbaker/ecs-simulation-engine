# WP-3.0.0 — `LifeStateComponent` + Cause-of-Death Events

**Tier:** Sonnet
**Depends on:** WP-2.1.A (action-selection seam), WP-2.1.B (BlockedActionsComponent veto pattern), WP-2.3.A (memory recording), WP-2.4.A (stress system), WP-1.6.A (narrative event bus), WP-1.9.A (persistent chronicle)
**Parallel-safe with:** WP-3.0.4 (live-mutation hardening — disjoint file surface; only `SimulationBootstrapper.cs` overlap, "keep both" merge)
**Timebox:** 120 minutes
**Budget:** $0.50

---

## Goal

The engine cannot model death. Every system in the engine assumes its NPCs are alive — physiology ticks, drives drift, willpower regenerates, action-selection enumerates candidates, the mask cracks, the stress system gains and decays, memory continues to accumulate. The world has no notion of a body that has stopped.

Phase 3 begins with death because the visualizer cannot render what the engine does not represent, and because the canonical first death scenario (choking on a sandwich at a desk; nobody noticed for an hour; cubicle 12 is now empty and the office is quieter for a week) is the proof of concept that everything Phase 1 and Phase 2 built can produce a story. This packet ships the **substrate**: a life-state component and the cause-of-death event surface. It does not ship the choking scenario itself (3.0.1), corpse handling (3.0.2), or any new scenario logic. It is the foundation those packets need.

This packet ships:

1. `LifeStateComponent { LifeState Alive | Incapacitated | Deceased }` attached to every NPC at spawn.
2. `CauseOfDeathComponent { CauseOfDeath Cause; long DeathTick; Guid? WitnessedByNpcId }` attached when an NPC transitions to `Deceased`.
3. Four new `NarrativeEventKind` values: `Choked`, `SlippedAndFell`, `StarvedAlone`, `Died` (general / unknown cause).
4. A single helper — `LifeStateGuard.IsAlive(Entity)` — that every social, physiology, and action system uses for early-return.
5. A `LifeStateTransitionSystem` that owns the `Alive → Incapacitated → Deceased` state machine. **It is the only writer of `LifeStateComponent.State`.**
6. `Persistent` mapping for the four new narrative kinds (all true — death is always remembered).

After this packet, an NPC manually transitioned to `Deceased` no longer ticks: drives stop drifting, willpower stops regenerating, action-selection skips them, dialog never targets them, the mask system leaves them alone, the stress system stops counting them as a source of social stress, memory continues to *reference* them by entity id (the relationship row stays, the deceased's personal memory stays — the engine does not delete a person from history). The 5000-tick determinism contract holds with one NPC dying mid-run.

This packet is **engine-internal at v0.1**. The wire-format `LifeState` field on `WorldStateDto.npcs[]` is deferred to a v0.5 schema bump (its own packet, post-3.0.4). The cost ledger and orchestrator are unaffected.

---

## Reference files

- `docs/PHASE-3-KICKOFF-BRIEF.md` — **read first.** Phase 3 commitments; especially "Death from the beginning"; the architectural axiom 8.7 (engine host-agnostic / WARDEN-vs-RETAIL split).
- `docs/c2-infrastructure/PHASE-2-HANDOFF.md` §6.0 (the Phase 3.0.x backlog and rationale) and §9 (the recommended first move — this packet's shape).
- `docs/c2-infrastructure/00-SRD.md` §4.1 (fail-closed escalation), §8.1 (no runtime LLM), §8.2 (save/load reuses WorldStateDto), §8.7 (engine host-agnostic).
- `docs/c2-content/world-bible.md` — the cubicle-12-Mark dynamic. The death this packet enables is the one that, in 3.0.2, generates Mark's vacancy from real events.
- `docs/c2-content/cast-bible.md` — every NPC spawned by the cast generator gains `LifeStateComponent` at boot. The Old Hand, the Cynic, the Newbie, all of them.
- `docs/c2-content/aesthetic-bible.md` — proximity-as-witness: a death witnessed in conversation range is qualitatively different from a death noticed by no one. This packet records the witness; 3.0.2 does the bereavement work.
- `docs/c2-content/new-systems-ideas.md` — Section "death and what comes after" (skim if present; otherwise the kickoff brief is canonical).
- `APIFramework/Components/CastSpawnComponents.cs` — `NpcArchetypeComponent` / spawn-time component attachment pattern. `LifeStateInitializerSystem` mirrors `StressInitializerSystem`'s structure.
- `APIFramework/Components/IntendedActionComponent.cs` — `IntendedActionComponent` is the action-selection contract. **Do not modify**; the `LifeStateGuard.IsAlive` early-return in `ActionSelectionSystem` keeps `IntendedAction` untouched on a deceased NPC.
- `APIFramework/Components/BlockedActionsComponent.cs` — the existing veto pattern. **Note for design parity**: `LifeStateGuard.IsAlive` is a stronger gate than `BlockedActions` — it is a system-level early-return, not a per-action filter. Do not collapse the two; they have different semantics.
- `APIFramework/Components/DriveComponent.cs`, `WillpowerComponent.cs`, `InhibitionsComponent.cs`, `SocialDrivesComponent.cs`, `StressComponent.cs`, `SocialMaskComponent.cs`, `WorkloadComponent.cs`, `ScheduleComponent.cs`, `MoodComponent.cs`, `BladderComponent.cs`, `MetabolismComponent.cs`, `EnergyComponent.cs`, `StomachComponent.cs`, `ColonComponent.cs`, `LargeIntestineComponent.cs`, `SmallIntestineComponent.cs`, `EsophagusTransitComponent.cs`, `BolusComponent.cs` — read for the "what does this NPC drift on" inventory. The guard pattern's job is to short-circuit the systems that drive these.
- `APIFramework/Systems/DriveDynamicsSystem.cs`, `WillpowerSystem.cs`, `MoodSystem.cs`, `EnergySystem.cs`, `MetabolismSystem.cs`, `BladderFillSystem.cs`, `BladderSystem.cs`, `EsophagusSystem.cs`, `DigestionSystem.cs`, `SmallIntestineSystem.cs`, `LargeIntestineSystem.cs`, `ColonSystem.cs`, `DesireSystem.cs`, `BiologicalConditionSystem.cs`, `BrainSystem.cs`, `RotSystem.cs`, `FeedingSystem.cs`, `DrinkingSystem.cs`, `SleepSystem.cs`, `UrinationSystem.cs`, `DefecationSystem.cs`, `MovementSystem.cs`, `Movement/IdleMovementSystem.cs`, `Movement/StepAsideSystem.cs`, `Movement/PathfindingTriggerSystem.cs`, `Movement/MovementSpeedModifierSystem.cs`, `Movement/FacingSystem.cs`, `Spatial/RoomMembershipSystem.cs`, `Spatial/ProximityEventSystem.cs`, `Spatial/SpatialIndexSyncSystem.cs`, `Coupling/LightingToDriveCouplingSystem.cs`, `Coupling/SocialDriveAccumulator.cs`, `ActionSelectionSystem.cs`, `PhysiologyGateSystem.cs`, `ScheduleSystem.cs`, `WorkloadSystem.cs`, `TaskGeneratorSystem.cs`, `StressSystem.cs`, `SocialMaskSystem.cs`, `MaskCrackSystem.cs`, `Dialog/DialogContextDecisionSystem.cs`, `Dialog/DialogFragmentRetrievalSystem.cs`, `Dialog/DialogCalcifySystem.cs`, `MemoryRecordingSystem.cs`, `RelationshipLifecycleSystem.cs`, `InteractionSystem.cs`, `InvariantSystem.cs` — every NPC-iterating system this packet adds the guard to. Most are 1–3 line changes (an `if (!LifeStateGuard.IsAlive(npc)) continue;` near the top of the per-NPC loop). **InvariantSystem is the exception**: dead NPCs should still pass invariants — the guard there is "the deceased's invariants are a different set" (see Design notes).
- `APIFramework/Systems/Narrative/NarrativeEventKind.cs` — **add four enum values at end** (`Choked`, `SlippedAndFell`, `StarvedAlone`, `Died`). Additive only. Coordinate with WP-3.0.4 if it touches this file (it should not — 3.0.4 deals in topology, not narrative).
- `APIFramework/Systems/Narrative/NarrativeEventCandidate.cs` — read for the participant-list shape. Death events have one participant (the deceased) plus optional witness id encoded in `NarrativeEventCandidate.Tags` or as a second participant — see Design notes for which.
- `APIFramework/Systems/Narrative/NarrativeEventBus.cs` — narrative bus is the emit surface. `LifeStateTransitionSystem` emits the cause-of-death candidate **before** flipping `LifeState` to `Deceased` so subscribers (notably `MemoryRecordingSystem`) see the deceased while still alive — this matters for the "deceased entity not iterated" guard.
- `APIFramework/Systems/MemoryRecordingSystem.cs` — read `IsPersistent` switch. **Modify**: add the four new `NarrativeEventKind` values, all `true`. Death is always persistent.
- `APIFramework/Core/SimulationBootstrapper.cs` — **register two new systems**: `LifeStateInitializerSystem` (PreUpdate / spawn-time, parallel to `StressInitializerSystem`); `LifeStateTransitionSystem` (Cleanup phase, after physiology has had its chance to push the NPC into `Incapacitated` and after action-selection has finished its tick). **Conflict warning**: WP-3.0.4 also registers a system here. Resolution is "keep both" — 30 seconds.
- `APIFramework/Core/SimulationClock.cs` — `TotalTime` / `CurrentTick` for `DeathTick` stamping.
- `APIFramework/Core/SeededRandom.cs` — none of this packet's logic needs randomness; do not introduce `System.Random`.
- `APIFramework.Tests/Systems/StressInitializerSystemTests.cs` and `WorkloadInitializerSystemTests.cs` — pattern templates for `LifeStateInitializerSystemTests.cs`.
- `APIFramework.Tests/Determinism/` (whichever Phase 2 test verifies the 5000-tick contract — likely `WorkloadDeterminismTests.cs` or `MaskSystemDeterminismTests.cs`) — model `LifeStateDeterminismTests.cs` on it.

---

## Non-goals

- Do **not** implement the choking-on-food path. That is WP-3.0.1 (this packet's first downstream consumer). This packet ships only the *substrate* — components, enum values, transition system, guard helper, persistent mapping. A unit test will manually call `LifeStateTransitionSystem.RequestTransition(npc, LifeState.Deceased, CauseOfDeath.Choked)` to exercise the contract; no scenario triggers this in production-config gameplay yet.
- Do **not** add `CorpseComponent`, body-removal logic, or bereavement memory amplification. That is WP-3.0.2.
- Do **not** add slip-and-fall risk to `StainComponent` or locked-door starvation logic. That is WP-3.0.3.
- Do **not** touch the wire format. `WorldStateDto.npcs[]` does **not** gain a `lifeState` field in this packet. That is a v0.5 schema bump packet, post-3.0.4. `Warden.Telemetry/Projectors` is untouched. Engine-internal only.
- Do **not** add a `Killed` event kind (homicide). The four kinds shipped (`Choked`, `SlippedAndFell`, `StarvedAlone`, `Died`) are the v0.1 vocabulary; everything else is deferred.
- Do **not** modify `WorldStateDto`, `Warden.Telemetry`, `Warden.Orchestrator`, `Warden.Anthropic`, `ECSCli`, or any orchestrator/test-harness surface. Engine project (`APIFramework`) and its tests only.
- Do **not** delete or recycle a deceased NPC's entity id. The id remains valid; relationship rows that reference it remain valid; personal-memory entries that reference it remain valid. The engine does not erase a person from history.
- Do **not** modify `RelationshipMemoryComponent` or `PersonalMemoryComponent`'s ring-buffer eviction policies. A bereaved NPC's grief is *recorded* via the standard memory path (the witnessed-death narrative emits, `MemoryRecordingSystem` routes it, the persistent flag keeps it). No new memory subsystem.
- Do **not** add a "ghost" or "haunting" component. Cubicle-12-Mark's emotional charge is content authored at world-bootstrap (per `world-bible.md`'s Cubicle 12 anchor); 3.0.2 will generate that authoring from a real death event but this packet is one step earlier.
- Do **not** introduce a NuGet dependency.
- Do **not** add a runtime LLM call anywhere. (SRD §8.1.)
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing.

---

## Design notes

### The new components

```csharp
public enum LifeState
{
    Alive = 0,
    Incapacitated = 1,
    Deceased = 2
}

public struct LifeStateComponent
{
    public LifeState State;
    public long LastTransitionTick;     // SimulationClock.CurrentTick at the most recent state change
    public int IncapacitatedTickBudget; // counts down each tick while State == Incapacitated; 0 = transition to Deceased
}

public enum CauseOfDeath
{
    Unknown = 0,
    Choked = 1,
    SlippedAndFell = 2,
    StarvedAlone = 3,
    // Future: Killed, Heart, Suicide, etc. Out of scope at v0.1.
}

public struct CauseOfDeathComponent
{
    public CauseOfDeath Cause;
    public long DeathTick;            // SimulationClock.CurrentTick at transition to Deceased
    public Guid WitnessedByNpcId;     // Guid.Empty if unwitnessed; otherwise the first NPC in conversation range at DeathTick
    public Guid LocationRoomId;       // RoomMembershipComponent room id at DeathTick (for 3.0.2's relationship-shift, 3.0.3's slip-on-stain)
}
```

`Incapacitated` is the deliberate intermediate state. Semantics: **non-volitional but biologically alive**. Cognition stops (action-selection skips the NPC); physiology continues to drift but most consumers are guarded out (see guard table); the `IncapacitatedTickBudget` counts down per tick until the transition to `Deceased`. The choking scenario (3.0.1) sets the budget to a configurable `chokingIncapacitationTicks` (suggested ~180 ticks ≈ 3 game-minutes at the current tick rate) and lets nature take its course. A future "rescued by Heimlich" packet can clear the `Incapacitated` state and restore `Alive` before the budget expires; this packet does not ship rescue, but the contract permits it.

### The guard helper

```csharp
public static class LifeStateGuard
{
    /// <summary>The single canonical "is this NPC eligible to be ticked" check. Equivalent to State == Alive.</summary>
    public static bool IsAlive(Entity npc)
        => npc.Has<LifeStateComponent>()
            && npc.Get<LifeStateComponent>().State == LifeState.Alive;

    /// <summary>True when biology continues but cognition stops. Used by physiology systems that should keep ticking after Incapacitated.</summary>
    public static bool IsBiologicallyTicking(Entity npc)
    {
        if (!npc.Has<LifeStateComponent>()) return true; // non-NPC entities pass through
        var s = npc.Get<LifeStateComponent>().State;
        return s == LifeState.Alive || s == LifeState.Incapacitated;
    }
}
```

The two-tier check distinguishes **cognitive systems** (skip Incapacitated and Deceased) from **physiology systems** (skip only Deceased; Incapacitated NPCs still digest, choke, fail to breathe). The guard table below specifies which systems use which check.

**Why a static helper, not an instance method.** The helper carries no state and is called from ~40 systems. A static keeps the call site terse (`if (!LifeStateGuard.IsAlive(npc)) continue;`) and avoids inflating every system's constructor. It's also faster — a virtual dispatch on a hot loop is real cost at 30+ NPCs at 60 FPS.

### Guard application table

Each system in the engine gets exactly one of three treatments. The guard line goes at the top of the per-NPC loop body, before any reads.

**Group A — `IsAlive` (skip Incapacitated and Deceased):** all cognitive / volitional / social systems.

| System | Treatment |
|:---|:---|
| `ActionSelectionSystem` | `if (!IsAlive(npc)) continue;` |
| `WillpowerSystem` | `IsAlive` |
| `DesireSystem` | `IsAlive` |
| `DriveDynamicsSystem` | `IsAlive` |
| `MoodSystem` | `IsAlive` |
| `BrainSystem` | `IsAlive` |
| `ScheduleSystem` | `IsAlive` |
| `WorkloadSystem` | `IsAlive` |
| `TaskGeneratorSystem` | `IsAlive` (assignment skips deceased candidates) |
| `StressSystem` | `IsAlive` |
| `SocialMaskSystem` | `IsAlive` |
| `MaskCrackSystem` | `IsAlive` |
| `Dialog/DialogContextDecisionSystem` | `IsAlive` |
| `Dialog/DialogFragmentRetrievalSystem` | `IsAlive` |
| `Dialog/DialogCalcifySystem` | `IsAlive` |
| `Coupling/LightingToDriveCouplingSystem` | `IsAlive` |
| `Coupling/SocialDriveAccumulator` | `IsAlive` |
| `Movement/IdleMovementSystem` | `IsAlive` |
| `Movement/StepAsideSystem` | `IsAlive` |
| `Movement/MovementSpeedModifierSystem` | `IsAlive` |
| `MovementSystem` | `IsAlive` (deceased NPCs do not move; the corpse stays where the body fell. WP-3.0.2 will introduce explicit corpse drag for player removal.) |
| `Movement/PathfindingTriggerSystem` | `IsAlive` |
| `Movement/FacingSystem` | `IsAlive` (the deceased's facing freezes at last value) |
| `RelationshipLifecycleSystem` | `IsAlive` (does not *iterate* deceased to update relationships, but relationship rows referencing them remain valid; do not remove the row) |
| `MemoryRecordingSystem` | **special** — does not gate the *bus subscription*; the deceased can be a participant in a candidate (their own death). Add the four new kinds to `IsPersistent` returning `true`. The handler still routes to per-pair / personal memory by the existing rules. |
| `Spatial/ProximityEventSystem` | `IsAlive` for the *acting* NPC; deceased NPCs still emit "left conversation range" events when the body is moved (WP-3.0.2 territory) but in this packet, deceased NPCs don't generate proximity events for themselves. |
| `Spatial/RoomMembershipSystem` | `IsAlive` for membership update; **the deceased's RoomMembershipComponent is preserved at the room they died in.** WP-3.0.2 manages corpse moves. |
| `Spatial/SpatialIndexSyncSystem` | `IsBiologicallyTicking` (the body still occupies space — the spatial index must still know where the corpse is so `RoomMembershipSystem` queries find it) |

**Group B — `IsBiologicallyTicking` (skip only Deceased):** physiology systems that legitimately continue while the NPC is Incapacitated. The choking NPC's stomach still churns the bolus; the digestion pipeline still runs; bladder still fills.

| System | Treatment |
|:---|:---|
| `EnergySystem` | `IsBiologicallyTicking` |
| `MetabolismSystem` | `IsBiologicallyTicking` |
| `BladderFillSystem` | `IsBiologicallyTicking` |
| `BladderSystem` | `IsBiologicallyTicking` (Incapacitated NPCs may void; that's a real thing) |
| `EsophagusSystem` | `IsBiologicallyTicking` (the bolus is still there) |
| `DigestionSystem` | `IsBiologicallyTicking` |
| `StomachComponent`-touching systems (none directly; via `DigestionSystem`) | n/a |
| `SmallIntestineSystem` | `IsBiologicallyTicking` |
| `LargeIntestineSystem` | `IsBiologicallyTicking` |
| `ColonSystem` | `IsBiologicallyTicking` |
| `RotSystem` | `IsBiologicallyTicking` (food on the deceased's desk continues to rot) |
| `BiologicalConditionSystem` | `IsBiologicallyTicking` |
| `FeedingSystem` | `IsAlive` (no autonomous eating while incapacitated) — Group A. Override above. |
| `DrinkingSystem` | `IsAlive` — Group A. Override above. |
| `SleepSystem` | `IsAlive` — Group A. Override above. |
| `UrinationSystem` | `IsAlive` for the *autonomous trigger*; the bladder may still void physiologically (handled in `BladderSystem`). |
| `DefecationSystem` | `IsAlive` for the autonomous trigger; same. |
| `Lighting/*` (`SunSystem`, `ApertureBeamSystem`, `IlluminationAccumulationSystem`, `LightSourceStateSystem`) | **no guard.** These iterate world geometry and lights, not NPCs. |
| `InteractionSystem` | `IsAlive` for the initiating NPC. The deceased can be a *target* of interaction (a witness checking on them) — that's WP-3.0.2's territory; in this packet the body is non-interactive. |

**Group C — `InvariantSystem`:** the invariant set differs for deceased entities. Do **not** assert `EnergyComponent.Energy > 0` on a deceased NPC; do not assert `BladderComponent.Fill <= 100` on an incapacitated NPC who has voided. The simplest move: `InvariantSystem` checks `IsAlive(npc)` and applies the live invariants only to those; deceased NPCs are checked against a smaller invariant set defined inline in `InvariantSystem` (`LifeStateComponent.State == Deceased`, `CauseOfDeathComponent` exists, `RoomMembershipComponent` unchanged since `DeathTick`). The packet introduces the deceased-invariant set; future packets extend it.

### `LifeStateInitializerSystem`

Spawn-time, PreUpdate phase, parallel to `StressInitializerSystem`. For each NPC entity that does not yet have `LifeStateComponent`:

```csharp
npc.Add(new LifeStateComponent {
    State = LifeState.Alive,
    LastTransitionTick = 0,
    IncapacitatedTickBudget = 0
});
```

Idempotent: re-running on an entity that already has the component is a no-op. Determinism: no RNG.

### `LifeStateTransitionSystem`

Cleanup phase, **after** `WorkloadSystem`, `MaskCrackSystem`, and `StressSystem` (so cognition has finished its tick before death is registered). The system holds a **single-tick request queue** — `LifeStateTransitionRequest { Guid NpcId, LifeState TargetState, CauseOfDeath Cause }`. Producers (the choking scenario in 3.0.1, the slip-and-fall in 3.0.3) push requests; the system drains them in deterministic order at end of tick.

The queue is the single-writer seam — analogous to `WillpowerEventQueue`, `IntendedActionComponent`, etc. **It is the only writer of `LifeStateComponent.State` and the only attacher of `CauseOfDeathComponent`.** Tests that need to "kill" an NPC do so by enqueuing a request, then ticking once.

```csharp
public sealed class LifeStateTransitionSystem
{
    public void RequestTransition(Guid npcId, LifeState target, CauseOfDeath cause)
    {
        // dedupe by npcId; later requests in the same tick win, with a deterministic warning
        // (a real future-proof loss case but rare; documented in WP)
    }

    public void Tick(...)
    {
        foreach (var req in queue.OrderBy(r => r.NpcId))   // deterministic
        {
            var npc = entityManager.Get(req.NpcId);
            if (npc == null) continue;
            if (!npc.Has<LifeStateComponent>()) continue;

            var current = npc.Get<LifeStateComponent>().State;

            // legal transitions: Alive -> Incapacitated -> Deceased; Alive -> Deceased (sudden death)
            if (current == LifeState.Deceased) continue;     // already done
            if (req.TargetState == LifeState.Alive && current == LifeState.Deceased) continue;  // no resurrection

            // emit cause-of-death candidate BEFORE flipping state, so MemoryRecordingSystem still sees Alive participants
            if (req.TargetState == LifeState.Deceased)
            {
                var witness = FindClosestWitness(npc);   // first NPC in conversation range; Guid.Empty if none
                var location = npc.Has<RoomMembershipComponent>()
                    ? npc.Get<RoomMembershipComponent>().RoomId
                    : Guid.Empty;

                narrativeEventBus.Emit(new NarrativeEventCandidate {
                    Kind = CauseToNarrativeKind(req.Cause),
                    Participants = witness == Guid.Empty
                        ? new[] { npc.Id }
                        : new[] { npc.Id, witness },
                    Tags = new[] { "death", req.Cause.ToString().ToLowerInvariant() },
                    Tick = clock.CurrentTick
                });

                npc.Add(new CauseOfDeathComponent {
                    Cause = req.Cause,
                    DeathTick = clock.CurrentTick,
                    WitnessedByNpcId = witness,
                    LocationRoomId = location
                });
            }

            npc.Set(new LifeStateComponent {
                State = req.TargetState,
                LastTransitionTick = clock.CurrentTick,
                IncapacitatedTickBudget = req.TargetState == LifeState.Incapacitated ? config.DefaultIncapacitatedTicks : 0
            });
        }
        queue.Clear();

        // separately: tick down IncapacitatedTickBudget for any Incapacitated NPC; if it reaches 0, enqueue a Deceased request for next tick
        // (using the cause carried from when Incapacitated was set — store it on the component as IncapacitationCause, see below)
    }
}
```

**Note on `IncapacitationCause`.** Add a fourth field to `LifeStateComponent`:

```csharp
public CauseOfDeath PendingDeathCause; // valid only while State == Incapacitated; carries the cause that will register if the budget runs out
```

This is what lets the choking system (3.0.1) say "Mark is choking, set Incapacitated for 180 ticks; if no rescue arrives, transition to Deceased(Choked)." The `LifeStateTransitionSystem` reads this field when the budget ticks to zero and enqueues a `Deceased` transition with `PendingDeathCause`.

### `CauseToNarrativeKind` mapping

```csharp
static NarrativeEventKind CauseToNarrativeKind(CauseOfDeath cause) => cause switch
{
    CauseOfDeath.Choked         => NarrativeEventKind.Choked,
    CauseOfDeath.SlippedAndFell => NarrativeEventKind.SlippedAndFell,
    CauseOfDeath.StarvedAlone   => NarrativeEventKind.StarvedAlone,
    _                           => NarrativeEventKind.Died,
};
```

### `MemoryRecordingSystem.IsPersistent` extension

```csharp
// add four new cases:
case NarrativeEventKind.Choked:         return true;
case NarrativeEventKind.SlippedAndFell: return true;
case NarrativeEventKind.StarvedAlone:   return true;
case NarrativeEventKind.Died:           return true;
```

Death is always persistent. (3.0.2 will pile on +20 stress for witnesses; this packet just records.)

### `FindClosestWitness`

Iterate NPCs in conversation range (`ProximityComponent.ConversationRange`), skip self, skip non-Alive (a deceased can't witness another's death — they're already gone, and an incapacitated one can't form memory). Return `Guid.Empty` if none. Deterministic order (ascending `EntityIntId`).

### SimConfig additions

```jsonc
{
  "lifeState": {
    "defaultIncapacitatedTicks": 180,
    "incapacitatedAllowsBladderVoid": true,
    "deceasedFreezesPosition": true,
    "emitDeathInvariantOnTransition": true
  }
}
```

Most are placeholders for later packets (3.0.1 will tune `defaultIncapacitatedTicks` per cause; 3.0.2 will use `deceasedFreezesPosition` to decide if the corpse blocks pathfinding). Default values let this packet's tests run.

### Determinism

`LifeStateTransitionSystem.queue` drained in `OrderBy(NpcId)`. `FindClosestWitness` iterates in `EntityIntId` order. `IncapacitatedTickBudget` is a long, decremented exactly once per tick. No RNG, no wall-clock. The 5000-tick byte-identical test confirms.

### Tests

Focused on the substrate, not on production scenarios. Scenarios test in 3.0.1+.

- `LifeStateComponentTests.cs` — construction, equality, default-value clamping.
- `CauseOfDeathComponentTests.cs` — same.
- `LifeStateInitializerSystemTests.cs` — every NPC at boot has `LifeStateComponent.State == Alive`.
- `LifeStateTransitionSystemTransitionTests.cs` — `RequestTransition(npc, Deceased, Choked)` + tick → state is `Deceased`, `CauseOfDeathComponent` attached with correct cause/tick/location, no resurrection request succeeds.
- `LifeStateTransitionSystemIncapacitationTests.cs` — Alive → Incapacitated → (budget exhausts) → Deceased over correct number of ticks; `PendingDeathCause` carried through.
- `LifeStateTransitionSystemWitnessTests.cs` — NPC dies in conversation range of another live NPC: `WitnessedByNpcId` set; in isolation: `Guid.Empty`; with only an Incapacitated nearby: `Guid.Empty`.
- `LifeStateTransitionSystemNarrativeEmitTests.cs` — `Choked` request → narrative bus emits `NarrativeEventKind.Choked` with the deceased as participant 0 and witness (if any) as participant 1, with `tags=["death","choked"]`. Same for `SlippedAndFell`, `StarvedAlone`. `Unknown` cause → emits `Died`.
- `LifeStateGuardTests.cs` — `IsAlive` returns true only for `Alive`; `IsBiologicallyTicking` returns true for `Alive` and `Incapacitated`; both return true for non-NPC entities lacking the component (the "passes through" semantics).
- `LifeStateGuardSystemIntegrationTests.cs` — for each Group A system, assert that ticking with one Deceased NPC and one Alive NPC iterates only the Alive one (use a probe — a counter on a component, or a check that drives drift on Alive but not Deceased over 100 ticks).
- `LifeStateGuardPhysiologyTests.cs` — for each Group B system, assert that an Incapacitated NPC continues to advance the relevant physiology component (energy decays, bladder fills, bolus moves through the esophagus, etc.).
- `LifeStateMemoryPersistenceTests.cs` — a witnessed Choked event lands in `RelationshipMemoryComponent` between deceased and witness with `Persistent = true`, in `PersonalMemoryComponent` of the witness with `Persistent = true`. The deceased's personal memory remains accessible (engine doesn't delete history).
- `LifeStateRelationshipPreservationTests.cs` — a relationship row that references a deceased NPC remains in the entity manager and remains queryable; the lifecycle system does not garbage-collect it.
- `LifeStateMovementFreezeTests.cs` — a Deceased NPC's `PositionComponent` does not change across 100 ticks even if `IntendedAction` was `Approach(target)` at time of death (intent stays as a stale write but movement doesn't act on it).
- `LifeStateInvariantTests.cs` — `InvariantSystem` does not assert `EnergyComponent.Energy > 0` on a Deceased NPC; does assert the deceased-invariant set (state == Deceased, cause attached, room preserved).
- `LifeStateDeterminismTests.cs` — 5000-tick run, two seeded worlds where one NPC dies at tick 1000 (deterministic request injection): byte-identical state across both runs.
- `LifeStateNonResurrectionTests.cs` — request `(deceased, Alive, Unknown)` is silently dropped; state stays Deceased.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/LifeStateComponent.cs` | `LifeState` enum + component struct (`State`, `LastTransitionTick`, `IncapacitatedTickBudget`, `PendingDeathCause`). |
| code | `APIFramework/Components/CauseOfDeathComponent.cs` | `CauseOfDeath` enum + component struct (`Cause`, `DeathTick`, `WitnessedByNpcId`, `LocationRoomId`). |
| code | `APIFramework/Systems/LifeState/LifeStateGuard.cs` | Static helper with `IsAlive` and `IsBiologicallyTicking`. |
| code | `APIFramework/Systems/LifeState/LifeStateInitializerSystem.cs` | Spawn-time attachment. |
| code | `APIFramework/Systems/LifeState/LifeStateTransitionSystem.cs` | Single-writer state machine; queue-drained; emits cause-of-death narrative. |
| code | `APIFramework/Systems/LifeState/LifeStateTransitionRequest.cs` | Internal request record. |
| code | `APIFramework/Systems/Narrative/NarrativeEventKind.cs` (modified) | Add `Choked`, `SlippedAndFell`, `StarvedAlone`, `Died` at end of enum. **Additive only.** |
| code | `APIFramework/Systems/MemoryRecordingSystem.cs` (modified) | Add four new kinds to `IsPersistent` switch (all `true`). |
| code | every Group A system listed above (~30 files, modified) | Add `if (!LifeStateGuard.IsAlive(npc)) continue;` near top of per-NPC loop. ~1–3 lines per file. |
| code | every Group B system listed above (~12 files, modified) | Add `if (!LifeStateGuard.IsBiologicallyTicking(npc)) continue;` similarly. |
| code | `APIFramework/Systems/InvariantSystem.cs` (modified) | Branch: deceased NPCs checked against deceased-invariant set; live NPCs against the existing live set. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register `LifeStateInitializerSystem` (PreUpdate) and `LifeStateTransitionSystem` (Cleanup, after WorkloadSystem and MaskCrackSystem). |
| code | `APIFramework/Config/SimConfig.cs` (modified) | `LifeStateConfig` class + property. |
| code | `SimConfig.json` (modified) | `lifeState` section. |
| code | `APIFramework.Tests/Components/LifeStateComponentTests.cs` | Construction, defaults. |
| code | `APIFramework.Tests/Components/CauseOfDeathComponentTests.cs` | Construction, defaults. |
| code | `APIFramework.Tests/Systems/LifeState/LifeStateInitializerSystemTests.cs` | Spawn attaches. |
| code | `APIFramework.Tests/Systems/LifeState/LifeStateTransitionSystemTransitionTests.cs` | Direct-to-Deceased path. |
| code | `APIFramework.Tests/Systems/LifeState/LifeStateTransitionSystemIncapacitationTests.cs` | Two-step Alive → Incap → Deceased. |
| code | `APIFramework.Tests/Systems/LifeState/LifeStateTransitionSystemWitnessTests.cs` | Witness selection rules. |
| code | `APIFramework.Tests/Systems/LifeState/LifeStateTransitionSystemNarrativeEmitTests.cs` | Cause-to-NarrativeKind mapping. |
| code | `APIFramework.Tests/Systems/LifeState/LifeStateGuardTests.cs` | Helper unit tests. |
| code | `APIFramework.Tests/Integration/LifeStateGuardSystemIntegrationTests.cs` | Group A systems skip Deceased. |
| code | `APIFramework.Tests/Integration/LifeStateGuardPhysiologyTests.cs` | Group B systems still tick Incapacitated. |
| code | `APIFramework.Tests/Integration/LifeStateMemoryPersistenceTests.cs` | Witnessed death lands persistent in per-pair + personal memory. |
| code | `APIFramework.Tests/Integration/LifeStateRelationshipPreservationTests.cs` | Relationship row survives. |
| code | `APIFramework.Tests/Integration/LifeStateMovementFreezeTests.cs` | Position doesn't drift after Deceased. |
| code | `APIFramework.Tests/Integration/LifeStateInvariantTests.cs` | InvariantSystem branches correctly. |
| code | `APIFramework.Tests/Determinism/LifeStateDeterminismTests.cs` | 5000-tick byte-identical with one mid-run death. |
| code | `APIFramework.Tests/Systems/LifeState/LifeStateNonResurrectionTests.cs` | Resurrection requests dropped. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.0.0.md` | Completion note. SimConfig defaults; the per-system guard treatment table actually applied (one row per system file modified); any deviations from the spec table; whether `IncapacitatedAllowsBladderVoid` is wired or stubbed; engine fact-sheet regen check (`ECSCli ai describe` should now list `LifeStateInitializerSystem` and `LifeStateTransitionSystem`); `dotnet test` summary. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `LifeStateComponent`, `CauseOfDeathComponent`, `LifeState`, `CauseOfDeath`, the four new `NarrativeEventKind` values compile, instantiate, equality round-trip. | unit-test |
| AT-02 | At world bootstrap, every NPC has `LifeStateComponent.State == Alive`, `LastTransitionTick == 0`, `IncapacitatedTickBudget == 0`. | unit-test |
| AT-03 | `LifeStateTransitionSystem.RequestTransition(npc, Deceased, Choked)` + one tick → `State == Deceased`, `CauseOfDeathComponent` attached with `Cause == Choked`, `DeathTick == clock.CurrentTick`, `LocationRoomId == npc's RoomMembershipComponent.RoomId`. | unit-test |
| AT-04 | `RequestTransition(npc, Incapacitated, Choked)` + 180 ticks (with `defaultIncapacitatedTicks=180`) → at tick 180, `State == Deceased`, `Cause == Choked`, `LastTransitionTick` updates twice. | unit-test |
| AT-05 | Witness selection: deceased has one NPC in conversation range — `WitnessedByNpcId == that NPC's id`. Deceased has nobody — `Guid.Empty`. Deceased has only an Incapacitated NPC nearby — `Guid.Empty`. | unit-test |
| AT-06 | Narrative emit: each of `Choked`, `SlippedAndFell`, `StarvedAlone`, `Unknown(→Died)` causes the bus to emit the corresponding `NarrativeEventKind`, with the deceased as participant 0 and witness (if any) as participant 1, `tags=["death", causeName]`. The emit happens **before** state flips to `Deceased` (subscribers see Alive participants). | unit-test |
| AT-07 | `MemoryRecordingSystem`: a witnessed `Choked` event with two participants lands in `RelationshipMemoryComponent` between them and in the witness's `PersonalMemoryComponent`, with `Persistent = true`. The deceased's `PersonalMemoryComponent` retains its prior contents (no garbage collection). | integration-test |
| AT-08 | Group A guard: ticking 100 times with one Deceased NPC and one Alive NPC, `DriveDynamicsSystem` advances drives only on the Alive NPC; `WillpowerSystem` regenerates only on the Alive NPC; `ActionSelectionSystem` writes `IntendedAction` only on the Alive NPC. (Sample three Group A systems; full coverage by the per-system guard placement.) | integration-test |
| AT-09 | Group B guard: ticking 100 times with one Incapacitated NPC, `EnergySystem` decays energy, `MetabolismSystem` advances metabolism, `EsophagusSystem` advances any in-transit bolus. | integration-test |
| AT-10 | Group A vs B distinction: an Incapacitated NPC's drives do **not** drift (`DriveDynamicsSystem` is Group A); their bladder fills (`BladderFillSystem` is Group B). | integration-test |
| AT-11 | Movement freeze: a Deceased NPC's `PositionComponent` is byte-identical at tick T+100 vs tick T (death tick). | integration-test |
| AT-12 | Relationship preservation: a relationship row referencing a deceased NPC is still in the entity manager 1000 ticks later; queries against it succeed. | integration-test |
| AT-13 | Invariant system: `Deceased` NPCs do not fail the live-NPC invariants; they pass the deceased-invariant set; live NPCs continue to be checked against the live set. | integration-test |
| AT-14 | Non-resurrection: a `RequestTransition(deceased, Alive, Unknown)` is silently dropped; state stays `Deceased`; `LastTransitionTick` does not change. | unit-test |
| AT-15 | Determinism: 5000-tick run, two seeded worlds with the same world spec and one deterministic mid-run death request at tick 1000: byte-identical state at every recorded interval and at tick 5000. | unit-test |
| AT-16 | `archetype-*.json` files unchanged (no per-archetype life-state tuning at v0.1). The cast bible's 10 archetypes spawn with default state. | unit-test |
| AT-17 | All Phase 0, Phase 1, and Phase 2 tests stay green. **Specifically**: `WorkloadDeterminismTests`, `MaskCrackSystemTests`, `StressSystemSourceTests`, `MemoryRecordingPersistenceTests`, the action-selection 5000-tick determinism test. The guard pattern must not break any extant determinism test. | regression |
| AT-18 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-19 | `dotnet test ECSSimulation.sln` — all green, no exclusions. | build + unit-test |
| AT-20 | `ECSCli ai describe --out engine-fact-sheet.md` regenerates with the two new systems listed and the four new `NarrativeEventKind` enum values; the `FactSheetStalenessTests` (WP-2.9.A) detects the diff and is regenerated as part of the packet. | build + unit-test |

---

## Followups (not in scope)

- **WP-3.0.1** — Choking-on-food scenario. The first concrete consumer of `RequestTransition`. Bolus-too-big × low-energy × no-proximity-help → enqueue `(npc, Incapacitated, Choked)`. The substrate this packet ships powers it directly.
- **WP-3.0.2** — `CorpseComponent`, body persistence, bereavement amplification (+20 stress on witnesses; the Cubicle-12-Mark dynamic generated from a real death).
- **WP-3.0.3** — Slip-and-fall (uses `SlippedAndFell`), locked-in-and-starved (uses `StarvedAlone`).
- **v0.5 schema bump** — `WorldStateDto.npcs[].lifeState` field. Own packet, post-3.0.4.
- **Rescue mechanic** — clearing `Incapacitated` back to `Alive` before the budget expires (Heimlich; CPR; an NPC who walks in and notices). Hooks already exist (`RequestTransition(npc, Alive, ...)` is silently dropped from Deceased but works from Incapacitated). The actual *trigger* — what makes another NPC notice and act — is its own packet.
- **Murder / homicide** — `Killed` cause, requires consent + intent surfaces that don't exist yet.
- **Suicide** — own design pass; mature theme; deferred until the bibles take a position.
- **Mass-casualty / disaster events** — fire, gas leak, plague-week deaths. The substrate handles N deaths in one tick (the queue is drained per-tick); a future packet ships the trigger.
- **Per-archetype mortality biasing** — the Old Hand may have higher choke risk; the Recovering may have a relapse death. Tunable in archetype JSON post-3.0.3.
- **Player-visible death** — UI surface for the moment a death happens. Phase 3.1.E (UX/UI bible-driven).
- **Save/load round-trip** — already covered automatically once the v0.5 schema bump lands, because deceased entities serialize via the standard `WorldStateDto` round-trip.
