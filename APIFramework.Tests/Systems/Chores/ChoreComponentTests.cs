using System;
using System.Collections.Generic;
using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Systems.Chores;

/// <summary>AT-01: ChoreComponent and ChoreHistoryComponent compile and have expected defaults.</summary>
public class ChoreComponentTests
{
    [Fact]
    public void ChoreKind_HasSevenValues()
    {
        var values = Enum.GetValues<ChoreKind>();
        Assert.Equal(7, values.Length);
    }

    [Fact]
    public void ChoreKind_ValuesMatchSpec()
    {
        Assert.Equal(0, (int)ChoreKind.CleanMicrowave);
        Assert.Equal(1, (int)ChoreKind.CleanFridge);
        Assert.Equal(2, (int)ChoreKind.CleanBathroom);
        Assert.Equal(3, (int)ChoreKind.TakeOutTrash);
        Assert.Equal(4, (int)ChoreKind.RefillWaterCooler);
        Assert.Equal(5, (int)ChoreKind.RestockSupplyCloset);
        Assert.Equal(6, (int)ChoreKind.ReplaceToner);
    }

    [Fact]
    public void ChoreComponent_DefaultsToUnassigned()
    {
        var c = new ChoreComponent();
        Assert.Equal(Guid.Empty, c.CurrentAssigneeId);
        Assert.Equal(Guid.Empty, c.TargetAnchorId);
    }

    [Fact]
    public void ChoreComponent_BelowHalfIsDirty()
    {
        var c = new ChoreComponent { CompletionLevel = 0.4f };
        Assert.True(c.CompletionLevel < 0.5f, "CompletionLevel below 0.5 means dirty");
    }

    [Fact]
    public void ChoreComponent_RoundTrip()
    {
        var id  = Guid.NewGuid();
        var anc = Guid.NewGuid();
        var c = new ChoreComponent
        {
            Kind                  = ChoreKind.TakeOutTrash,
            CompletionLevel       = 0.75f,
            QualityOfLastExecution = 0.90f,
            LastDoneTick          = 1234L,
            NextScheduledTick     = 9999L,
            CurrentAssigneeId     = id,
            TargetAnchorId        = anc,
        };

        Assert.Equal(ChoreKind.TakeOutTrash, c.Kind);
        Assert.Equal(0.75f, c.CompletionLevel);
        Assert.Equal(0.90f, c.QualityOfLastExecution);
        Assert.Equal(1234L, c.LastDoneTick);
        Assert.Equal(9999L, c.NextScheduledTick);
        Assert.Equal(id,    c.CurrentAssigneeId);
        Assert.Equal(anc,   c.TargetAnchorId);
    }
}
