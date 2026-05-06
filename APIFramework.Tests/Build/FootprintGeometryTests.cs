using APIFramework.Build;
using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Build;

/// <summary>
/// AT-03: FootprintGeometry.Overlaps exhaustive small-case matrix.
/// AT-04: FootprintGeometry.CanStackOn correct results.
/// </summary>
public class FootprintGeometryTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static BuildFootprintComponent FP(int w, int d) =>
        new() { WidthTiles = w, DepthTiles = d, CanStackOnTop = false };

    private static BuildFootprintComponent FP(int w, int d, bool canStack) =>
        new() { WidthTiles = w, DepthTiles = d, CanStackOnTop = canStack };

    // ── Overlaps ───────────────────────────────────────────────────────────────

    // AT-03: same tile, same 1×1 footprint → overlaps.
    [Fact]
    public void Overlaps_SameTile_ReturnsTrue()
    {
        Assert.True(FootprintGeometry.Overlaps(
            (0, 0), FP(1, 1),
            (0, 0), FP(1, 1)));
    }

    // AT-03: adjacent tiles, non-overlapping 1×1 footprints.
    [Fact]
    public void Overlaps_AdjacentTileX_ReturnsFalse()
    {
        Assert.False(FootprintGeometry.Overlaps(
            (0, 0), FP(1, 1),
            (1, 0), FP(1, 1)));
    }

    [Fact]
    public void Overlaps_AdjacentTileZ_ReturnsFalse()
    {
        Assert.False(FootprintGeometry.Overlaps(
            (0, 0), FP(1, 1),
            (0, 1), FP(1, 1)));
    }

    // AT-03: partial overlap on X axis only.
    [Fact]
    public void Overlaps_PartialOverlapX_ReturnsTrue()
    {
        // A occupies (0,0)–(2,1), B occupies (1,0)–(3,1) → X overlap at 1.
        Assert.True(FootprintGeometry.Overlaps(
            (0, 0), FP(2, 1),
            (1, 0), FP(2, 1)));
    }

    // AT-03: partial overlap on Z axis only.
    [Fact]
    public void Overlaps_PartialOverlapZ_ReturnsTrue()
    {
        Assert.True(FootprintGeometry.Overlaps(
            (0, 0), FP(1, 2),
            (0, 1), FP(1, 2)));
    }

    // AT-03: full overlap — identical footprints at same anchor.
    [Fact]
    public void Overlaps_FullOverlap_ReturnsTrue()
    {
        Assert.True(FootprintGeometry.Overlaps(
            (3, 3), FP(2, 2),
            (3, 3), FP(2, 2)));
    }

    // AT-03: smaller footprint contained within larger footprint.
    [Fact]
    public void Overlaps_ContainedSmallWithinLarge_ReturnsTrue()
    {
        // 1×1 inside 2×2 at same anchor.
        Assert.True(FootprintGeometry.Overlaps(
            (0, 0), FP(2, 2),
            (0, 0), FP(1, 1)));
    }

    // AT-03: no overlap on either axis (diagonal separation).
    [Fact]
    public void Overlaps_DiagonalSeparation_ReturnsFalse()
    {
        Assert.False(FootprintGeometry.Overlaps(
            (0, 0), FP(1, 1),
            (2, 2), FP(1, 1)));
    }

    // AT-03: 1×1 footprint at (0,0) is adjacent to 1×1 at (1,0) — no gap but also no overlap.
    [Fact]
    public void Overlaps_TouchingEdgeX_ReturnsFalse()
    {
        // [0..1) and [1..2) share the boundary 1 but do not overlap in half-open interval.
        Assert.False(FootprintGeometry.Overlaps(
            (0, 0), FP(1, 1),
            (1, 0), FP(1, 1)));
    }

    // ── CanStackOn ────────────────────────────────────────────────────────────

    // AT-04: monitor (1×1) on desk (2×1, canStack=true) → true.
    [Fact]
    public void CanStackOn_MonitorOnDesk_ReturnsTrue()
    {
        var monitor = FP(1, 1, canStack: false);
        var desk    = FP(2, 1, canStack: true);
        Assert.True(FootprintGeometry.CanStackOn(monitor, desk));
    }

    // AT-04: desk (2×1) on chair (1×1, canStack=false) → false.
    [Fact]
    public void CanStackOn_DeskOnChair_ReturnsFalse()
    {
        var desk  = FP(2, 1, canStack: true);
        var chair = FP(1, 1, canStack: false);
        Assert.False(FootprintGeometry.CanStackOn(desk, chair));
    }

    // AT-04: printer (1×1) on desk (2×1, canStack=true) → true.
    [Fact]
    public void CanStackOn_PrinterOnDesk_ReturnsTrue()
    {
        var printer = FP(1, 1, canStack: true);
        var desk    = FP(2, 1, canStack: true);
        Assert.True(FootprintGeometry.CanStackOn(printer, desk));
    }

    // Top prop larger than bottom → false even if canStackOnTop is true.
    [Fact]
    public void CanStackOn_TopLargerThanBottom_ReturnsFalse()
    {
        var big   = FP(3, 3, canStack: false);
        var small = FP(1, 1, canStack: true);
        Assert.False(FootprintGeometry.CanStackOn(big, small));
    }

    // Bottom canStackOnTop=false → false even if top fits.
    [Fact]
    public void CanStackOn_BottomNotStackable_ReturnsFalse()
    {
        var top    = FP(1, 1, canStack: false);
        var bottom = FP(2, 2, canStack: false);
        Assert.False(FootprintGeometry.CanStackOn(top, bottom));
    }

    // Exact same size, bottom stackable → true.
    [Fact]
    public void CanStackOn_ExactSameSizeStackable_ReturnsTrue()
    {
        var top    = FP(2, 2, canStack: false);
        var bottom = FP(2, 2, canStack: true);
        Assert.True(FootprintGeometry.CanStackOn(top, bottom));
    }
}
