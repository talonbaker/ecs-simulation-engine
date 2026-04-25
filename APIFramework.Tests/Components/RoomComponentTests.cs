using APIFramework.Components;
using Warden.Contracts.Telemetry;
using Xunit;

namespace APIFramework.Tests.Components;

/// <summary>AT-01: RoomComponent field-mirror and BoundsRect sanity.</summary>
public class RoomComponentTests
{
    [Fact]
    public void RoomComponent_FieldMirror_RoundTripEquality()
    {
        // Arrange: engine-side component
        var rc = new RoomComponent
        {
            Id           = "00000000-0000-0000-0000-000000000001",
            Name         = "breakroom",
            Category     = APIFramework.Components.RoomCategory.Breakroom,
            Floor        = APIFramework.Components.BuildingFloor.First,
            Bounds       = new BoundsRect(10, 20, 30, 40),
            Illumination = new RoomIllumination(75, 4000, null),
        };

        // Act: project to DTO (as the future projector will do)
        var dto = new RoomDto
        {
            Id           = rc.Id,
            Name         = rc.Name,
            Category     = (Warden.Contracts.Telemetry.RoomCategory)(int)rc.Category,
            Floor        = (Warden.Contracts.Telemetry.BuildingFloor)(int)rc.Floor,
            BoundsRect   = new BoundsRectDto { X = rc.Bounds.X, Y = rc.Bounds.Y, Width = rc.Bounds.Width, Height = rc.Bounds.Height },
            Illumination = new IlluminationDto { AmbientLevel = rc.Illumination.AmbientLevel, ColorTemperatureK = rc.Illumination.ColorTemperatureK, DominantSourceId = rc.Illumination.DominantSourceId },
        };

        // Act: round-trip back to component
        var rc2 = new RoomComponent
        {
            Id           = dto.Id,
            Name         = dto.Name,
            Category     = (APIFramework.Components.RoomCategory)(int)dto.Category,
            Floor        = (APIFramework.Components.BuildingFloor)(int)dto.Floor,
            Bounds       = new BoundsRect(dto.BoundsRect.X, dto.BoundsRect.Y, dto.BoundsRect.Width, dto.BoundsRect.Height),
            Illumination = new RoomIllumination(dto.Illumination.AmbientLevel, dto.Illumination.ColorTemperatureK, dto.Illumination.DominantSourceId),
        };

        // Assert: fields survive the round-trip unchanged
        Assert.Equal(rc.Id,                          rc2.Id);
        Assert.Equal(rc.Name,                        rc2.Name);
        Assert.Equal(rc.Category,                    rc2.Category);
        Assert.Equal(rc.Floor,                       rc2.Floor);
        Assert.Equal(rc.Bounds,                      rc2.Bounds);
        Assert.Equal(rc.Illumination.AmbientLevel,   rc2.Illumination.AmbientLevel);
        Assert.Equal(rc.Illumination.ColorTemperatureK, rc2.Illumination.ColorTemperatureK);
        Assert.Null(rc2.Illumination.DominantSourceId);
    }

    [Fact]
    public void RoomCategory_EnumValues_MatchContractsEnum()
    {
        // The integer values of both enums must be identical so a direct cast works.
        Assert.Equal((int)Warden.Contracts.Telemetry.RoomCategory.Breakroom,       (int)APIFramework.Components.RoomCategory.Breakroom);
        Assert.Equal((int)Warden.Contracts.Telemetry.RoomCategory.ItCloset,        (int)APIFramework.Components.RoomCategory.ItCloset);
        Assert.Equal((int)Warden.Contracts.Telemetry.RoomCategory.Outdoor,         (int)APIFramework.Components.RoomCategory.Outdoor);
    }

    [Fact]
    public void BuildingFloor_EnumValues_MatchContractsEnum()
    {
        Assert.Equal((int)Warden.Contracts.Telemetry.BuildingFloor.Basement,  (int)APIFramework.Components.BuildingFloor.Basement);
        Assert.Equal((int)Warden.Contracts.Telemetry.BuildingFloor.Exterior,  (int)APIFramework.Components.BuildingFloor.Exterior);
    }

    [Fact]
    public void BoundsRect_Contains_InclusiveMinExclusiveMax()
    {
        var b = new BoundsRect(5, 10, 20, 30); // covers X:5..24, Y:10..39

        Assert.True(b.Contains(5,  10));  // min corner
        Assert.True(b.Contains(24, 39));  // max corner (exclusive - 1)
        Assert.False(b.Contains(4,  10)); // left outside
        Assert.False(b.Contains(25, 10)); // right outside
        Assert.False(b.Contains(5,   9)); // top outside
        Assert.False(b.Contains(5,  40)); // bottom outside
    }

    [Fact]
    public void BoundsRect_Area_EqualsWidthTimesHeight()
    {
        var b = new BoundsRect(0, 0, 10, 5);
        Assert.Equal(50, b.Area);
    }
}
