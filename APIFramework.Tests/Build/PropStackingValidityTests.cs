using APIFramework.Build;
using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Build;

/// <summary>
/// Comprehensive stacking validity matrix for all v0.2 starting-palette prop pairs.
///
/// Uses FootprintGeometry.CanStackOn (the canonical check) and FootprintGeometry.CanPlaceAt
/// (multi-tile placement). All catalog values sourced from prop-footprints.json v0.1.0.
/// </summary>
public class PropStackingValidityTests
{
    // ── Prop footprints (from catalog v0.1.0) ─────────────────────────────────

    private static BuildFootprintComponent CubeWall  => new() { WidthTiles = 1, DepthTiles = 1, TopHeight = 2.50f, CanStackOnTop = false };
    private static BuildFootprintComponent Door      => new() { WidthTiles = 1, DepthTiles = 1, TopHeight = 2.20f, CanStackOnTop = false };
    private static BuildFootprintComponent Desk      => new() { WidthTiles = 2, DepthTiles = 1, TopHeight = 0.75f, CanStackOnTop = true  };
    private static BuildFootprintComponent Chair     => new() { WidthTiles = 1, DepthTiles = 1, TopHeight = 0.45f, CanStackOnTop = false };
    private static BuildFootprintComponent Monitor   => new() { WidthTiles = 1, DepthTiles = 1, TopHeight = 0.40f, CanStackOnTop = false };
    private static BuildFootprintComponent Computer  => new() { WidthTiles = 1, DepthTiles = 1, TopHeight = 0.45f, CanStackOnTop = false };
    private static BuildFootprintComponent Printer   => new() { WidthTiles = 1, DepthTiles = 1, TopHeight = 0.35f, CanStackOnTop = true  };
    private static BuildFootprintComponent Banana    => new() { WidthTiles = 1, DepthTiles = 1, TopHeight = 0.05f, CanStackOnTop = false };

    // ── Valid stacking combinations ───────────────────────────────────────────

    [Fact] public void Monitor_OnDesk_Valid()   => Assert.True(FootprintGeometry.CanStackOn(Monitor,  Desk));
    [Fact] public void Computer_OnDesk_Valid()  => Assert.True(FootprintGeometry.CanStackOn(Computer, Desk));
    [Fact] public void Printer_OnDesk_Valid()   => Assert.True(FootprintGeometry.CanStackOn(Printer,  Desk));
    [Fact] public void Desk_OnDesk_Valid()      => Assert.True(FootprintGeometry.CanStackOn(Desk,     Desk));

    // ── Invalid stacking combinations (non-stackable bottoms) ────────────────

    [Fact] public void AnyProp_OnCubeWall_Rejected()   => Assert.False(FootprintGeometry.CanStackOn(Desk,    CubeWall));
    [Fact] public void AnyProp_OnDoor_Rejected()       => Assert.False(FootprintGeometry.CanStackOn(Monitor, Door));
    [Fact] public void Table_OnBanana_Rejected()       => Assert.False(FootprintGeometry.CanStackOn(Desk,    Banana));
    [Fact] public void Desk_OnChair_Rejected()         => Assert.False(FootprintGeometry.CanStackOn(Desk,    Chair));
    [Fact] public void Monitor_OnChair_Rejected()      => Assert.False(FootprintGeometry.CanStackOn(Monitor, Chair));
    [Fact] public void Monitor_OnMonitor_Rejected()    => Assert.False(FootprintGeometry.CanStackOn(Monitor, Monitor));
    [Fact] public void Desk_OnComputer_Rejected()      => Assert.False(FootprintGeometry.CanStackOn(Desk,    Computer));

    // ── Footprint-size mismatches (top is larger than bottom) ─────────────────

    [Fact]
    public void Desk_OnPrinter_Rejected_FootprintTooLarge()
    {
        // Printer is 1×1, desk is 2×1 — desk footprint exceeds printer's.
        Assert.False(FootprintGeometry.CanStackOn(Desk, Printer),
            "Desk (2×1) cannot stack on Printer (1×1) — footprint too large.");
    }

    // ── Multi-tile placement (CanPlaceAt) ─────────────────────────────────────

    [Fact]
    public void Desk_OnTwoAdjacentEmptyTiles_Valid()
    {
        bool valid = FootprintGeometry.CanPlaceAt((3, 3), Desk, _ => null, out float y);
        Assert.True(valid);
        Assert.Equal(0f, y, precision: 5);
    }

    [Fact]
    public void Monitor_OnSingleEmptyTile_Valid()
    {
        bool valid = FootprintGeometry.CanPlaceAt((3, 3), Monitor, _ => null, out float y);
        Assert.True(valid);
        Assert.Equal(0f, y, precision: 5);
    }

    [Fact]
    public void Monitor_OnDesk_TwoTilesOccupied_Valid()
    {
        // Desk spans (3,3) and (4,3). Monitor placed at (3,3).
        var desk = Desk;
        System.Func<(int, int), (string, BuildFootprintComponent)?> occ =
            t => (t == (3, 3) || t == (4, 3)) ? ("desk-1", desk) : ((string, BuildFootprintComponent)?)null;

        bool valid = FootprintGeometry.CanPlaceAt((3, 3), Monitor, occ, out float y);
        Assert.True(valid);
        Assert.Equal(desk.BottomHeight + desk.TopHeight, y, precision: 5);
    }

    [Fact]
    public void Desk_HalfOnEmptyHalfOnChair_Rejected()
    {
        var chair = Chair;
        System.Func<(int, int), (string, BuildFootprintComponent)?> occ =
            t => t == (3, 3) ? ("chair-1", chair) : ((string, BuildFootprintComponent)?)null;

        bool valid = FootprintGeometry.CanPlaceAt((3, 3), Desk, occ, out _);
        Assert.False(valid, "Desk half on chair, half on empty floor must be rejected.");
    }

    [Fact]
    public void Desk_AcrossTwoDifferentProps_Rejected()
    {
        var chair = Chair;
        var banana = Banana;
        System.Func<(int, int), (string, BuildFootprintComponent)?> occ =
            t => t == (3, 3) ? ("chair-1", chair)
               : t == (4, 3) ? ("banana-1", banana)
               : ((string, BuildFootprintComponent)?)null;

        bool valid = FootprintGeometry.CanPlaceAt((3, 3), Desk, occ, out _);
        Assert.False(valid, "Desk spanning two different props must be rejected.");
    }

    // ── CanPlaceAt: surface Y matches target.BottomHeight + target.TopHeight ──

    [Fact]
    public void CanPlaceAt_ValidStack_SurfaceY_EqualsBottomPlusTop()
    {
        var desk = Desk;
        System.Func<(int, int), (string, BuildFootprintComponent)?> occ =
            t => t == (7, 2) ? ("desk-1", desk) : ((string, BuildFootprintComponent)?)null;

        FootprintGeometry.CanPlaceAt((7, 2), Monitor, occ, out float y);
        Assert.Equal(desk.BottomHeight + desk.TopHeight, y, precision: 5);
    }

    [Fact]
    public void CanPlaceAt_InvalidStack_SurfaceY_IsZero()
    {
        var banana = Banana;
        System.Func<(int, int), (string, BuildFootprintComponent)?> occ =
            t => t == (7, 2) ? ("banana-1", banana) : ((string, BuildFootprintComponent)?)null;

        FootprintGeometry.CanPlaceAt((7, 2), Desk, occ, out float y);
        Assert.Equal(0f, y, precision: 5);
    }

    // ── Catalog-driven round-trip: verify all 8 props parse correctly ─────────

    [Fact]
    public void Catalog_AllEightV02Props_ParseCorrectly()
    {
        const string json = @"{
  ""schemaVersion"": ""0.1.0"",
  ""propFootprints"": [
    { ""propTypeId"": ""cube-wall"", ""widthTiles"": 1, ""depthTiles"": 1, ""bottomHeight"": 0.0, ""topHeight"": 2.5,  ""canStackOnTop"": false, ""footprintCategory"": ""Wall"" },
    { ""propTypeId"": ""door"",      ""widthTiles"": 1, ""depthTiles"": 1, ""bottomHeight"": 0.0, ""topHeight"": 2.2,  ""canStackOnTop"": false, ""footprintCategory"": ""Wall"" },
    { ""propTypeId"": ""desk"",      ""widthTiles"": 2, ""depthTiles"": 1, ""bottomHeight"": 0.0, ""topHeight"": 0.75, ""canStackOnTop"": true,  ""footprintCategory"": ""Furniture"" },
    { ""propTypeId"": ""chair"",     ""widthTiles"": 1, ""depthTiles"": 1, ""bottomHeight"": 0.0, ""topHeight"": 0.45, ""canStackOnTop"": false, ""footprintCategory"": ""Furniture"" },
    { ""propTypeId"": ""monitor"",   ""widthTiles"": 1, ""depthTiles"": 1, ""bottomHeight"": 0.0, ""topHeight"": 0.40, ""canStackOnTop"": false, ""footprintCategory"": ""DeskAccessory"" },
    { ""propTypeId"": ""computer"",  ""widthTiles"": 1, ""depthTiles"": 1, ""bottomHeight"": 0.0, ""topHeight"": 0.45, ""canStackOnTop"": false, ""footprintCategory"": ""DeskAccessory"" },
    { ""propTypeId"": ""printer"",   ""widthTiles"": 1, ""depthTiles"": 1, ""bottomHeight"": 0.0, ""topHeight"": 0.35, ""canStackOnTop"": true,  ""footprintCategory"": ""DeskAccessory"" },
    { ""propTypeId"": ""banana"",    ""widthTiles"": 1, ""depthTiles"": 1, ""bottomHeight"": 0.0, ""topHeight"": 0.05, ""canStackOnTop"": false, ""footprintCategory"": ""FloorProp"" }
  ]
}";
        var catalog = BuildFootprintCatalog.ParseJson(json);
        Assert.Equal(8, catalog.AllPropTypeIds.Count);

        var banana = catalog.GetByPropType("banana")!;
        Assert.False(banana.CanStackOnTop);
        Assert.Equal("FloorProp", banana.FootprintCategory);

        var desk = catalog.GetByPropType("desk")!;
        Assert.True(desk.CanStackOnTop);
        Assert.Equal(2, desk.WidthTiles);
    }
}
