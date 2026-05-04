using System;
using System.IO;
using APIFramework.Build;
using Xunit;

namespace APIFramework.Tests.Build;

/// <summary>
/// Tests for <see cref="AuthorModePaletteLoader"/>.
/// </summary>
public class AuthorModePaletteLoaderTests
{
    [Fact]
    public void DefaultPalette_LoadsCleanly()
    {
        var path = AuthorModePaletteLoader.FindDefault();
        Assert.NotNull(path);
        var data = AuthorModePaletteLoader.Load(path!);

        Assert.Equal("0.1.0", data.SchemaVersion);
        Assert.NotEmpty(data.Rooms);
        Assert.NotEmpty(data.LightSources);
        Assert.NotEmpty(data.LightApertures);
    }

    [Fact]
    public void DefaultPalette_RoomEntriesHaveAllFields()
    {
        var data = AuthorModePaletteLoader.LoadDefault();
        Assert.NotNull(data);
        Assert.All(data!.Rooms, room =>
        {
            Assert.False(string.IsNullOrWhiteSpace(room.Label));
            Assert.False(string.IsNullOrWhiteSpace(room.RoomKind));
            Assert.False(string.IsNullOrWhiteSpace(room.Tooltip));
        });
    }

    [Fact]
    public void DefaultPalette_LightSourceEntriesHaveValidRanges()
    {
        var data = AuthorModePaletteLoader.LoadDefault();
        Assert.NotNull(data);
        Assert.All(data!.LightSources, ls =>
        {
            Assert.InRange(ls.DefaultIntensity, 0, 100);
            Assert.InRange(ls.DefaultTempK,     1000, 10000);
            Assert.False(string.IsNullOrWhiteSpace(ls.Label));
            Assert.False(string.IsNullOrWhiteSpace(ls.Kind));
        });
    }

    [Fact]
    public void DefaultPalette_ApertureEntriesHaveValidArea()
    {
        var data = AuthorModePaletteLoader.LoadDefault();
        Assert.NotNull(data);
        Assert.All(data!.LightApertures, ap =>
        {
            Assert.InRange(ap.AreaSqTiles, 0.5, 64.0);
            Assert.False(string.IsNullOrWhiteSpace(ap.Label));
        });
    }

    [Fact]
    public void Load_MissingFile_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => AuthorModePaletteLoader.Load("does/not/exist.json"));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void Load_MissingRoomsBlock_FailsClosed()
    {
        var json = """
        {
          "schemaVersion": "0.1.0",
          "rooms": [],
          "lightSources": [{"label":"L","kind":"deskLamp","defaultIntensity":50,"defaultTempK":4000,"defaultState":"on"}],
          "lightApertures": [{"label":"W","areaSqTiles":3.0}]
        }
        """;
        var tmp = Path.Combine(Path.GetTempPath(), $"palette-{Guid.NewGuid():N}.json");
        File.WriteAllText(tmp, json);
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => AuthorModePaletteLoader.Load(tmp));
            Assert.Contains("rooms", ex.Message);
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
