using System;
using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Components;

/// <summary>AT-01: TaskComponent construction and field assignment.</summary>
public class TaskComponentTests
{
    [Fact]
    public void DefaultConstruction_HasExpectedDefaults()
    {
        var tc = new TaskComponent();
        Assert.Equal(0f,         tc.EffortHours);
        Assert.Equal(0L,         tc.DeadlineTick);
        Assert.Equal(0,          tc.Priority);
        Assert.Equal(0f,         tc.Progress);
        Assert.Equal(0f,         tc.QualityLevel);
        Assert.Equal(Guid.Empty, tc.AssignedNpcId);
        Assert.Equal(0L,         tc.CreatedTick);
    }

    [Fact]
    public void AllFields_CanBeAssigned()
    {
        var id = Guid.NewGuid();
        var tc = new TaskComponent
        {
            EffortHours   = 4.0f,
            DeadlineTick  = 10000L,
            Priority      = 50,
            Progress      = 0.5f,
            QualityLevel  = 0.9f,
            AssignedNpcId = id,
            CreatedTick   = 5000L,
        };

        Assert.Equal(4.0f,   tc.EffortHours);
        Assert.Equal(10000L, tc.DeadlineTick);
        Assert.Equal(50,     tc.Priority);
        Assert.Equal(0.5f,   tc.Progress);
        Assert.Equal(0.9f,   tc.QualityLevel);
        Assert.Equal(id,     tc.AssignedNpcId);
        Assert.Equal(5000L,  tc.CreatedTick);
    }

    [Fact]
    public void StructEquality_SameValues_AreEqual()
    {
        var id = Guid.NewGuid();
        var a = new TaskComponent { Priority = 50, Progress = 0.5f, AssignedNpcId = id };
        var b = new TaskComponent { Priority = 50, Progress = 0.5f, AssignedNpcId = id };
        Assert.Equal(a, b);
    }

    [Fact]
    public void StructEquality_DifferentValues_AreNotEqual()
    {
        var a = new TaskComponent { Priority = 50 };
        var b = new TaskComponent { Priority = 60 };
        Assert.NotEqual(a, b);
    }
}
