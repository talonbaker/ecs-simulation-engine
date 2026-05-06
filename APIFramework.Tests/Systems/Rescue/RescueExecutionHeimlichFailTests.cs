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
/// AT-05: On failure, victim continues toward death; RescueAttempted narrative emitted.
/// </summary>
public class RescueExecutionHeimlichFailTests
{
    private static RescueConfig ZeroSuccessRate() => new()
    {
        HeimlichBaseSuccessRate   = 0.00f, // guaranteed fail
        CprBaseSuccessRate        = 0.00f,
        DoorUnlockBaseSuccessRate = 0.00f,
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
    Build(int incapacitatedBudget = 50)
    {
        var em     = new EntityManager();
        var bus    = new NarrativeEventBus();
        var clock  = new SimulationClock();
        var config = new SimConfig { LifeState = new LifeStateConfig { DefaultIncapacitatedTicks = 90 } };
        var transitions = new LifeStateTransitionSystem(bus, em, clock, config);

        var structBus   = new StructuralChangeBus();
        var mutationApi = new WorldMutationApi(em, structBus);

        var rescuer = em.CreateEntity();
        rescuer.Add(new NpcTag());
        rescuer.Add(new LifeStateComponent { State = LS.Alive });
        rescuer.Add(new PositionComponent { X = 0f, Z = 0f });
        rescuer.Add(new ProximityComponent { ConversationRangeTiles = 2 });
        rescuer.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });

        var victim = em.CreateEntity();
        victim.Add(new NpcTag());
        victim.Add(new LifeStateComponent
        {
            State = LS.Incapacitated,
            IncapacitatedTickBudget = incapacitatedBudget,
            PendingDeathCause = CauseOfDeath.Choked,
        });
        victim.Add(new PositionComponent { X = 1f, Z = 0f });
        victim.Add(new IsChokingTag());

        int victimId = BitConverter.ToInt32(victim.Id.ToByteArray(), 0);
        rescuer.Add(new IntendedActionComponent(
            Kind:           IntendedActionKind.Rescue,
            TargetEntityId: victimId,
            Context:        DialogContextValue.None,
            IntensityHint:  80));

        return (em, bus, clock, transitions, mutationApi, rescuer, victim);
    }

    // ── AT-05: Victim still Incapacitated (not rescued) on failure ───────────

    [Fact]
    public void AT05_FailedRescue_VictimRemainsIncapacitated()
    {
        var (em, bus, clock, transitions, mutationApi, rescuer, victim) = Build();
        var sys = new RescueExecutionSystem(EmptyCatalog(), ZeroSuccessRate(), transitions, mutationApi, bus, clock, new SeededRandom(0));
        sys.Update(em, 1f);

        Assert.Equal(LS.Incapacitated, victim.Get<LifeStateComponent>().State);
    }

    [Fact]
    public void AT05_FailedRescue_IsChokingTagRetained()
    {
        var (em, bus, clock, transitions, mutationApi, rescuer, victim) = Build();
        var sys = new RescueExecutionSystem(EmptyCatalog(), ZeroSuccessRate(), transitions, mutationApi, bus, clock, new SeededRandom(0));
        sys.Update(em, 1f);

        Assert.True(victim.Has<IsChokingTag>());
    }

    // ── AT-05: RescueAttempted emitted on non-fatal failure ──────────────────

    [Fact]
    public void AT05_FailedRescue_EmitsRescueAttemptedNarrative()
    {
        var (em, bus, clock, transitions, mutationApi, rescuer, victim) = Build(incapacitatedBudget: 50);

        var events = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += events.Add;

        var sys = new RescueExecutionSystem(EmptyCatalog(), ZeroSuccessRate(), transitions, mutationApi, bus, clock, new SeededRandom(0));
        sys.Update(em, 1f);

        Assert.Contains(events, e => e.Kind == NarrativeEventKind.RescueAttempted);
    }

    // ── RescueFailed emitted when budget is exhausted ────────────────────────

    [Fact]
    public void RescueFailed_WhenBudgetAtZero_EmitsRescueFailed()
    {
        var (em, bus, clock, transitions, mutationApi, rescuer, victim) = Build(incapacitatedBudget: 0);

        var events = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += events.Add;

        var sys = new RescueExecutionSystem(EmptyCatalog(), ZeroSuccessRate(), transitions, mutationApi, bus, clock, new SeededRandom(0));
        sys.Update(em, 1f);

        Assert.Contains(events, e => e.Kind == NarrativeEventKind.RescueFailed);
    }

    [Fact]
    public void RescueFailed_IsPersistent()
    {
        Assert.True(APIFramework.Systems.MemoryRecordingSystem.IsPersistent(NarrativeEventKind.RescueFailed));
    }

    [Fact]
    public void RescueAttempted_IsNotPersistent()
    {
        Assert.False(APIFramework.Systems.MemoryRecordingSystem.IsPersistent(NarrativeEventKind.RescueAttempted));
    }
}
