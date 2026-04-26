using System;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>AT-07: BurningOutTag sticky cooldown.</summary>
public class StressBurningOutStickyTests
{
    private static (EntityManager em, Entity npc, SimulationClock clock, StressSystem sys)
        Build(double chronicLevel, int lastDayUpdated = 1)
    {
        var cfg   = new StressConfig
        {
            BurningOutTagThreshold  = 70,
            BurningOutCooldownDays  = 3,
            AcuteDecayPerTick       = 0.0,   // no decay noise
        };
        var clock = new SimulationClock();
        var queue = new WillpowerEventQueue();
        var bus   = new NarrativeEventBus();
        var sys   = new StressSystem(cfg, clock, queue, bus);

        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 0));
        npc.Add(new StressComponent
        {
            AcuteLevel    = 0,
            ChronicLevel  = chronicLevel,
            LastDayUpdated = lastDayUpdated,
        });
        return (em, npc, clock, sys);
    }

    private static void AdvanceDay(SimulationClock clock) =>
        clock.Tick(720f);   // 720 real-s * 120 TimeScale = 86400 game-s = 1 game-day

    [Fact]
    public void BurningOutTag_AppliedWhen_ChronicAboveThreshold()
    {
        var (em, npc, _, sys) = Build(chronicLevel: 75.0);

        sys.Update(em, 1f);

        Assert.True(npc.Has<BurningOutTag>());
    }

    [Fact]
    public void BurningOutTag_NotApplied_BelowThreshold_NoCooldown()
    {
        var (em, npc, _, sys) = Build(chronicLevel: 40.0);

        sys.Update(em, 1f);

        Assert.False(npc.Has<BurningOutTag>());
    }

    [Fact]
    public void BurningOutTag_Sticky_Within_CooldownDays()
    {
        // Start above threshold so BurningOutTag is applied and BurnoutLastAppliedDay is set
        var (em, npc, clock, sys) = Build(chronicLevel: 75.0);

        // Day 1: above threshold → tag applied, BurnoutLastAppliedDay = 1
        sys.Update(em, 1f);
        Assert.True(npc.Has<BurningOutTag>());
        Assert.Equal(1, npc.Get<StressComponent>().BurnoutLastAppliedDay);

        // Now drop ChronicLevel below threshold but stay within cooldown (day 2)
        var sc = npc.Get<StressComponent>();
        sc.ChronicLevel = 30.0;
        npc.Add(sc);

        AdvanceDay(clock);  // advance to day 2
        sys.Update(em, 1f);

        // cooldown: day 2 - day 1 = 1 ≤ 3 → still sticky
        Assert.True(npc.Has<BurningOutTag>());
    }

    [Fact]
    public void BurningOutTag_Removed_After_CooldownExpires()
    {
        var (em, npc, clock, sys) = Build(chronicLevel: 75.0);

        // Day 1: tag applied
        sys.Update(em, 1f);
        int appliedDay = npc.Get<StressComponent>().BurnoutLastAppliedDay;

        // Drop ChronicLevel below threshold
        var sc = npc.Get<StressComponent>();
        sc.ChronicLevel = 20.0;
        npc.Add(sc);

        // Advance 4 days past appliedDay (cooldown = 3, so day appliedDay+4 clears it)
        for (int i = 0; i < 4; i++)
        {
            AdvanceDay(clock);
            sys.Update(em, 1f);
        }

        Assert.False(npc.Has<BurningOutTag>(),
            $"After {clock.DayNumber - appliedDay} days with low ChronicLevel, tag should be gone");
    }

    [Fact]
    public void BurningOutTag_RemainsSticky_ThroughCooldown_ThenDrops()
    {
        var (em, npc, clock, sys) = Build(chronicLevel: 75.0);

        sys.Update(em, 1f);
        int appliedDay = npc.Get<StressComponent>().BurnoutLastAppliedDay;

        var sc = npc.Get<StressComponent>();
        sc.ChronicLevel = 10.0;
        npc.Add(sc);

        // Days 1, 2, 3 past appliedDay: still within cooldown
        for (int d = 1; d <= 3; d++)
        {
            AdvanceDay(clock);
            sys.Update(em, 1f);
            Assert.True(npc.Has<BurningOutTag>(),
                $"Expected BurningOutTag sticky on day {appliedDay + d}");
        }

        // Day 4 past appliedDay: cooldown expired
        AdvanceDay(clock);
        sys.Update(em, 1f);
        Assert.False(npc.Has<BurningOutTag>(),
            $"Expected BurningOutTag removed on day {appliedDay + 4}");
    }

    [Fact]
    public void BurningOutTag_Renewed_When_ChronicRisesAgain()
    {
        var (em, npc, clock, sys) = Build(chronicLevel: 75.0);

        // Apply, then expire
        sys.Update(em, 1f);
        int appliedDay = npc.Get<StressComponent>().BurnoutLastAppliedDay;

        var sc = npc.Get<StressComponent>();
        sc.ChronicLevel = 10.0;
        npc.Add(sc);

        for (int i = 0; i < 5; i++) { AdvanceDay(clock); sys.Update(em, 1f); }
        Assert.False(npc.Has<BurningOutTag>());

        // Rise above threshold again
        sc = npc.Get<StressComponent>();
        sc.ChronicLevel = 80.0;
        npc.Add(sc);

        sys.Update(em, 1f);
        Assert.True(npc.Has<BurningOutTag>());
    }
}
