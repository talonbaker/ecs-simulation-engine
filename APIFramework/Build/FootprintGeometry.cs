using System;
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

    /// <summary>
    /// Returns true if <paramref name="dragged"/> can be placed at <paramref name="anchor"/>.
    ///
    /// The tile query returns a (propId, footprint) pair for any prop occupying a given tile,
    /// or null if the tile is empty. All tiles of the dragged footprint must either be
    /// (a) all empty — floor placement — or (b) all occupied by the same stackable prop whose
    /// footprint is large enough to support the dragged prop via <see cref="CanStackOn"/>.
    ///
    /// Mixed occupancy (some tiles empty, some occupied, or two different props) is invalid.
    /// </summary>
    /// <param name="anchor">Top-left anchor tile of the dragged prop.</param>
    /// <param name="dragged">Footprint of the prop being placed.</param>
    /// <param name="tileQuery">Returns (propId, footprint) for the prop at a tile, or null if empty.</param>
    /// <param name="surfaceY">The computed surface Y for the placement; 0 on invalid.</param>
    public static bool CanPlaceAt(
        (int X, int Z) anchor,
        BuildFootprintComponent dragged,
        Func<(int X, int Z), (string PropId, BuildFootprintComponent Footprint)?> tileQuery,
        out float surfaceY)
    {
        surfaceY = 0f;
        string? stackTargetId = null;
        BuildFootprintComponent stackTargetFp = default;
        bool hasEmpty = false;

        for (int dx = 0; dx < dragged.WidthTiles; dx++)
        for (int dz = 0; dz < dragged.DepthTiles; dz++)
        {
            var result = tileQuery((anchor.X + dx, anchor.Z + dz));
            if (result is null)
            {
                hasEmpty = true;
                if (stackTargetId is not null) return false;
            }
            else
            {
                if (hasEmpty) return false;
                var (propId, fp) = result.Value;
                if (stackTargetId is null) { stackTargetId = propId; stackTargetFp = fp; }
                else if (stackTargetId != propId) return false;
            }
        }

        if (stackTargetId is null) { surfaceY = 0f; return true; }

        if (CanStackOn(dragged, stackTargetFp))
        {
            surfaceY = stackTargetFp.BottomHeight + stackTargetFp.TopHeight;
            return true;
        }
        return false;
    }
}
