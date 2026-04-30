# WP-3.2.4 — Rescue Mechanic (Heimlich, CPR, Door-Unlock)

> **DO NOT DISPATCH UNTIL ALL OF PHASE 3.1.x IS MERGED.**
> The most-anticipated followup from WP-3.0.1 (choking) and 3.0.3 (slip-and-fall, locked-in starvation). Rescue closes the loop the death packets opened: another NPC notices, intervenes, and saves a life.

**Tier:** Sonnet
**Depends on:** WP-3.0.0 (LifeStateTransitionSystem; recovery path Alive ← Incapacitated already in contract), WP-3.0.1 (choking), WP-3.0.3 (lockout), Phase 0/1/2 (action selection, proximity, memory)
**Parallel-safe with:** WP-3.2.2 (physics), WP-3.2.3 (chores)
**Timebox:** 130 minutes
**Budget:** $0.55

---

## Goal

WP-3.0.0's `LifeStateTransitionSystem` accepts `RequestTransition(npcId, Alive, Unknown)` from `Incapacitated` — the recovery path is already in the contract. Nothing in Phase 3.0.x ever called it. WP-3.0.1's choking deaths happen because no rescue exists. This packet ships the rescue.

After this packet:

- Witnesses (NPCs in proximity / awareness range of an incapacitated NPC) compute a `RescueIntent` candidate in their action-selection: "go intervene." Computation gates on archetype-rescue-bias, current willpower, current drive state, distance.
- A successful rescue calls `LifeStateTransitionSystem.RequestTransition(npc, Alive, Unknown)`, clears the choke / faint / lockout state, emits a `RescuePerformed` narrative event.
- Failed rescue (witness arrives too late, fails-rescue-roll for choking, can't unlock door for lockout) → death cascade fires as standard.
- Rescue events are **persistent** memory entries — rescuer and rescued form a strong relationship bond; office talks about it for a game-week.
- Scoped to three rescue kinds at v0.1:
  - **Heimlich** — choking NPC; witness performs Heimlich; success rate per archetype.
  - **CPR** — collapsed NPC (placeholder hook for future non-fatal-slip variant).
  - **Door-unlock** — locked-in NPC noticed by another NPC; the noticer goes to unlock the door.

This is the world's first compassion surface.

---

## Reference files

- `docs/c2-infrastructure/work-packets/_completed/WP-3.0.0.md` — recovery path contract: `RequestTransition(npc, Alive, Unknown)` from Incapacitated clears state.
- `docs/c2-infrastructure/work-packets/_completed/WP-3.0.1.md` — choking. v0.1 had no rescue.
- `docs/c2-infrastructure/work-packets/_completed/WP-3.0.3.md` — lockout starvation.
- `docs/c2-content/cast-bible.md` — archetypes; rescue-bias varies. Newbie panics and helps; Cynic stoically helps; Hermit avoids; Founder's Nephew calls someone else; Climber rescues for visibility.
- `APIFramework/Systems/ActionSelectionSystem.cs` — new candidate source: `RescueIntent`.
- `APIFramework/Systems/LifeState/LifeStateTransitionSystem.cs` — `RequestTransition(npc, Alive, Unknown)` already supports the upgrade.
- `APIFramework/Systems/LifeState/ChokingDetectionSystem.cs`, `ChokingCleanupSystem.cs` — choke path; rescue clears `IsChokingTag` + `ChokingComponent`.
- `APIFramework/Mutation/IWorldMutationApi.cs` — `DetachObstacle` for door unlock.

---

## Non-goals

- Do **not** ship player-driven rescue at v0.1. Rescue is NPC-autonomous.
- Do **not** ship rescue from non-Incapacitated states (depression, addiction recovery support). Future.
- Do **not** ship multi-NPC coordinated rescue. v0.1 single-rescuer.
- Do **not** modify Phase 3.0.x packets. Recovery path contract preserved as-is.
- Do **not** retry, recurse, or self-heal.

---

## Design notes

### `RescueIntentSystem`

Cleanup phase, after `ActionSelectionSystem`. For each Alive NPC:

```csharp
foreach (var npc in em.Query<LifeStateComponent>().OrderBy(e => e.Id))
{
    if (!LifeStateGuard.IsAlive(npc)) continue;

    var inNeed = em.Query<LifeStateComponent>()
        .Where(other => other.Get<LifeStateComponent>().State == LifeState.Incapacitated)
        .Where(other => InAwarenessRange(npc, other))
        .OrderBy(other => other.Id);

    foreach (var victim in inNeed)
    {
        var archetype = npc.Get<NpcArchetypeComponent>().ArchetypeId;
        var rescueBias = archetypeRescueBias[archetype];
        var distance = Distance(npc, victim);
        var willpower = npc.Get<WillpowerComponent>().Current;
        var stress = npc.Get<StressComponent>().AcuteLevel;

        var rescueScore = rescueBias - (distance * 0.05f) + (willpower * 0.3f) - (stress * 0.005f);

        if (rescueScore > config.RescueThreshold)
        {
            actionSelectionSystem.OverrideIntent(npc, new IntendedAction {
                Kind = IntendedActionKind.Rescue,
                TargetEntityId = victim.Id,
                Context = DialogContextValue.None
            });
            break;
        }
    }
}
```

`IntendedActionKind.Rescue` is a new additive enum value.

### `RescueExecutionSystem`

Cleanup phase, after `RescueIntentSystem`. For each NPC with `IntendedAction.Kind == Rescue`:

```csharp
var npc = em.Get(npcId);
var victim = em.Get(npc.Get<IntendedActionComponent>().TargetEntityId);

if (!InConversationRange(npc, victim)) continue;

var rescueKind = DetermineRescueKind(victim);
var successProbability = ComputeSuccessProbability(npc, victim, rescueKind);

var roll = rng.NextFloat(seed: HashTuple(npc.Id, victim.Id, clock.CurrentTick));
if (roll < successProbability)
    PerformRescue(npc, victim, rescueKind);
else
    EmitRescueAttemptFailedNarrative(npc, victim, rescueKind);
```

`PerformRescue`:

```csharp
void PerformRescue(Entity rescuer, Entity victim, RescueKind kind)
{
    switch (kind)
    {
        case RescueKind.Heimlich:
            victim.Remove<IsChokingTag>();
            victim.Remove<ChokingComponent>();
            transitionSystem.RequestTransition(victim.Id, LifeState.Alive, CauseOfDeath.Unknown);
            break;
        case RescueKind.CPR:
            transitionSystem.RequestTransition(victim.Id, LifeState.Alive, CauseOfDeath.Unknown);
            break;
        case RescueKind.DoorUnlock:
            var lockedDoor = FindLockedDoorForVictim(victim);
            mutationApi.DetachObstacle(lockedDoor.Id);
            break;
    }

    narrativeBus.Emit(new NarrativeEventCandidate {
        Kind = NarrativeEventKind.RescuePerformed,
        Participants = new[] { victim.Id, rescuer.Id },
        Tags = new[] { "rescue", kind.ToString().ToLowerInvariant() },
        Tick = clock.CurrentTick
    });
}
```

### Per-archetype rescue bias

```jsonc
{
  "schemaVersion": "0.1.0",
  "archetypeRescueBias": [
    {"archetype": "the-newbie",          "bias": 0.85},
    {"archetype": "the-old-hand",        "bias": 0.80},
    {"archetype": "the-cynic",           "bias": 0.55},
    {"archetype": "the-climber",         "bias": 0.50},
    {"archetype": "the-recovering",      "bias": 0.65},
    {"archetype": "the-vent",            "bias": 0.40},
    {"archetype": "the-hermit",          "bias": 0.30},
    {"archetype": "the-founders-nephew", "bias": 0.10},
    {"archetype": "the-affair",          "bias": 0.45},
    {"archetype": "the-crush",           "bias": 0.55}
  ]
}
```

### Per-archetype rescue success probability

```csharp
float ComputeSuccessProbability(Entity rescuer, Entity victim, RescueKind kind)
{
    float baseRate = kind switch {
        RescueKind.Heimlich => 0.65f,
        RescueKind.CPR => 0.30f,
        RescueKind.DoorUnlock => 0.95f,
        _ => 0.50f
    };
    var rescuerArchetype = rescuer.Get<NpcArchetypeComponent>().ArchetypeId;
    var competenceBonus = archetypeRescueCompetence[rescuerArchetype][kind];
    return MathF.Clamp(baseRate + competenceBonus, 0f, 0.99f);
}
```

### New narrative kinds

- `RescuePerformed` — persistent: true. Strong positive memory.
- `RescueAttempted` — persistent: false.
- `RescueFailed` — persistent: true. Failed rescue with death; rescuer carries this.

### SimConfig additions

```jsonc
{
  "rescue": {
    "rescueThreshold":           0.40,
    "awarenessRangeForRescue":   3.0,
    "rescueIntentBaseWeight":    0.85,
    "heimlichBaseSuccessRate":   0.65,
    "cprBaseSuccessRate":        0.30,
    "doorUnlockBaseSuccessRate": 0.95
  }
}
```

### Tests

- `RescueIntentSystemTests.cs`, `RescueIntentBiasTests.cs`.
- `RescueExecutionHeimlichSuccessTests.cs`, `RescueExecutionHeimlichFailTests.cs`, `RescueExecutionDoorUnlockTests.cs`.
- `RescueRelationshipBondTests.cs`, `RescueLowWillpowerTests.cs`, `RescueHighStressTests.cs`.
- `RescueDeterminismTests.cs` — 5000 ticks deterministic.
- `RescueAcceptanceBiasJsonTests.cs`.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Systems/Rescue/RescueIntentSystem.cs` | Intent emission. |
| code | `APIFramework/Systems/Rescue/RescueExecutionSystem.cs` | Execution + success roll. |
| code | `APIFramework/Systems/Rescue/RescueKind.cs` | Enum. |
| code | `APIFramework/Systems/Rescue/ArchetypeRescueBiasCatalog.cs` | ScriptableObject pattern. |
| code | `APIFramework/Components/IntendedActionComponent.cs` (modified) | Add `Rescue` to `IntendedActionKind`. |
| code | `APIFramework/Systems/Narrative/NarrativeEventKind.cs` (modified) | Add RescuePerformed/Attempted/Failed. |
| code | `APIFramework/Systems/MemoryRecordingSystem.cs` (modified) | Persistent flags. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register rescue systems. |
| code | `APIFramework/Config/SimConfig.cs` (modified) | `RescueConfig`. |
| config | `SimConfig.json` (modified) | `rescue` section. |
| data | `docs/c2-content/rescue/archetype-rescue-bias.json` | Per-archetype rescue likelihood + competence. |
| test | (~10 test files) | Comprehensive coverage. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.2.4.md` | Completion note. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `IntendedActionKind.Rescue`, `RescueKind`, new narrative kinds compile. | unit-test |
| AT-02 | Alive NPC in awareness range of Incapacitated → `RescueIntentSystem` emits Rescue intent (above-threshold archetype). | integration-test |
| AT-03 | Below-threshold archetype (FoundersNephew) → no intent emitted. | integration-test |
| AT-04 | Rescue intent → `RescueExecutionSystem` rolls success; on success, choking NPC's IsChokingTag cleared and `RequestTransition(npc, Alive, Unknown)` called. | integration-test |
| AT-05 | On failure, victim continues toward death; `RescueAttempted` narrative emitted. | integration-test |
| AT-06 | Locked-in NPC + nearby rescuer → door unlocked via `IWorldMutationApi.DetachObstacle`. | integration-test |
| AT-07 | Successful rescue → `RescuePerformed` persistent narrative; rescuer's and rescued's memory has strong positive entry. | integration-test |
| AT-08 | Low-willpower rescuer (below threshold) → no intent. | integration-test |
| AT-09 | High-stress rescuer (above skip threshold) → no intent. | integration-test |
| AT-10 | Determinism: 5000 ticks deterministic rescue scenarios: byte-identical state. | integration-test |
| AT-11 | `archetype-rescue-bias.json` loads; all cast-bible archetypes covered. | unit-test |
| AT-12 | All Phase 0/1/2/3.0.x/3.1.x and prior Wave 1/2 of 3.2.x tests stay green. | regression |
| AT-13 | `dotnet build` warning count = 0; `dotnet test` all green. | build + test |

---

## Followups (not in scope)

- Player-driven rescue. Click an NPC and tell them to rescue. UX bible Q6 (direct intervention) — currently leaning environmental-only at v0.1.
- Multi-NPC coordinated rescue. Future emergent.
- Rescue from depression / addiction. Non-Incapacitated states. Future.
- Failed-rescue trauma. Rescuer carries persistent stress and grief. v0.1 records via narrative; future may add specific tags.
- Rescue training. Per-NPC competence improvement. Future progression.
- Bystander effect. Multiple witnesses → fewer interventions. Future polish.
- Hero archetype emergence. Future content.
- Rescue from fire / smoke. Couples to future disaster packets.
