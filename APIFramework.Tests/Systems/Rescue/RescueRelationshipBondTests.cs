using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Rescue;
using APIFramework.Systems.Spatial;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.Rescue;

/// <summary>
/// AT-07 (memory side): Successful rescue → MemoryRecordingSystem routes RescuePerformed
/// to the relationship memory between rescuer and rescued, creating a strong positive entry.
/// </summary>
public class RescueRelationshipBondTests
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

    [Fact]
    public void SuccessfulRescue_RescuePerformed_RouteToRelationshipMemory()
    {
        var em     = new EntityManager();
        var bus    = new NarrativeEventBus();
        var clock  = new SimulationClock();
        var config = new SimConfig { LifeState = new LifeStateConfig { DefaultIncapacitatedTicks = 90 } };
        var transitions = new LifeStateTransitionSystem(bus, em, clock, config);

        var structBus   = new StructuralChangeBus();
        var mutationApi = new WorldMutationApi(em, structBus);

        var memoryCfg = new MemoryConfig { MaxRelationshipMemoryCount = 50, MaxPersonalMemoryCount = 50 };
        var memSys    = new MemoryRecordingSystem(bus, em, memoryCfg);

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
            PendingDeathCause = CauseOfDeath.Choked,
        });
        victim.Add(new PositionComponent { X = 1f, Z = 0f });
        victim.Add(new IsChokingTag());

        int victimIntId = BitConverter.ToInt32(victim.Id.ToByteArray(), 0);
        rescuer.Add(new IntendedActionComponent(
            Kind:           IntendedActionKind.Rescue,
            TargetEntityId: victimIntId,
            Context:        DialogContextValue.None,
            IntensityHint:  80));

        var sys = new RescueExecutionSystem(EmptyCatalog(), GuaranteedSuccess(),
            transitions, mutationApi, bus, clock, new SeededRandom(0));
        sys.Update(em, 1f);

        // MemoryRecordingSystem is event-driven; it already processed the candidate.
        // Find the relationship entity that carries the pair memory.
        Entity? relEntity = null;
        int rescuerIntId = BitConverter.ToInt32(rescuer.Id.ToByteArray(), 0);
        int pA = Math.Min(rescuerIntId, victimIntId);
        int pB = Math.Max(rescuerIntId, victimIntId);

        foreach (var e in em.Query<RelationshipTag>())
        {
            if (!e.Has<RelationshipComponent>()) continue;
            var rc = e.Get<RelationshipComponent>();
            if (rc.ParticipantA == pA && rc.ParticipantB == pB)
            { relEntity = e; break; }
        }

        Assert.NotNull(relEntity);
        Assert.True(relEntity!.Has<RelationshipMemoryComponent>());

        var mem = relEntity.Get<RelationshipMemoryComponent>();
        Assert.Contains(mem.Recent, entry => entry.Kind == NarrativeEventKind.RescuePerformed);
    }

    [Fact]
    public void RescuePerformed_MemoryEntry_IsPersistent()
    {
        // Verify the persistent flag is set on the stored entry.
        Assert.True(MemoryRecordingSystem.IsPersistent(NarrativeEventKind.RescuePerformed));
    }
}
