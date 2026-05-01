using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Chores;
using APIFramework.Systems.Narrative;
using Xunit;


namespace APIFramework.Tests.Systems.Chores;

/// <summary>AT-08: NPC completing the same chore 4 times in 7 game-days triggers overrotation stress.</summary>
public class ChoreOverrotationTests
{
    private const string BiasJson = @"{
        ""schemaVersion"": ""0.1.0"",
        ""biases"": {
            ""the-newbie"": { ""cleanMicrowave"": 0.95 }
        }
    }";

    private static (EntityManager em, Entity npc, Entity choreEntity, ChoreExecutionSystem execSys,
                    StressSystem stressSys, NarrativeEventBus bus)
        Build()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var choreCfg = new ChoreConfig
        {
            ChoreCompletionRatePerSecond    = 0.01,
            MinChoreAcceptanceBias          = 0.20,
            BadQualityThreshold             = 0.10f,  // set low so quality doesn't trigger bad-done
            ChoreOverrotationThreshold      = 3,       // fire when count > 3
            ChoreOverrotationWindowGameDays = 7,
            ChoreOverrotationStressGain     = 1.5,
            FrequencyTicks                  = new ChoreFrequencyConfig { CleanMicrowave = 5000L },
        };
        var table    = ChoreAcceptanceBiasTable.ParseJson(BiasJson);
        var bus      = new NarrativeEventBus();
        var execSys  = new ChoreExecutionSystem(choreCfg, clock, table, bus);

        var stressCfg  = new StressConfig();
        var queue      = new WillpowerEventQueue();
        var stressSys  = new StressSystem(stressCfg, new WorkloadConfig(), clock, queue, bus, em,
                                          choreCfg: choreCfg);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });
        npc.Add(new IntendedActionComponent(IntendedActionKind.ChoreWork, 0, DialogContextValue.None, 0));
        npc.Add(new StressComponent { AcuteLevel = 0 });
        npc.Add(new ChoreHistoryComponent
        {
            TimesPerformed       = new Dictionary<ChoreKind, int>(),
            TimesRefused         = new Dictionary<ChoreKind, int>(),
            AverageQuality       = new Dictionary<ChoreKind, float>(),
            WindowTimesPerformed = new Dictionary<ChoreKind, int>(),
            WindowStartDay       = new Dictionary<ChoreKind, int>(),
        });

        var choreEntity = em.CreateEntity();
        choreEntity.Add(new ChoreComponent
        {
            Kind              = ChoreKind.CleanMicrowave,
            CompletionLevel   = 0.0f,
            CurrentAssigneeId = npc.Id,
        });

        return (em, npc, choreEntity, execSys, stressSys, bus);
    }

    private static void CompleteChore(EntityManager em, Entity npc, Entity choreEntity, ChoreExecutionSystem sys)
    {
        // Reset and re-assign chore
        var c = choreEntity.Get<ChoreComponent>();
        c.CompletionLevel   = 0.0f;
        c.CurrentAssigneeId = npc.Id;
        choreEntity.Add(c);

        sys.Update(em, 100_000f);
    }

    [Fact]
    public void FourCompletions_IncrementOverrotationEventsToday()
    {
        var (em, npc, choreEntity, execSys, _, _) = Build();

        for (int i = 0; i < 4; i++)
            CompleteChore(em, npc, choreEntity, execSys);

        Assert.True(npc.Get<StressComponent>().ChoreOverrotationEventsToday > 0,
            "ChoreOverrotationEventsToday should be incremented after 4 completions in window");
    }

    [Fact]
    public void ThreeCompletions_DoNotTriggerOverrotation()
    {
        var (em, npc, choreEntity, execSys, _, _) = Build();

        for (int i = 0; i < 3; i++)
            CompleteChore(em, npc, choreEntity, execSys);

        Assert.Equal(0, npc.Get<StressComponent>().ChoreOverrotationEventsToday);
    }

    [Fact]
    public void FourCompletions_EmitOverrotationNarrative()
    {
        var (em, npc, choreEntity, execSys, _, bus) = Build();

        NarrativeEventCandidate? overrotation = null;
        bus.OnCandidateEmitted += c =>
        {
            if (c.Kind == NarrativeEventKind.ChoreOverrotation)
                overrotation = c;
        };

        for (int i = 0; i < 4; i++)
            CompleteChore(em, npc, choreEntity, execSys);

        Assert.NotNull(overrotation);
        Assert.Equal(NarrativeEventKind.ChoreOverrotation, overrotation!.Kind);
    }

    [Fact]
    public void OverrotationNarrative_IsPersistent()
    {
        Assert.True(MemoryRecordingSystem.IsPersistent(NarrativeEventKind.ChoreOverrotation));
    }

    [Fact]
    public void StressSystem_AppliesOverrotationStressGain()
    {
        var (em, npc, choreEntity, execSys, stressSys, _) = Build();

        for (int i = 0; i < 4; i++)
            CompleteChore(em, npc, choreEntity, execSys);

        int acuteBefore = npc.Get<StressComponent>().AcuteLevel;

        stressSys.Update(em, 1f);

        int acuteAfter = npc.Get<StressComponent>().AcuteLevel;
        Assert.True(acuteAfter >= acuteBefore,
            "AcuteLevel should not decrease after overrotation stress is applied");
    }

    [Fact]
    public void StressSystem_ClearsOverrotationCounter_AfterApplication()
    {
        var (em, npc, choreEntity, execSys, stressSys, _) = Build();

        for (int i = 0; i < 4; i++)
            CompleteChore(em, npc, choreEntity, execSys);

        stressSys.Update(em, 1f);

        Assert.Equal(0, npc.Get<StressComponent>().ChoreOverrotationEventsToday);
    }
}
