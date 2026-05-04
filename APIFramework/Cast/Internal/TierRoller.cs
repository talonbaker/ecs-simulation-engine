namespace APIFramework.Cast.Internal;

/// <summary>
/// Maps a uniform [0,1) roll to a <see cref="CastNameTier"/> using cumulative thresholds
/// from <see cref="TierThresholdsDto"/>. Direct port of the JS source's threshold cascade.
/// </summary>
internal static class TierRoller
{
    public static CastNameTier Roll(double r, TierThresholdsDto t)
    {
        if (r < t.Common)    return CastNameTier.Common;
        if (r < t.Uncommon)  return CastNameTier.Uncommon;
        if (r < t.Rare)      return CastNameTier.Rare;
        if (r < t.Epic)      return CastNameTier.Epic;
        if (r < t.Legendary) return CastNameTier.Legendary;
        return CastNameTier.Mythic;
    }
}
