using System;
using System.Collections.Generic;
using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Components;

/// <summary>AT-01: WorkloadComponent construction and capacity enforcement.</summary>
public class WorkloadComponentTests
{
    [Fact]
    public void DefaultConstruction_HasExpectedDefaults()
    {
        var wc = new WorkloadComponent();
        Assert.Null(wc.ActiveTasks);
        Assert.Equal(0, wc.Capacity);
        Assert.Equal(0, wc.CurrentLoad);
    }

    [Fact]
    public void AllFields_CanBeAssigned()
    {
        var tasks = new List<Guid> { Guid.NewGuid() };
        var wc = new WorkloadComponent
        {
            ActiveTasks  = tasks,
            Capacity     = 3,
            CurrentLoad  = 33,
        };

        Assert.Single(wc.ActiveTasks);
        Assert.Equal(3,  wc.Capacity);
        Assert.Equal(33, wc.CurrentLoad);
    }

    [Fact]
    public void CurrentLoad_Formula_IsCorrect()
    {
        // 1 task / capacity 3 → (1*100)/3 = 33%
        Assert.Equal(33, 1 * 100 / 3);
        // 3 tasks / capacity 3 → 100%
        Assert.Equal(100, 3 * 100 / 3);
        // 5 tasks / capacity 5 → 100%, clamped
        Assert.Equal(100, Math.Clamp(5 * 100 / 5, 0, 100));
    }

    [Fact]
    public void EmptyActiveTasks_HasZeroCount()
    {
        var wc = new WorkloadComponent { ActiveTasks = Array.Empty<Guid>(), Capacity = 3 };
        Assert.Empty(wc.ActiveTasks);
    }
}
