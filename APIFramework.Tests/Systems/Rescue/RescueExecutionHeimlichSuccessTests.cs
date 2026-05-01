using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Rescue;
using APIFramework.Systems.Spatial;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.Rescue;

/// <summary>
/// AT-04: Rescue intent → RescueExecutionSystem rolls success; on success, choking NPC's
/// IsChokingTag cleared and RequestTransition(npc, Alive, Unknown) called.
/// AT-07: Successful rescue → RescuePerformed persistent narrative emitted.
/// </summary>
public class RescueExecutionHeimlichSuccessTests
{
    private static RescueConfig Cfg() => new()
    {
        HeimlichBaseSuccessRate   = 1.00f, // guaranteed success for these tests
        CprBaseSuccessRate        = 1.00f,
        DoorUnlockBaseSuccessRate = 1.00f,
    };

    private static ArchetypeRescueBiasCatalog EmptyCatalog()
    {
        var path = System.IO.Path.GetTempFileName();
        System.IO.File.WriteAllText(path, @"{
            ""schemaVersion"": ""0.1.0"",
            ""archetypeRescueBias"": [
                { ""archetype"": ""the-newbie"", ""bias"": 0.85,
                  ""heimlichCompetence"": 0.0, ""cprCompetence"": 0.0, ""doorUnlockCompetence"": 0.0 }
            ]
        }");
        return ArchetypeRescueBiasCatalog.LoadFromFile(path);
    }

    private (
        EntityManager em,
        NarrativeEventBus bus,
        SimulationClock clock,
        LifeStateTransitionSystem transitions,
        IWorldMutationApi mutationApi,
        Entity rescuer,
        Entity victim)
    Build()
    {
        var em     = new EntityManager();
        var bus    = new NarrativeEventBus();
        var clock  = new SimulationClock();
        var config = new SimConfig { LifeState = new LifeStateConfig { DefaultIncapacitatedTicks = 90 } };
        var transitions = new LifeStateTransitionSystem(bus, em, clock, config);

        var structBus  = new StructuralChangeBus();
        var mutationApi = new WorldMutationApi(em, structBus);

        // Rescuer — in conversation range of victim
        var rescuer = em.CreateEntity();
        rescuer.Add(new NpcTag());
        rescuer.Add(new LifeStateComponent { State = LS.Alive });
        rescuer.Add(new PositionComponent { X = 0f, Z = 0f });
        rescuer.Add(new ProximityComponent { ConversationRangeTiles = 2 });
        rescuer.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });

        // Victim — choking, incapacitated, within conversation range
        var victim = em.CreateEntity();
        victim.Add(new NpcTag());
        victim.Add(new LifeStateComponent
        {
            State = LS.Incapacitated,
            IncapacitatedTickBudget = 90,
            PendingDeathCause = CauseOfDeath.Choked,
        });
        victim.Add(new PositionComponent { X = 1f, Z = 0f });
        victim.Add(new IsChokingTag());
        victim.Add(new ChokingComponent { BolusSize = 0.9f, RemainingTicks = 90 });

        // Write the rescue intent directly (as RescueIntentSystem would)
        int victimId = BitConverter.ToInt32(victim.Id.ToByteArray(), 0);
        rescuer.Add(new IntendedActionComponent(
            Kind:          IntendedActionKind.Rescue,
            TargetEntityId: victimId,
            Context:       DialogContextValue.None,
            IntensityHint: 80));

        return (em, bus, clock, transitions, mutationApi, rescuer, victim);
    }

    // ── AT-04: IsChokingTag cleared on success ───────────────────────────────

    [Fact]
    public void AT04_HeimlichSuccess_ClearsIsChokingTag()
    {
        var (em, bus, clock, transitions, mutationApi, rescuer, victim) = Build();
        var sys = new RescueExecutionSystem(EmptyCatalog(), Cfg(), transitions, mutationApi, bus, clock, new SeededRandom(0));
        sys.Update(em, 1f);
        transitions.Update(em, 1f); // drain queue

        Assert.False(victim.Has<IsChokingTag>());
    }

    [Fact]
    public void AT04_HeimlichSuccess_ClearsChokingComponent()
    {
        var (em, bus, clock, transitions, mutationApi, rescuer, victim) = Build();
        var sys = new RescueExecutionSystem(EmptyCatalog(), Cfg(), transitions, mutationApi, bus, clock, new SeededRandom(0));
        sys.Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.False(victim.Has<ChokingComponent>());
    }

    [Fact]
    public void AT04_HeimlichSuccess_VictimTransitionsToAlive()
    {
        var (em, bus, clock, transitions, mutationApi, rescuer, victim) = Build();
        var sys = new RescueExecutionSystem(EmptyCatalog(), Cfg(), transitions, mutationApi, bus, clock, new SeededRandom(0));
        sys.Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.Equal(LS.Alive, victim.Get<LifeStateComponent>().State);
    }

    // ── AT-07: RescuePerformed narrative emitted ─────────────────────────────

    [Fact]
    public void AT07_HeimlichSuccess_EmitsRescuePerformedNarrative()
    {
        var (em, bus, clock, transitions, mutationApi, rescuer, victim) = Build();

        var events = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += events.Add;

        var sys = new RescueExecutionSystem(EmptyCatalog(), Cfg(), transitions, mutationApi, bus, clock, new SeededRandom(0));
        sys.Update(em, 1f);

        Assert.Contains(events, e => e.Kind == NarrativeEventKind.RescuePerformed);
    }

    [Fact]
    public void AT07_RescuePerformed_IsPersistentInMemory()
    {
        Assert.True(APIFramework.Systems.MemoryRecordingSystem.IsPersistent(NarrativeEventKind.RescuePerformed));
    }
}
