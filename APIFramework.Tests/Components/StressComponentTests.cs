using System;
using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Components;

/// <summary>AT-01: StressComponent construction, clamping, and equality.</summary>
public class StressComponentTests
{
    [Fact]
    public void DefaultConstruction_AllZero()
    {
        var s = new StressComponent();
        Assert.Equal(0, s.AcuteLevel);
        Assert.Equal(0.0, s.ChronicLevel);
        Assert.Equal(0, s.LastDayUpdated);
        Assert.Equal(0, s.SuppressionEventsToday);
        Assert.Equal(0, s.DriveSpikeEventsToday);
        Assert.Equal(0, s.SocialConflictEventsToday);
        Assert.Equal(0, s.BurnoutLastAppliedDay);
    }

    [Fact]
    public void AcuteLevel_ClampedTo0_100()
    {
        var s = new StressComponent { AcuteLevel = 50 };

        s.AcuteLevel = Math.Clamp(s.AcuteLevel - 200, 0, 100);
        Assert.Equal(0, s.AcuteLevel);

        s.AcuteLevel = Math.Clamp(200, 0, 100);
        Assert.Equal(100, s.AcuteLevel);
    }

    [Fact]
    public void ChronicLevel_ClampedTo0_100()
    {
        var s = new StressComponent { ChronicLevel = 50.0 };

        s.ChronicLevel = Math.Clamp(s.ChronicLevel - 200, 0, 100);
        Assert.Equal(0.0, s.ChronicLevel);

        s.ChronicLevel = Math.Clamp(200.0, 0, 100);
        Assert.Equal(100.0, s.ChronicLevel);
    }

    [Fact]
    public void Equality_SameValues_Equal()
    {
        var a = new StressComponent
        {
            AcuteLevel = 30, ChronicLevel = 25.5, LastDayUpdated = 3,
            SuppressionEventsToday = 2, DriveSpikeEventsToday = 1,
            SocialConflictEventsToday = 0, BurnoutLastAppliedDay = 0
        };
        var b = new StressComponent
        {
            AcuteLevel = 30, ChronicLevel = 25.5, LastDayUpdated = 3,
            SuppressionEventsToday = 2, DriveSpikeEventsToday = 1,
            SocialConflictEventsToday = 0, BurnoutLastAppliedDay = 0
        };
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentAcuteLevel_NotEqual()
    {
        var a = new StressComponent { AcuteLevel = 10 };
        var b = new StressComponent { AcuteLevel = 20 };
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentChronicLevel_NotEqual()
    {
        var a = new StressComponent { ChronicLevel = 10.0 };
        var b = new StressComponent { ChronicLevel = 20.0 };
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void CanBeAddedToEntity()
    {
        var em = new APIFramework.Core.EntityManager();
        var e  = em.CreateEntity();
        e.Add(new StressComponent { AcuteLevel = 42, ChronicLevel = 30.0 });

        Assert.True(e.Has<StressComponent>());
        Assert.Equal(42, e.Get<StressComponent>().AcuteLevel);
    }
}
