using System;
using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Components;

public class ThrownVelocityComponentTests
{
    [Fact]
    public void DefaultConstruction_AllFieldsZero()
    {
        var c = new ThrownVelocityComponent();
        Assert.Equal(0f, c.VelocityX);
        Assert.Equal(0f, c.VelocityZ);
        Assert.Equal(0f, c.VelocityY);
        Assert.Equal(0f, c.DecayPerTick);
        Assert.Equal(0L, c.ThrownAtTick);
        Assert.Equal(Guid.Empty, c.ThrownByEntityId);
    }

    [Fact]
    public void SetFields_RoundTrip()
    {
        var id = Guid.NewGuid();
        var c = new ThrownVelocityComponent
        {
            VelocityX        = 5f,
            VelocityZ        = 0f,
            VelocityY        = 3f,
            DecayPerTick     = 0.10f,
            ThrownAtTick     = 42L,
            ThrownByEntityId = id
        };
        Assert.Equal(5f, c.VelocityX);
        Assert.Equal(0f, c.VelocityZ);
        Assert.Equal(3f, c.VelocityY);
        Assert.Equal(0.10f, c.DecayPerTick);
        Assert.Equal(42L, c.ThrownAtTick);
        Assert.Equal(id, c.ThrownByEntityId);
    }
}
