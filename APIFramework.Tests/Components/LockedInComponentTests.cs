using System;
using Xunit;
using APIFramework.Components;

namespace APIFramework.Tests.Components;

public class LockedInComponentTests
{
    [Fact]
    public void Construction_Succeeds()
    {
        var component = new LockedInComponent
        {
            FirstDetectedTick = 100,
            StarvationTickBudget = 5
        };
        Assert.Equal(100, component.FirstDetectedTick);
        Assert.Equal(5, component.StarvationTickBudget);
    }

    [Fact]
    public void FirstDetectedTick_CanBeZero()
    {
        var component = new LockedInComponent
        {
            FirstDetectedTick = 0,
            StarvationTickBudget = 5
        };
        Assert.Equal(0, component.FirstDetectedTick);
    }

    [Fact]
    public void StarvationTickBudget_CanBeDecremented()
    {
        var component = new LockedInComponent
        {
            FirstDetectedTick = 100,
            StarvationTickBudget = 5
        };
        component.StarvationTickBudget--;
        Assert.Equal(4, component.StarvationTickBudget);
    }

    [Fact]
    public void StarvationTickBudget_CanBeNegative()
    {
        var component = new LockedInComponent
        {
            FirstDetectedTick = 100,
            StarvationTickBudget = 0
        };
        component.StarvationTickBudget--;
        Assert.Equal(-1, component.StarvationTickBudget);
    }
}
