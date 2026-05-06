using System.Text.Json;
using Warden.Contracts;
using Warden.Contracts.Telemetry;
using Xunit;

namespace APIFramework.Tests.Build;

/// <summary>
/// AT-07: BuildFootprintComponent round-trips through WorldStateDto JSON.
/// </summary>
public class BuildFootprintRoundtripTests
{
    private static readonly BuildFootprintDto DeskFootprint = new()
    {
        WidthTiles        = 2,
        DepthTiles        = 1,
        BottomHeight      = 0.0f,
        TopHeight         = 0.75f,
        CanStackOnTop     = true,
        FootprintCategory = "Furniture",
    };

    // AT-07: BuildFootprintDto with all fields survives a serialize→deserialize cycle.
    [Fact]
    public void BuildFootprintDto_AllFields_RoundTripsViaWorldStateDto()
    {
        var dto = BuildMinimalWorldState(DeskFootprint);

        var json  = JsonSerializer.Serialize(dto, JsonOptions.Wire);
        var dto2  = JsonSerializer.Deserialize<WorldStateDto>(json, JsonOptions.Wire)!;
        var json2 = JsonSerializer.Serialize(dto2, JsonOptions.Wire);

        Assert.Equal(json, json2);

        var fp = dto2.Entities[0].BuildFootprint!;
        Assert.Equal(2,          fp.WidthTiles);
        Assert.Equal(1,          fp.DepthTiles);
        Assert.Equal(0.0f,       fp.BottomHeight);
        Assert.Equal(0.75f,      fp.TopHeight);
        Assert.True(             fp.CanStackOnTop);
        Assert.Equal("Furniture",fp.FootprintCategory);
    }

    // AT-07: null BuildFootprint on an NPC entity round-trips as null.
    [Fact]
    public void BuildFootprintDto_Null_RoundTripsAsNull()
    {
        var dto  = BuildMinimalWorldState(null);
        var json = JsonSerializer.Serialize(dto, JsonOptions.Wire);
        var dto2 = JsonSerializer.Deserialize<WorldStateDto>(json, JsonOptions.Wire)!;

        Assert.Null(dto2.Entities[0].BuildFootprint);
    }

    // AT-07: schema version is preserved as 0.5.1 after round-trip.
    [Fact]
    public void WorldStateDto_SchemaVersion_IsV051_AfterRoundTrip()
    {
        var dto  = BuildMinimalWorldState(DeskFootprint);
        var json = JsonSerializer.Serialize(dto, JsonOptions.Wire);
        var dto2 = JsonSerializer.Deserialize<WorldStateDto>(json, JsonOptions.Wire)!;

        Assert.Equal("0.5.1", dto2.SchemaVersion);
    }

    // AT-07: zero-value footprint (all defaults) round-trips correctly.
    [Fact]
    public void BuildFootprintDto_ZeroValues_RoundTripsCorrectly()
    {
        var zeroPrint = new BuildFootprintDto
        {
            WidthTiles        = 1,
            DepthTiles        = 1,
            BottomHeight      = 0f,
            TopHeight         = 0f,
            CanStackOnTop     = false,
            FootprintCategory = string.Empty,
        };

        var dto  = BuildMinimalWorldState(zeroPrint);
        var json = JsonSerializer.Serialize(dto, JsonOptions.Wire);
        var dto2 = JsonSerializer.Deserialize<WorldStateDto>(json, JsonOptions.Wire)!;
        var fp   = dto2.Entities[0].BuildFootprint!;

        Assert.Equal(1,  fp.WidthTiles);
        Assert.Equal(1,  fp.DepthTiles);
        Assert.Equal(0f, fp.BottomHeight);
        Assert.Equal(0f, fp.TopHeight);
        Assert.False(fp.CanStackOnTop);
        Assert.Equal(string.Empty, fp.FootprintCategory);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static WorldStateDto BuildMinimalWorldState(BuildFootprintDto? footprint) =>
        new()
        {
            SchemaVersion = "0.5.1",
            CapturedAt    = DateTimeOffset.UnixEpoch,
            Tick          = 0,
            Clock         = new ClockStateDto { GameTimeDisplay = "08:00", DayNumber = 1, IsDaytime = true },
            Invariants    = new InvariantDigestDto { ViolationCount = 0 },
            Entities      =
            [
                new EntityStateDto
                {
                    Id             = "entity-001",
                    ShortId        = "e001",
                    BuildFootprint = footprint,
                },
            ],
        };
}
