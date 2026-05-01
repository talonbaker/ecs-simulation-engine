using System.Collections.Generic;
using System.Text;
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
/// AT-10: Determinism — two independent runs with the same seed over 5000 ticks produce
/// byte-identical rescue outcome sequences.
/// </summary>
public class RescueDeterminismTests
{
    private static RescueConfig DefaultCfg() => new()
    {
        RescueThreshold         = 0.40f,
        AwarenessRangeForRescue = 3.0f,
        MinRescueWillpower      = 20,
        MaxRescueStress         = 80f,
        HeimlichBaseSuccessRate = 0.65f,
        CprBaseSuccessRate      = 0.30f,
        DoorUnlockBaseSuccessRate = 0.95f,
    };

    private static ArchetypeRescueBiasCatalog MakeCatalog()
    {
        var path = System.IO.Path.GetTempFileName();
        System.IO.File.WriteAllText(path, @"{
            ""schemaVersion"": ""0.1.0"",
            ""archetypeRescueBias"": [
                { ""archetype"": ""the-newbie"", ""bias"": 0.85,
                  ""heimlichCompetence"": 0.10, ""cprCompetence"": 0.05, ""doorUnlockCompetence"": 0.03 }
            ]
        }");
        return ArchetypeRescueBiasCatalog.LoadFromFile(path);
    }

    private record RescueOutcome(long Tick, NarrativeEventKind Kind, int ParticipantCount);

    private static List<RescueOutcome> RunScenario(int seed, int ticks)
    {
        var outcomes = new List<RescueOutcome>();

        var em     = new EntityManager();
        var bus    = new NarrativeEventBus();
        var clock  = new SimulationClock();
        var config = new SimConfig { LifeState = new LifeStateConfig { DefaultIncapacitatedTicks = 30 } };
        var rng    = new SeededRandom(seed);

        var transitions = new LifeStateTransitionSystem(bus, em, clock, config);
        var structBus   = new StructuralChangeBus();
        var mutationApi = new WorldMutationApi(em, structBus);

        var catalog = MakeCatalog();
        var intentSys = new RescueIntentSystem(catalog, DefaultCfg());
        var execSys   = new RescueExecutionSystem(catalog, DefaultCfg(), transitions, mutationApi, bus, clock, rng);

        bus.OnCandidateEmitted += c =>
        {
            if (c.Kind is NarrativeEventKind.RescuePerformed
                       or NarrativeEventKind.RescueAttempted
                       or NarrativeEventKind.RescueFailed)
                outcomes.Add(new RescueOutcome(c.Tick, c.Kind, c.ParticipantIds.Count));
        };

        // Build a repeating world: rescuer + victim, victim restarts on rescue
        var rescuer = em.CreateEntity();
        rescuer.Add(new NpcTag());
        rescuer.Add(new LifeStateComponent { State = LS.Alive });
        rescuer.Add(new PositionComponent { X = 0f, Z = 0f });
        rescuer.Add(new WillpowerComponent(80, 80));
        rescuer.Add(new StressComponent { AcuteLevel = 5 });
        rescuer.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });
        rescuer.Add(new ProximityComponent { ConversationRangeTiles = 2 });

        var victim = em.CreateEntity();
        victim.Add(new NpcTag());
        victim.Add(new PositionComponent { X = 1f, Z = 0f });

        for (int t = 0; t < ticks; t++)
        {
            // Ensure victim is Incapacitated and choking every 5 ticks
            if (t % 5 == 0)
            {
                victim.Add(new LifeStateComponent
                {
                    State = LS.Incapacitated,
                    IncapacitatedTickBudget = 30,
                    PendingDeathCause = CauseOfDeath.Choked,
                });
                if (!victim.Has<IsChokingTag>()) victim.Add(new IsChokingTag());
            }

            intentSys.Update(em, 1f);
            execSys.Update(em, 1f);
            transitions.Update(em, 1f);

            // Tick clock
            clock.Tick(1f / 20f);
        }

        return outcomes;
    }

    [Fact]
    public void FiveThousandTicks_TwoRunsSameSeed_ProduceIdenticalOutcomes()
    {
        var run1 = RunScenario(seed: 42, ticks: 5000);
        var run2 = RunScenario(seed: 42, ticks: 5000);

        Assert.Equal(run1.Count, run2.Count);
        for (int i = 0; i < run1.Count; i++)
        {
            Assert.Equal(run1[i].Tick,             run2[i].Tick);
            Assert.Equal(run1[i].Kind,             run2[i].Kind);
            Assert.Equal(run1[i].ParticipantCount, run2[i].ParticipantCount);
        }
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentOutcomes()
    {
        var run1 = RunScenario(seed: 1, ticks: 5000);
        var run2 = RunScenario(seed: 9999, ticks: 5000);

        // With different seeds the success/fail counts should differ at some point
        // (probabilistic — but overwhelmingly likely given 5000 ticks)
        bool differs = run1.Count != run2.Count;
        if (!differs && run1.Count > 0)
        {
            for (int i = 0; i < run1.Count; i++)
            {
                if (run1[i].Kind != run2[i].Kind) { differs = true; break; }
            }
        }
        Assert.True(differs || run1.Count == 0, "Different seeds should produce different results");
    }
}
