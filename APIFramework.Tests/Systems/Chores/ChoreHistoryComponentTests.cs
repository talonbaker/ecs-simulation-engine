using System.Collections.Generic;
using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Systems.Chores;

/// <summary>AT-01: ChoreHistoryComponent construction and field access.</summary>
public class ChoreHistoryComponentTests
{
    [Fact]
    public void ChoreHistoryComponent_CanBeConstructed_WithAllFields()
    {
        var h = new ChoreHistoryComponent
        {
            TimesPerformed       = new Dictionary<ChoreKind, int>(),
            TimesRefused         = new Dictionary<ChoreKind, int>(),
            AverageQuality       = new Dictionary<ChoreKind, float>(),
            WindowTimesPerformed = new Dictionary<ChoreKind, int>(),
            WindowStartDay       = new Dictionary<ChoreKind, int>(),
            LastRefusalTick      = 0L,
        };

        Assert.NotNull(h.TimesPerformed);
        Assert.NotNull(h.TimesRefused);
        Assert.NotNull(h.AverageQuality);
        Assert.NotNull(h.WindowTimesPerformed);
        Assert.NotNull(h.WindowStartDay);
        Assert.Equal(0L, h.LastRefusalTick);
    }

    [Fact]
    public void ChoreHistoryComponent_DictionariesStartEmpty()
    {
        var h = new ChoreHistoryComponent
        {
            TimesPerformed       = new Dictionary<ChoreKind, int>(),
            TimesRefused         = new Dictionary<ChoreKind, int>(),
            AverageQuality       = new Dictionary<ChoreKind, float>(),
            WindowTimesPerformed = new Dictionary<ChoreKind, int>(),
            WindowStartDay       = new Dictionary<ChoreKind, int>(),
        };

        Assert.Empty(h.TimesPerformed);
        Assert.Empty(h.TimesRefused);
        Assert.Empty(h.AverageQuality);
        Assert.Empty(h.WindowTimesPerformed);
        Assert.Empty(h.WindowStartDay);
    }

    [Fact]
    public void ChoreHistoryComponent_TimesPerformed_AccumulatesCorrectly()
    {
        var h = new ChoreHistoryComponent
        {
            TimesPerformed = new Dictionary<ChoreKind, int>(),
        };

        h.TimesPerformed[ChoreKind.CleanMicrowave] = 3;
        h.TimesPerformed[ChoreKind.TakeOutTrash]   = 1;

        Assert.Equal(3, h.TimesPerformed[ChoreKind.CleanMicrowave]);
        Assert.Equal(1, h.TimesPerformed[ChoreKind.TakeOutTrash]);
        Assert.False(h.TimesPerformed.ContainsKey(ChoreKind.CleanFridge));
    }

    [Fact]
    public void IntendedActionKind_ChoreWork_IsPresent()
    {
        // AT-01: IntendedActionKind.ChoreWork compiles
        var kind = IntendedActionKind.ChoreWork;
        Assert.NotEqual(IntendedActionKind.Idle, kind);
        Assert.NotEqual(IntendedActionKind.Work, kind);
    }
}
