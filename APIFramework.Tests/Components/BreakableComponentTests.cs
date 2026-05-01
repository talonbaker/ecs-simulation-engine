using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Components;

public class BreakableComponentTests
{
    [Fact]
    public void DefaultConstruction_HitEnergyThreshold_IsZero()
    {
        var c = new BreakableComponent();
        Assert.Equal(0f, c.HitEnergyThreshold);
        Assert.Equal(BreakageBehavior.Despawn, c.OnBreak);
    }

    [Theory]
    [InlineData(BreakageBehavior.Despawn)]
    [InlineData(BreakageBehavior.SpawnLiquidStain)]
    [InlineData(BreakageBehavior.SpawnGlassShards)]
    [InlineData(BreakageBehavior.SpawnDebris)]
    public void OnBreak_AllValues_RoundTrip(BreakageBehavior behavior)
    {
        var c = new BreakableComponent { OnBreak = behavior, HitEnergyThreshold = 10f };
        Assert.Equal(behavior, c.OnBreak);
        Assert.Equal(10f, c.HitEnergyThreshold);
    }

    [Fact]
    public void GlassPane_Configuration_IsExpected()
    {
        var c = new BreakableComponent
        {
            HitEnergyThreshold = 20f,
            OnBreak = BreakageBehavior.SpawnGlassShards
        };
        Assert.Equal(BreakageBehavior.SpawnGlassShards, c.OnBreak);
        Assert.Equal(20f, c.HitEnergyThreshold);
    }
}
