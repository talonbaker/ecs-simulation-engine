using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-02: ScheduleSystem given a 4-block schedule advances the clock and produces the correct
/// ActiveBlockIndex at each hour boundary.
/// AT-03: End-of-day wrap (EndHour &lt; StartHour) is handled correctly.
/// </summary>
public class ScheduleSystemTests
{
    // -- Helpers ---------------------------------------------------------------

    private static (EntityManager em, SimulationClock clock, ScheduleSystem sys) Setup()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var sys   = new ScheduleSystem(clock);
        return (em, clock, sys);
    }

    private static Entity SpawnNpcWithSchedule(EntityManager em, IReadOnlyList<ScheduleBlock> blocks)
    {
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new ScheduleComponent { Blocks = blocks });
        return npc;
    }

    /// <summary>Advances the clock so GameHour == targetHour (roughly).</summary>
    private static void SetClockToHour(SimulationClock clock, float targetHour)
    {
        // Clock starts at 6:00 AM (offset 6h). To reach targetHour:
        // GameHour = (TotalTime + 6*3600) % 86400 / 3600 = targetHour
        // TotalTime = targetHour * 3600 - 6 * 3600 (mod 86400)
        float desiredSeconds = targetHour * 3600f;
        float startOffsetSeconds = SimulationClock.DawnHour * 3600f; // = 6 * 3600 = 21600
        float totalTime = desiredSeconds - startOffsetSeconds;
        if (totalTime < 0) totalTime += SimulationClock.SecondsPerDay;

        // Hack: use TimeScale=1 and advance by the needed delta.
        float currentTotal = (float)clock.TotalTime;
        float delta = totalTime - currentTotal;
        if (delta < 0) delta += SimulationClock.SecondsPerDay;

        clock.TimeScale = 1f;
        clock.Tick(delta);
    }

    // -- AT-02: Block resolver -------------------------------------------------

    [Fact]
    public void AT02_FourBlockSchedule_CorrectIndexAtEachBoundary()
    {
        var (em, clock, sys) = Setup();

        // 4 blocks covering 6:00–22:00 in linear segments + off-shift wrap
        var blocks = new List<ScheduleBlock>
        {
            new( 6.0f,  9.0f, "anchor-a", ScheduleActivityKind.Outside),   // 0
            new( 9.0f, 12.0f, "anchor-b", ScheduleActivityKind.AtDesk),    // 1
            new(12.0f, 18.0f, "anchor-c", ScheduleActivityKind.Lunch),     // 2
            new(18.0f,  6.0f, "anchor-d", ScheduleActivityKind.Sleeping),  // 3 (wrap)
        };
        var npc = SpawnNpcWithSchedule(em, blocks);

        // Test each segment.
        var cases = new (float hour, int expectedIdx)[]
        {
            (6.5f,  0),   // inside block 0
            (9.0f,  1),   // at the start of block 1
            (11.5f, 1),   // inside block 1
            (12.0f, 2),   // at the start of block 2
            (15.0f, 2),   // inside block 2
            (18.0f, 3),   // at the start of wrap block
            (23.0f, 3),   // deep inside wrap block (after midnight)
            (0.5f,  3),   // wrap block active at 00:30
            (5.5f,  3),   // wrap block active just before 6:00
        };

        foreach (var (hour, expectedIdx) in cases)
        {
            SetClockToHour(clock, hour);
            sys.Update(em, 1f);

            Assert.True(npc.Has<CurrentScheduleBlockComponent>());
            var comp = npc.Get<CurrentScheduleBlockComponent>();
            Assert.Equal(expectedIdx, comp.ActiveBlockIndex);
        }
    }

    [Fact]
    public void AT02_NoMatchingBlock_ReturnsNegativeOne()
    {
        var (em, clock, sys) = Setup();

        // Schedule has a gap from 12:00 to 14:00.
        var blocks = new List<ScheduleBlock>
        {
            new( 8.0f, 12.0f, "anchor-a", ScheduleActivityKind.AtDesk),
            new(14.0f, 22.0f, "anchor-b", ScheduleActivityKind.AtDesk),
            new(22.0f,  8.0f, "anchor-c", ScheduleActivityKind.Sleeping),
        };
        var npc = SpawnNpcWithSchedule(em, blocks);

        SetClockToHour(clock, 13.0f);  // in the gap
        sys.Update(em, 1f);

        var comp = npc.Get<CurrentScheduleBlockComponent>();
        Assert.Equal(-1, comp.ActiveBlockIndex);
        Assert.Equal(Guid.Empty, comp.AnchorEntityId);
    }

    // -- AT-03: End-of-day wrap -------------------------------------------------

    [Fact]
    public void AT03_WrapAroundBlock_ActiveAfterStartHour()
    {
        var (em, clock, sys) = Setup();

        var blocks = new List<ScheduleBlock>
        {
            new( 6.0f, 22.0f, "anchor-a", ScheduleActivityKind.AtDesk),
            new(22.0f,  6.0f, "anchor-b", ScheduleActivityKind.Sleeping),
        };
        var npc = SpawnNpcWithSchedule(em, blocks);

        SetClockToHour(clock, 23.0f);
        sys.Update(em, 1f);

        Assert.Equal(1, npc.Get<CurrentScheduleBlockComponent>().ActiveBlockIndex);
    }

    [Fact]
    public void AT03_WrapAroundBlock_ActiveBeforeEndHour()
    {
        var (em, clock, sys) = Setup();

        var blocks = new List<ScheduleBlock>
        {
            new( 6.0f, 22.0f, "anchor-a", ScheduleActivityKind.AtDesk),
            new(22.0f,  6.0f, "anchor-b", ScheduleActivityKind.Sleeping),
        };
        var npc = SpawnNpcWithSchedule(em, blocks);

        SetClockToHour(clock, 0.5f);   // 00:30
        sys.Update(em, 1f);

        Assert.Equal(1, npc.Get<CurrentScheduleBlockComponent>().ActiveBlockIndex);

        SetClockToHour(clock, 5.5f);   // 05:30
        sys.Update(em, 1f);

        Assert.Equal(1, npc.Get<CurrentScheduleBlockComponent>().ActiveBlockIndex);
    }

    [Fact]
    public void AT03_WrapAroundBlock_NotActiveAt7()
    {
        var (em, clock, sys) = Setup();

        var blocks = new List<ScheduleBlock>
        {
            new( 6.0f, 22.0f, "anchor-a", ScheduleActivityKind.AtDesk),
            new(22.0f,  6.0f, "anchor-b", ScheduleActivityKind.Sleeping),
        };
        var npc = SpawnNpcWithSchedule(em, blocks);

        SetClockToHour(clock, 7.0f);
        sys.Update(em, 1f);

        // 7:00 is in block 0, not the sleeping wrap block.
        Assert.Equal(0, npc.Get<CurrentScheduleBlockComponent>().ActiveBlockIndex);
    }

    // -- IsBlockActive unit tests ----------------------------------------------

    [Theory]
    [InlineData(22.0f, 6.0f, 23.0f, true)]
    [InlineData(22.0f, 6.0f,  0.5f, true)]
    [InlineData(22.0f, 6.0f,  5.5f, true)]
    [InlineData(22.0f, 6.0f,  7.0f, false)]
    [InlineData( 8.0f, 17.0f, 12.0f, true)]
    [InlineData( 8.0f, 17.0f,  7.0f, false)]
    [InlineData( 8.0f, 17.0f, 17.0f, false)]  // endHour is exclusive
    public void AT03_IsBlockActive_VariousCases(float start, float end, float hour, bool expected)
    {
        Assert.Equal(expected, ScheduleSystem.IsBlockActive(start, end, hour));
    }

    // -- Anchor resolution -----------------------------------------------------

    [Fact]
    public void AT02_AnchorEntityResolved_WhenPresent()
    {
        var (em, clock, sys) = Setup();

        // Create an anchor entity.
        var anchor = em.CreateEntity();
        anchor.Add(new NamedAnchorComponent { Tag = "the-window" });
        anchor.Add(new PositionComponent { X = 10f, Y = 0f, Z = 10f });

        var blocks = new List<ScheduleBlock>
        {
            new( 6.0f, 22.0f, "the-window",    ScheduleActivityKind.AtDesk),
            new(22.0f,  6.0f, "the-parking-lot", ScheduleActivityKind.Sleeping),
        };
        var npc = SpawnNpcWithSchedule(em, blocks);

        SetClockToHour(clock, 9.0f);
        sys.Update(em, 1f);

        var comp = npc.Get<CurrentScheduleBlockComponent>();
        Assert.Equal(0, comp.ActiveBlockIndex);
        Assert.Equal(anchor.Id, comp.AnchorEntityId);
    }

    [Fact]
    public void AT02_AnchorEntityEmpty_WhenMissing()
    {
        var (em, clock, sys) = Setup();

        // No anchor entity with this tag.
        var blocks = new List<ScheduleBlock>
        {
            new( 6.0f, 22.0f, "nonexistent-anchor", ScheduleActivityKind.AtDesk),
            new(22.0f,  6.0f, "the-parking-lot",    ScheduleActivityKind.Sleeping),
        };
        var npc = SpawnNpcWithSchedule(em, blocks);

        SetClockToHour(clock, 9.0f);
        sys.Update(em, 1f);

        var comp = npc.Get<CurrentScheduleBlockComponent>();
        Assert.Equal(Guid.Empty, comp.AnchorEntityId);
    }
}
