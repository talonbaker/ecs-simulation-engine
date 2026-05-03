using APIFramework.Build;
using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Build;

/// <summary>
/// Regression tests for BUG-001: large prop placed on top of small prop causes
/// disappear / incorrect placement.
///
/// AT-01  BUG-001 regression: table on banana → rejected; banana remains.
/// AT-02  Monitor on desk → valid; monitor lands at desk's TopHeight.
/// AT-03  Desk on chair → rejected.
/// AT-04  Printer on desk → valid; printer lands at desk's TopHeight.
/// AT-05  Two-tile desk half on stackable surface, half on empty floor → rejected.
/// AT-06  Two-tile desk on two adjacent empty floor tiles → valid.
/// AT-09  Placement validity is false for invalid config → mutation must not be called.
/// </summary>
public class Bug001RegressionTests
{
    // ── Prop footprint fixtures ───────────────────────────────────────────────

    private static BuildFootprintComponent Banana => new()
        { WidthTiles = 1, DepthTiles = 1, BottomHeight = 0f, TopHeight = 0.05f, CanStackOnTop = false };

    private static BuildFootprintComponent Table => new()
        { WidthTiles = 2, DepthTiles = 1, BottomHeight = 0f, TopHeight = 0.75f, CanStackOnTop = true };

    private static BuildFootprintComponent Desk => new()
        { WidthTiles = 2, DepthTiles = 1, BottomHeight = 0f, TopHeight = 0.75f, CanStackOnTop = true };

    private static BuildFootprintComponent Chair => new()
        { WidthTiles = 1, DepthTiles = 1, BottomHeight = 0f, TopHeight = 0.45f, CanStackOnTop = false };

    private static BuildFootprintComponent Monitor => new()
        { WidthTiles = 1, DepthTiles = 1, BottomHeight = 0f, TopHeight = 0.40f, CanStackOnTop = false };

    private static BuildFootprintComponent Printer => new()
        { WidthTiles = 1, DepthTiles = 1, BottomHeight = 0f, TopHeight = 0.35f, CanStackOnTop = true };

    private static BuildFootprintComponent SmallPlatform => new()
        { WidthTiles = 1, DepthTiles = 1, BottomHeight = 0f, TopHeight = 0.30f, CanStackOnTop = true };

    // ── AT-01: BUG-001 regression ─────────────────────────────────────────────

    [Fact]
    public void Bug001_TableOnBanana_PlacementRejected()
    {
        // Pre-fix: table settled at incorrect Y; banana was not displaced.
        // Post-fix: CanStackOn returns false → placement is rejected.
        Assert.False(FootprintGeometry.CanStackOn(Table, Banana),
            "Table on banana must be rejected (banana.CanStackOnTop = false).");
    }

    [Fact]
    public void Bug001_TableOnBanana_CanPlaceAt_Rejected()
    {
        var occupancy = SingleTileOccupancy((5, 5), "banana-1", Banana);
        bool valid = FootprintGeometry.CanPlaceAt((5, 5), Table, occupancy, out _);
        Assert.False(valid, "Table anchored at (5,5) on banana (1×1, non-stackable) must be rejected.");
    }

    // ── AT-02: Monitor on desk ────────────────────────────────────────────────

    [Fact]
    public void MonitorOnDesk_CanStackOn_Valid()
    {
        Assert.True(FootprintGeometry.CanStackOn(Monitor, Desk),
            "Monitor (1×1) fits within desk (2×1) and desk.CanStackOnTop = true.");
    }

    [Fact]
    public void MonitorOnDesk_SurfaceY_IsDeskTopHeight()
    {
        var occupancy = DeskOccupancy();
        bool valid = FootprintGeometry.CanPlaceAt((5, 5), Monitor, occupancy, out float surfaceY);
        Assert.True(valid);
        Assert.Equal(Desk.BottomHeight + Desk.TopHeight, surfaceY, precision: 5);
    }

    // ── AT-03: Desk on chair ──────────────────────────────────────────────────

    [Fact]
    public void DeskOnChair_CanStackOn_Rejected()
    {
        Assert.False(FootprintGeometry.CanStackOn(Desk, Chair),
            "Desk on chair must be rejected (chair.CanStackOnTop = false).");
    }

    // ── AT-04: Printer on desk ────────────────────────────────────────────────

    [Fact]
    public void PrinterOnDesk_CanStackOn_Valid()
    {
        Assert.True(FootprintGeometry.CanStackOn(Printer, Desk),
            "Printer (1×1) fits within desk (2×1) and desk.CanStackOnTop = true.");
    }

    [Fact]
    public void PrinterOnDesk_SurfaceY_IsDeskTopHeight()
    {
        var occupancy = DeskOccupancy();
        bool valid = FootprintGeometry.CanPlaceAt((5, 5), Printer, occupancy, out float surfaceY);
        Assert.True(valid);
        Assert.Equal(Desk.BottomHeight + Desk.TopHeight, surfaceY, precision: 5);
    }

    // ── AT-05: Two-tile desk half on stackable, half on floor ─────────────────

    [Fact]
    public void Desk_HalfOnStackableSurface_HalfOnEmptyFloor_Rejected()
    {
        // Desk occupies (5,5) and (6,5). Only (5,5) has a stackable platform; (6,5) is empty.
        var occupancy = SingleTileOccupancy((5, 5), "platform-1", SmallPlatform);
        bool valid = FootprintGeometry.CanPlaceAt((5, 5), Desk, occupancy, out _);
        Assert.False(valid, "Desk half on stackable surface, half on empty floor must be rejected.");
    }

    // ── AT-06: Two-tile desk on two adjacent empty tiles ─────────────────────

    [Fact]
    public void Desk_OnTwoAdjacentEmptyTiles_Valid()
    {
        bool valid = FootprintGeometry.CanPlaceAt((5, 5), Desk, _ => null, out float surfaceY);
        Assert.True(valid, "Desk on two adjacent empty tiles must be valid (floor placement).");
        Assert.Equal(0f, surfaceY, precision: 5);
    }

    // ── AT-09: Invalid placement → mutation not called ────────────────────────

    [Fact]
    public void InvalidPlacement_ValidityFalse_MutationMustNotBeCalled()
    {
        // Validity is the gate. When CanPlaceAt returns false, the caller must not call mutation.
        var occupancy = SingleTileOccupancy((5, 5), "banana-1", Banana);
        bool valid = FootprintGeometry.CanPlaceAt((5, 5), Table, occupancy, out _);
        // The assertion here proves the gate returns false. Callers (DragHandler, BuildModeController)
        // are responsible for not calling AddEntity/SpawnStructural when valid = false.
        Assert.False(valid, "Placement validity gate must return false for invalid config.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static System.Func<(int, int), (string, BuildFootprintComponent)?> SingleTileOccupancy(
        (int X, int Z) tile, string propId, BuildFootprintComponent fp) =>
        t => t == tile ? (propId, fp) : ((string, BuildFootprintComponent)?)null;

    // Desk occupies tiles (5,5) and (6,5) — same propId for both.
    private static System.Func<(int, int), (string, BuildFootprintComponent)?> DeskOccupancy()
    {
        var desk = Desk;
        return t => (t == (5, 5) || t == (6, 5)) ? ("desk-1", desk) : ((string, BuildFootprintComponent)?)null;
    }
}
