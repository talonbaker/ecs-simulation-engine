using APIFramework.Systems.Animation;
using Xunit;

namespace APIFramework.Tests.Systems.Animation;

/// <summary>AT-12: Per-archetype timing — Old Hand walks slower; Newbie eats faster.</summary>
public class PerArchetypeAnimationTimingTests
{
    private readonly AnimationTimingCatalog _catalog = AnimationTimingCatalog.Default;

    [Fact]
    public void OldHand_WalkSpeedMult_IsLessThanNewbie()
    {
        float oldHand = _catalog.GetWalkSpeedMult("the-old-hand");
        float newbie  = _catalog.GetWalkSpeedMult("the-newbie");

        Assert.True(oldHand < newbie, $"Old Hand ({oldHand}) should walk slower than Newbie ({newbie}).");
    }

    [Fact]
    public void Hermit_WalkSpeedMult_IsLessThanClimber()
    {
        float hermit  = _catalog.GetWalkSpeedMult("the-hermit");
        float climber = _catalog.GetWalkSpeedMult("the-climber");

        Assert.True(hermit < climber, $"Hermit ({hermit}) should walk slower than Climber ({climber}).");
    }

    [Fact]
    public void OldHand_EatSpeedMult_IsLessThanNewbie()
    {
        float oldHand = _catalog.GetEatSpeedMult("the-old-hand");
        float newbie  = _catalog.GetEatSpeedMult("the-newbie");

        Assert.True(oldHand < newbie, $"Old Hand ({oldHand}) should eat slower than Newbie ({newbie}).");
    }

    [Fact]
    public void OldHand_WalkSpeedMult_MatchesSpec()
    {
        Assert.Equal(0.85f, _catalog.GetWalkSpeedMult("the-old-hand"), precision: 4);
    }

    [Fact]
    public void Newbie_WalkSpeedMult_MatchesSpec()
    {
        Assert.Equal(1.15f, _catalog.GetWalkSpeedMult("the-newbie"), precision: 4);
    }

    [Fact]
    public void Climber_WalkSpeedMult_MatchesSpec()
    {
        Assert.Equal(1.20f, _catalog.GetWalkSpeedMult("the-climber"), precision: 4);
    }

    [Fact]
    public void Hermit_WalkSpeedMult_MatchesSpec()
    {
        Assert.Equal(0.80f, _catalog.GetWalkSpeedMult("the-hermit"), precision: 4);
    }

    [Fact]
    public void UnknownArchetype_ReturnsFallback_1f()
    {
        Assert.Equal(1f, _catalog.GetWalkSpeedMult("the-unknown-archetype"), precision: 4);
        Assert.Equal(1f, _catalog.GetEatSpeedMult("the-unknown-archetype"), precision: 4);
        Assert.Equal(1f, _catalog.GetTalkGesturalRate("the-unknown-archetype"), precision: 4);
    }

    [Fact]
    public void LookupIsCaseInsensitive()
    {
        float lower = _catalog.GetWalkSpeedMult("the-old-hand");
        float upper = _catalog.GetWalkSpeedMult("THE-OLD-HAND");
        Assert.Equal(lower, upper, precision: 4);
    }
}
