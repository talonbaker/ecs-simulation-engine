using APIFramework.Build;
using Xunit;

namespace APIFramework.Tests.Build;

/// <summary>
/// AT-01: All seven v0.2 starting-palette props have catalog entries.
/// AT-02: All catalog values are in valid range.
/// </summary>
public class BuildFootprintCatalogTests
{
    private const string CatalogJson = @"{
  ""schemaVersion"": ""0.1.0"",
  ""propFootprints"": [
    { ""propTypeId"": ""cube-wall"",  ""widthTiles"": 1, ""depthTiles"": 1, ""bottomHeight"": 0.0, ""topHeight"": 2.5,  ""canStackOnTop"": false, ""footprintCategory"": ""Wall"" },
    { ""propTypeId"": ""door"",       ""widthTiles"": 1, ""depthTiles"": 1, ""bottomHeight"": 0.0, ""topHeight"": 2.2,  ""canStackOnTop"": false, ""footprintCategory"": ""Wall"" },
    { ""propTypeId"": ""desk"",       ""widthTiles"": 2, ""depthTiles"": 1, ""bottomHeight"": 0.0, ""topHeight"": 0.75, ""canStackOnTop"": true,  ""footprintCategory"": ""Furniture"" },
    { ""propTypeId"": ""chair"",      ""widthTiles"": 1, ""depthTiles"": 1, ""bottomHeight"": 0.0, ""topHeight"": 0.45, ""canStackOnTop"": false, ""footprintCategory"": ""Furniture"" },
    { ""propTypeId"": ""monitor"",    ""widthTiles"": 1, ""depthTiles"": 1, ""bottomHeight"": 0.0, ""topHeight"": 0.40, ""canStackOnTop"": false, ""footprintCategory"": ""DeskAccessory"" },
    { ""propTypeId"": ""computer"",   ""widthTiles"": 1, ""depthTiles"": 1, ""bottomHeight"": 0.0, ""topHeight"": 0.45, ""canStackOnTop"": false, ""footprintCategory"": ""DeskAccessory"" },
    { ""propTypeId"": ""printer"",    ""widthTiles"": 1, ""depthTiles"": 1, ""bottomHeight"": 0.0, ""topHeight"": 0.35, ""canStackOnTop"": true,  ""footprintCategory"": ""DeskAccessory"" }
  ]
}";

    // AT-01: All seven v0.2 starting-palette props are present.
    [Fact]
    public void ParseJson_AllSevenV02PropTypes_HaveEntries()
    {
        var catalog = BuildFootprintCatalog.ParseJson(CatalogJson);

        var required = new[] { "cube-wall", "door", "desk", "chair", "monitor", "computer", "printer" };
        foreach (var id in required)
        {
            var entry = catalog.GetByPropType(id);
            Assert.True(entry is not null, $"Catalog missing entry for '{id}'");
        }
    }

    // AT-01: AllPropTypeIds reports seven entries.
    [Fact]
    public void ParseJson_AllPropTypeIds_HasSevenEntries()
    {
        var catalog = BuildFootprintCatalog.ParseJson(CatalogJson);
        Assert.Equal(7, catalog.AllPropTypeIds.Count);
    }

    // AT-02: widthTiles and depthTiles are all ≥ 1.
    [Fact]
    public void ParseJson_AllEntries_TileDimensionsAtLeastOne()
    {
        var catalog = BuildFootprintCatalog.ParseJson(CatalogJson);
        foreach (var id in catalog.AllPropTypeIds)
        {
            var entry = catalog.GetByPropType(id)!;
            Assert.True(entry.WidthTiles >= 1, $"{id}.widthTiles = {entry.WidthTiles}");
            Assert.True(entry.DepthTiles >= 1, $"{id}.depthTiles = {entry.DepthTiles}");
        }
    }

    // AT-02: heights are all ≥ 0.
    [Fact]
    public void ParseJson_AllEntries_HeightsNonNegative()
    {
        var catalog = BuildFootprintCatalog.ParseJson(CatalogJson);
        foreach (var id in catalog.AllPropTypeIds)
        {
            var entry = catalog.GetByPropType(id)!;
            Assert.True(entry.BottomHeight >= 0f, $"{id}.bottomHeight = {entry.BottomHeight}");
            Assert.True(entry.TopHeight    >= 0f, $"{id}.topHeight = {entry.TopHeight}");
        }
    }

    [Fact]
    public void GetByPropType_UnknownId_ReturnsNull()
    {
        var catalog = BuildFootprintCatalog.ParseJson(CatalogJson);
        Assert.Null(catalog.GetByPropType("vending-machine"));
    }

    [Fact]
    public void GetByPropType_IsCaseInsensitive()
    {
        var catalog = BuildFootprintCatalog.ParseJson(CatalogJson);
        Assert.NotNull(catalog.GetByPropType("DESK"));
        Assert.NotNull(catalog.GetByPropType("Desk"));
    }

    [Fact]
    public void ParseJson_Desk_CorrectValues()
    {
        var catalog = BuildFootprintCatalog.ParseJson(CatalogJson);
        var desk = catalog.GetByPropType("desk")!;

        Assert.Equal(2, desk.WidthTiles);
        Assert.Equal(1, desk.DepthTiles);
        Assert.Equal(0.0f, desk.BottomHeight, precision: 5);
        Assert.Equal(0.75f, desk.TopHeight, precision: 5);
        Assert.True(desk.CanStackOnTop);
        Assert.Equal("Furniture", desk.FootprintCategory);
    }

    [Fact]
    public void ParseJson_EmptyJson_ReturnsEmptyCatalog()
    {
        var catalog = BuildFootprintCatalog.ParseJson(@"{ ""schemaVersion"": ""0.1.0"" }");
        Assert.Empty(catalog.AllPropTypeIds);
    }

    [Fact]
    public void LoadDefault_DoesNotThrow()
    {
        // LoadDefault walks CWD upward; returns empty catalog when file not found.
        // Just ensure it doesn't throw.
        var catalog = BuildFootprintCatalog.LoadDefault();
        Assert.NotNull(catalog);
    }
}
