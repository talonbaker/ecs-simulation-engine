using APIFramework.Components;

namespace APIFramework.Build;

/// <summary>
/// Pure utility functions for footprint-based spatial queries.
/// No state, no side effects — safe to call from any context.
/// </summary>
public static class FootprintGeometry
{
    /// <summary>
    /// Returns true if the XZ tile extents of footprintA (anchored at anchorA)
    /// and footprintB (anchored at anchorB) overlap.
    /// Heights are not considered — this is a tile-level XZ check only.
    /// Footprints occupy [anchor.X, anchor.X + width) × [anchor.Z, anchor.Z + depth).
    /// </summary>
    public static bool Overlaps(
        (int X, int Z) anchorA, BuildFootprintComponent footprintA,
        (int X, int Z) anchorB, BuildFootprintComponent footprintB)
    {
        int aMinX = anchorA.X, aMaxX = anchorA.X + footprintA.WidthTiles;
        int aMinZ = anchorA.Z, aMaxZ = anchorA.Z + footprintA.DepthTiles;

        int bMinX = anchorB.X, bMaxX = anchorB.X + footprintB.WidthTiles;
        int bMinZ = anchorB.Z, bMaxZ = anchorB.Z + footprintB.DepthTiles;

        return aMinX < bMaxX && aMaxX > bMinX &&
               aMinZ < bMaxZ && aMaxZ > bMinZ;
    }

    /// <summary>
    /// Returns true if <paramref name="topProp"/> can rest on <paramref name="bottomProp"/>.
    /// Requires: bottomProp.CanStackOnTop is true, and topProp fits within bottomProp's
    /// XZ footprint (width and depth both ≤).
    /// </summary>
    public static bool CanStackOn(
        BuildFootprintComponent topProp,
        BuildFootprintComponent bottomProp)
    {
        if (!bottomProp.CanStackOnTop) return false;
        return topProp.WidthTiles <= bottomProp.WidthTiles &&
               topProp.DepthTiles <= bottomProp.DepthTiles;
    }
}
