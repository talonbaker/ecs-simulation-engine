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
/// AT-06: Locked-in NPC + nearby rescuer → door unlocked via IWorldMutationApi.DetachObstacle.
/// </summary>
public class RescueExecutionDoorUnlockTests
{
    private static RescueConfig GuaranteedSuccess() => new()
    {
        HeimlichBaseSuccessRate   = 1.00f,
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
        WorldMutationApi mutationApi,
        StructuralChangeBus structBus,
        Entity rescuer,
        Entity victim,
        Entity obstacle)
    Build()
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
            IncapacitatedTickBudget = 90,
            PendingDeathCause = CauseOfDeath.StarvedAlone,
        });
        victim.Add(new PositionComponent { X = 1f, Z = 0f });
        victim.Add(new LockedInComponent { FirstDetectedTick = 0, StarvationTickBudget = 2 });

        // Obstacle entity near victim (represents the locked door)
        var obstacle = em.CreateEntity();
        obstacle.Add(new PositionComponent { X = 2f, Z = 0f });
        obstacle.Add(new IdentityComponent { Name = "Door" });
        mutationApi.AttachObstacle(obstacle.Id);

        int victimId = BitConverter.ToInt32(victim.Id.ToByteArray(), 0);
        rescuer.Add(new IntendedActionComponent(
            Kind:           IntendedActionKind.Rescue,
            TargetEntityId: victimId,
            Context:        DialogContextValue.None,
            IntensityHint:  80));

        return (em, bus, clock, transitions, mutationApi, structBus, rescuer, victim, obstacle);
    }

    // ── AT-06: Door obstacle detached ────────────────────────────────────────

    [Fact]
    public void AT06_DoorUnlock_RemovesObstacleTag()
    {
        var (em, bus, clock, transitions, mutationApi, _, rescuer, victim, obstacle) = Build();
        var sys = new RescueExecutionSystem(EmptyCatalog(), GuaranteedSuccess(), transitions, mutationApi, bus, clock, new SeededRandom(0));
        sys.Update(em, 1f);

        Assert.False(obstacle.Has<ObstacleTag>());
    }

    [Fact]
    public void AT06_DoorUnlock_ClearsLockedInComponent()
    {
        var (em, bus, clock, transitions, mutationApi, _, rescuer, victim, obstacle) = Build();
        var sys = new RescueExecutionSystem(EmptyCatalog(), GuaranteedSuccess(), transitions, mutationApi, bus, clock, new SeededRandom(0));
        sys.Update(em, 1f);

        Assert.False(victim.Has<LockedInComponent>());
    }

    [Fact]
    public void AT06_DoorUnlock_TransitionsVictimToAlive()
    {
        var (em, bus, clock, transitions, mutationApi, _, rescuer, victim, obstacle) = Build();
        var sys = new RescueExecutionSystem(EmptyCatalog(), GuaranteedSuccess(), transitions, mutationApi, bus, clock, new SeededRandom(0));
        sys.Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.Equal(LS.Alive, victim.Get<LifeStateComponent>().State);
    }

    [Fact]
    public void AT06_DoorUnlock_EmitsRescuePerformed()
    {
        var (em, bus, clock, transitions, mutationApi, _, rescuer, victim, obstacle) = Build();

        var events = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += events.Add;

        var sys = new RescueExecutionSystem(EmptyCatalog(), GuaranteedSuccess(), transitions, mutationApi, bus, clock, new SeededRandom(0));
        sys.Update(em, 1f);

        Assert.Contains(events, e => e.Kind == NarrativeEventKind.RescuePerformed);
    }

    // ── DetermineRescueKind: victim with LockedInComponent → DoorUnlock ──────

    [Fact]
    public void VictimWithLockedIn_AndNoChokingTag_IsDoorUnlock()
    {
        var (em, bus, clock, transitions, mutationApi, _, rescuer, victim, obstacle) = Build();
        // Victim has LockedInComponent, no IsChokingTag → expect DoorUnlock kind
        // Verified indirectly: obstacle tag is removed (only DoorUnlock calls DetachObstacle)
        var sys = new RescueExecutionSystem(EmptyCatalog(), GuaranteedSuccess(), transitions, mutationApi, bus, clock, new SeededRandom(0));
        sys.Update(em, 1f);

        Assert.False(obstacle.Has<ObstacleTag>(), "DoorUnlock kind should remove ObstacleTag");
    }
}
