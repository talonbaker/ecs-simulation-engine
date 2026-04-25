using APIFramework.Components;
using Xunit;

using ContractsApertureFacing = Warden.Contracts.Telemetry.ApertureFacing;
using ContractsDayPhase       = Warden.Contracts.Telemetry.DayPhase;
using Warden.Contracts.Telemetry;

namespace APIFramework.Tests.Components;

/// <summary>
/// AT-01 (partial): LightApertureComponent fields mirror LightApertureDto round-trip;
/// enum integer values match; area is within valid range.
/// </summary>
public class LightApertureComponentTests
{
    [Fact]
    public void LightApertureComponent_FieldsMatchDto_RoundTrip()
    {
        var comp = new LightApertureComponent
        {
            Id          = "apt-001",
            TileX       = 3,
            TileY       = 7,
            RoomId      = "room-b",
            Facing      = APIFramework.Components.ApertureFacing.South,
            AreaSqTiles = 4.0,
        };

        var dto = new LightApertureDto
        {
            Id          = comp.Id,
            Position    = new TilePointDto { X = comp.TileX, Y = comp.TileY },
            RoomId      = comp.RoomId,
            Facing      = (ContractsApertureFacing)(int)comp.Facing,
            AreaSqTiles = comp.AreaSqTiles,
        };

        Assert.Equal(comp.Id,          dto.Id);
        Assert.Equal(comp.TileX,       dto.Position.X);
        Assert.Equal(comp.TileY,       dto.Position.Y);
        Assert.Equal(comp.RoomId,      dto.RoomId);
        Assert.Equal((int)comp.Facing, (int)dto.Facing);
        Assert.Equal(comp.AreaSqTiles, dto.AreaSqTiles);
    }

    [Fact]
    public void ApertureFacing_EnumValues_MatchDto()
    {
        Assert.Equal((int)ContractsApertureFacing.North,   (int)APIFramework.Components.ApertureFacing.North);
        Assert.Equal((int)ContractsApertureFacing.East,    (int)APIFramework.Components.ApertureFacing.East);
        Assert.Equal((int)ContractsApertureFacing.South,   (int)APIFramework.Components.ApertureFacing.South);
        Assert.Equal((int)ContractsApertureFacing.West,    (int)APIFramework.Components.ApertureFacing.West);
        Assert.Equal((int)ContractsApertureFacing.Ceiling, (int)APIFramework.Components.ApertureFacing.Ceiling);
    }

    [Fact]
    public void DayPhase_EnumValues_MatchDto()
    {
        Assert.Equal((int)ContractsDayPhase.Night,        (int)APIFramework.Components.DayPhase.Night);
        Assert.Equal((int)ContractsDayPhase.EarlyMorning, (int)APIFramework.Components.DayPhase.EarlyMorning);
        Assert.Equal((int)ContractsDayPhase.MidMorning,   (int)APIFramework.Components.DayPhase.MidMorning);
        Assert.Equal((int)ContractsDayPhase.Afternoon,    (int)APIFramework.Components.DayPhase.Afternoon);
        Assert.Equal((int)ContractsDayPhase.Evening,      (int)APIFramework.Components.DayPhase.Evening);
        Assert.Equal((int)ContractsDayPhase.Dusk,         (int)APIFramework.Components.DayPhase.Dusk);
    }

    [Fact]
    public void LightApertureComponent_Area_IsWithinBounds()
    {
        var comp = new LightApertureComponent { AreaSqTiles = 4.0 };
        Assert.InRange(comp.AreaSqTiles, 0.5, 64.0);
    }
}
