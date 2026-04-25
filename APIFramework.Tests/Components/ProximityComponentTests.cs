using APIFramework.Components;
using APIFramework.Config;
using Xunit;

namespace APIFramework.Tests.Components;

/// <summary>AT-01 (proximity portion): ProximityComponent range bounds and config alignment.</summary>
public class ProximityComponentTests
{
    [Fact]
    public void Default_AllRangesPositive()
    {
        var p = ProximityComponent.Default;
        Assert.True(p.ConversationRangeTiles > 0);
        Assert.True(p.AwarenessRangeTiles    > 0);
        Assert.True(p.SightRangeTiles        > 0);
    }

    [Fact]
    public void Default_RangesInAscendingOrder()
    {
        var p = ProximityComponent.Default;
        Assert.True(p.ConversationRangeTiles <= p.AwarenessRangeTiles,
            "Conversation range should be ≤ awareness range");
        Assert.True(p.AwarenessRangeTiles    <= p.SightRangeTiles,
            "Awareness range should be ≤ sight range");
    }

    [Fact]
    public void Default_MatchesSimConfigDefaults()
    {
        // The hardcoded defaults in ProximityComponent.Default must match SimConfig.Spatial.ProximityRangeDefaults.
        var cfg  = new SimConfig();
        var prox = ProximityComponent.Default;

        Assert.Equal(cfg.Spatial.ProximityRangeDefaults.ConversationTiles, prox.ConversationRangeTiles);
        Assert.Equal(cfg.Spatial.ProximityRangeDefaults.AwarenessTiles,    prox.AwarenessRangeTiles);
        Assert.Equal(cfg.Spatial.ProximityRangeDefaults.SightTiles,        prox.SightRangeTiles);
    }

    [Fact]
    public void Mutable_CanOverrideFields()
    {
        var p = new ProximityComponent { ConversationRangeTiles = 5, AwarenessRangeTiles = 15, SightRangeTiles = 50 };
        Assert.Equal(5,  p.ConversationRangeTiles);
        Assert.Equal(15, p.AwarenessRangeTiles);
        Assert.Equal(50, p.SightRangeTiles);
    }
}
