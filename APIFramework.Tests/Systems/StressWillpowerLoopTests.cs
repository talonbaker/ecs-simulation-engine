using System;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>AT-08: StressSystem pushes amplification events to WillpowerEventQueue.</summary>
public class StressWillpowerLoopTests
{
    private static (EntityManager em, Entity npc, WillpowerEventQueue queue, StressSystem sys)
        Build(int acuteLevel, StressConfig? cfg = null)
    {
        cfg ??= new StressConfig();
        var clock = new SimulationClock();
        var queue = new WillpowerEventQueue();
        var bus   = new NarrativeEventBus();
        var sys   = new StressSystem(cfg, clock, queue, bus);

        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 0));
        npc.Add(new StressComponent { AcuteLevel = acuteLevel, LastDayUpdated = 1 });

        return (em, npc, queue, sys);
    }

    // AT-08: When AcuteLevel ≥ stressedTagThreshold, one amplification SuppressionTick is pushed.

    [Fact]
    public void AtThreshold_PushesAmplificationEvent()
    {
        var (em, npc, queue, sys) = Build(acuteLevel: 60); // = stressedTagThreshold

        sys.Update(em, 1f);

        var drained = queue.DrainAll();
        Assert.Single(drained);
        Assert.Equal(WillpowerEventKind.SuppressionTick, drained[0].Kind);
        Assert.Equal(WillpowerSystem.EntityIntId(npc), drained[0].EntityId);
    }

    [Fact]
    public void BelowThreshold_NoPushToQueue()
    {
        var (em, npc, queue, sys) = Build(acuteLevel: 59);

        sys.Update(em, 1f);

        var drained = queue.DrainAll();
        Assert.Empty(drained);
    }

    [Fact]
    public void MagnitudeScalesWithAcuteLevel_AtThreshold()
    {
        // AcuteLevel = 60 (= threshold): scale = (60-60)/(100-60) = 0 → mag = 0 → magInt = max(1,0) = 1
        var (em, npc, queue, sys) = Build(acuteLevel: 60);

        sys.Update(em, 1f);

        var drained = queue.DrainAll();
        Assert.Single(drained);
        Assert.Equal(1, drained[0].Magnitude);
    }

    [Fact]
    public void MagnitudeScalesWithAcuteLevel_AtMax()
    {
        // AcuteLevel = 100: scale = (100-60)/(100-60) = 1.0 → mag = 1.0*1.0 = 1.0 → magInt = max(1, round(1.0)) = 1
        var cfg = new StressConfig { StressAmplificationMagnitude = 5.0 };
        var (em, npc, queue, sys) = Build(acuteLevel: 100, cfg: cfg);

        sys.Update(em, 1f);

        var drained = queue.DrainAll();
        Assert.Single(drained);
        // scale = 1.0, mag = 5.0 * 1.0 = 5.0 → magInt = max(1, 5) = 5
        Assert.Equal(5, drained[0].Magnitude);
    }

    [Fact]
    public void AmplificationEvent_ExactlyOnePerTick_WhenStressed()
    {
        var (em, npc, queue, sys) = Build(acuteLevel: 75);

        // Run 3 ticks; each should push exactly one event
        for (int i = 0; i < 3; i++)
        {
            sys.Update(em, 1f);
            var drained = queue.DrainAll();
            Assert.Single(drained);
        }
    }

    [Fact]
    public void WillpowerDrainedByAmplification_WhenLoopCloses()
    {
        // Full loop: StressSystem pushes SuppressionTick → WillpowerSystem applies it
        var cfg     = new StressConfig { StressedTagThreshold = 60, StressAmplificationMagnitude = 5.0 };
        var ssCfg   = new SocialSystemConfig();
        var clock   = new SimulationClock();
        var queue   = new WillpowerEventQueue();
        var bus     = new NarrativeEventBus();
        var wpSys   = new WillpowerSystem(ssCfg, queue);
        var stressSys = new StressSystem(cfg, clock, queue, bus);

        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 0));
        npc.Add(new WillpowerComponent(80, 80));
        npc.Add(new StressComponent { AcuteLevel = 100, LastDayUpdated = 1 });

        // Tick order: StressSystem pushes → WillpowerSystem drains
        stressSys.Update(em, 1f);
        wpSys.Update(em, 1f);

        int wpAfter = npc.Get<WillpowerComponent>().Current;
        // StressAmplificationMagnitude=5, scale=1.0 → magInt=5 → WP drops by 5
        Assert.Equal(75, wpAfter);
    }
}
