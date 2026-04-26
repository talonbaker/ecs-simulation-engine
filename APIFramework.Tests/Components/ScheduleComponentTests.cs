using System;
using System.Collections.Generic;
using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Components;

/// <summary>
/// AT-01: ScheduleComponent and CurrentScheduleBlockComponent compile, instantiate, and
/// round-trip equality. ScheduleBlock record struct stores values in range 0..24.
/// </summary>
public class ScheduleComponentTests
{
    [Fact]
    public void AT01_ScheduleBlock_StoresAllFields()
    {
        var block = new ScheduleBlock(8.0f, 12.0f, "the-window", ScheduleActivityKind.AtDesk);

        Assert.Equal(8.0f,                       block.StartHour);
        Assert.Equal(12.0f,                      block.EndHour);
        Assert.Equal("the-window",               block.AnchorId);
        Assert.Equal(ScheduleActivityKind.AtDesk, block.Activity);
    }

    [Fact]
    public void AT01_ScheduleBlock_RecordEquality()
    {
        var a = new ScheduleBlock(8.0f, 12.0f, "the-window", ScheduleActivityKind.AtDesk);
        var b = new ScheduleBlock(8.0f, 12.0f, "the-window", ScheduleActivityKind.AtDesk);
        var c = new ScheduleBlock(9.0f, 12.0f, "the-window", ScheduleActivityKind.AtDesk);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void AT01_ScheduleBlock_AllActivityKindsRoundTrip()
    {
        foreach (ScheduleActivityKind kind in Enum.GetValues<ScheduleActivityKind>())
        {
            var block = new ScheduleBlock(0f, 1f, "test-anchor", kind);
            Assert.Equal(kind, block.Activity);
        }
    }

    [Fact]
    public void AT01_ScheduleComponent_StoresBlocks()
    {
        var blocks = new List<ScheduleBlock>
        {
            new(6.0f, 12.0f, "the-window",      ScheduleActivityKind.AtDesk),
            new(12.0f, 17.0f, "the-microwave",  ScheduleActivityKind.Lunch),
            new(17.0f, 6.0f,  "the-parking-lot", ScheduleActivityKind.Sleeping),
        };

        var component = new ScheduleComponent { Blocks = blocks };

        Assert.Equal(3, component.Blocks.Count);
        Assert.Equal("the-window",         component.Blocks[0].AnchorId);
        Assert.Equal("the-microwave",      component.Blocks[1].AnchorId);
        Assert.Equal("the-parking-lot",    component.Blocks[2].AnchorId);
    }

    [Fact]
    public void AT01_CurrentScheduleBlockComponent_DefaultsToInactive()
    {
        var comp = new CurrentScheduleBlockComponent
        {
            ActiveBlockIndex = -1,
            AnchorEntityId   = Guid.Empty
        };

        Assert.Equal(-1,        comp.ActiveBlockIndex);
        Assert.Equal(Guid.Empty, comp.AnchorEntityId);
    }

    [Fact]
    public void AT01_CurrentScheduleBlockComponent_StoresValues()
    {
        var guid = Guid.NewGuid();
        var comp = new CurrentScheduleBlockComponent
        {
            ActiveBlockIndex = 2,
            AnchorEntityId   = guid,
            Activity         = ScheduleActivityKind.Break
        };

        Assert.Equal(2,                            comp.ActiveBlockIndex);
        Assert.Equal(guid,                         comp.AnchorEntityId);
        Assert.Equal(ScheduleActivityKind.Break,   comp.Activity);
    }

    [Fact]
    public void AT01_ScheduleBlock_ValidHourRanges()
    {
        // Hours must be in [0, 24]. These should construct without error.
        var normal = new ScheduleBlock(0f, 24f, "a", ScheduleActivityKind.AtDesk);
        Assert.Equal(0f,  normal.StartHour);
        Assert.Equal(24f, normal.EndHour);

        // Wrap-around block (EndHour < StartHour) is valid.
        var wrap = new ScheduleBlock(17.0f, 6.0f, "b", ScheduleActivityKind.Sleeping);
        Assert.Equal(17.0f, wrap.StartHour);
        Assert.Equal(6.0f,  wrap.EndHour);
    }
}
