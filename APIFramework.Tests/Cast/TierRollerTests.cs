using APIFramework.Cast;
using APIFramework.Cast.Internal;
using Xunit;

namespace APIFramework.Tests.Cast;

/// <summary>
/// Unit tests for the threshold cascade. Defaults: 0.55 / 0.82 / 0.94 / 0.98 / 0.995 / 1.0.
/// </summary>
public class TierRollerTests
{
    private static readonly TierThresholdsDto Default = new();

    [Theory]
    [InlineData(0.0,    CastNameTier.Common)]
    [InlineData(0.49,   CastNameTier.Common)]
    [InlineData(0.5499, CastNameTier.Common)]
    [InlineData(0.55,   CastNameTier.Uncommon)]
    [InlineData(0.81,   CastNameTier.Uncommon)]
    [InlineData(0.8199, CastNameTier.Uncommon)]
    [InlineData(0.82,   CastNameTier.Rare)]
    [InlineData(0.93,   CastNameTier.Rare)]
    [InlineData(0.9399, CastNameTier.Rare)]
    [InlineData(0.94,   CastNameTier.Epic)]
    [InlineData(0.97,   CastNameTier.Epic)]
    [InlineData(0.9799, CastNameTier.Epic)]
    [InlineData(0.98,   CastNameTier.Legendary)]
    [InlineData(0.994,  CastNameTier.Legendary)]
    [InlineData(0.995,  CastNameTier.Mythic)]
    [InlineData(0.999,  CastNameTier.Mythic)]
    public void Roll_RespectsDefaultThresholds(double r, CastNameTier expected)
    {
        Assert.Equal(expected, TierRoller.Roll(r, Default));
    }
}
