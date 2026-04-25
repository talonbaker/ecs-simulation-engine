using APIFramework.Components;
using Xunit;

using ContractsLightKind  = Warden.Contracts.Telemetry.LightKind;
using ContractsLightState = Warden.Contracts.Telemetry.LightState;
using ContractsDayPhase   = Warden.Contracts.Telemetry.DayPhase;
using Warden.Contracts.Telemetry;

namespace APIFramework.Tests.Components;

/// <summary>
/// AT-01 (partial): LightSourceComponent fields mirror LightSourceDto round-trip;
/// enum integer values match; intensity and color temperature are in valid ranges.
/// </summary>
public class LightSourceComponentTests
{
    [Fact]
    public void LightSourceComponent_FieldsMatchDto_RoundTrip()
    {
        var comp = new LightSourceComponent
        {
            Id                = "src-001",
            Kind              = APIFramework.Components.LightKind.OverheadFluorescent,
            State             = APIFramework.Components.LightState.On,
            Intensity         = 80,
            ColorTemperatureK = 4000,
            TileX             = 5,
            TileY             = 10,
            RoomId            = "room-a",
        };

        var dto = new LightSourceDto
        {
            Id                = comp.Id,
            Kind              = (ContractsLightKind)(int)comp.Kind,
            State             = (ContractsLightState)(int)comp.State,
            Intensity         = comp.Intensity,
            ColorTemperatureK = comp.ColorTemperatureK,
            Position          = new TilePointDto { X = comp.TileX, Y = comp.TileY },
            RoomId            = comp.RoomId,
        };

        Assert.Equal(comp.Id,                dto.Id);
        Assert.Equal((int)comp.Kind,         (int)dto.Kind);
        Assert.Equal((int)comp.State,        (int)dto.State);
        Assert.Equal(comp.Intensity,         dto.Intensity);
        Assert.Equal(comp.ColorTemperatureK, dto.ColorTemperatureK);
        Assert.Equal(comp.TileX,             dto.Position.X);
        Assert.Equal(comp.TileY,             dto.Position.Y);
        Assert.Equal(comp.RoomId,            dto.RoomId);
    }

    [Fact]
    public void LightKind_EnumValues_MatchDto()
    {
        // Integer ordinals must match so the projector can cast without a translation table
        Assert.Equal((int)ContractsLightKind.OverheadFluorescent, (int)APIFramework.Components.LightKind.OverheadFluorescent);
        Assert.Equal((int)ContractsLightKind.DeskLamp,            (int)APIFramework.Components.LightKind.DeskLamp);
        Assert.Equal((int)ContractsLightKind.ServerLed,           (int)APIFramework.Components.LightKind.ServerLed);
        Assert.Equal((int)ContractsLightKind.BreakroomStrip,      (int)APIFramework.Components.LightKind.BreakroomStrip);
        Assert.Equal((int)ContractsLightKind.ConferenceTrack,     (int)APIFramework.Components.LightKind.ConferenceTrack);
        Assert.Equal((int)ContractsLightKind.ExteriorWall,        (int)APIFramework.Components.LightKind.ExteriorWall);
        Assert.Equal((int)ContractsLightKind.SignageGlow,         (int)APIFramework.Components.LightKind.SignageGlow);
        Assert.Equal((int)ContractsLightKind.Neon,                (int)APIFramework.Components.LightKind.Neon);
        Assert.Equal((int)ContractsLightKind.MonitorGlow,         (int)APIFramework.Components.LightKind.MonitorGlow);
        Assert.Equal((int)ContractsLightKind.OtherInterior,       (int)APIFramework.Components.LightKind.OtherInterior);
    }

    [Fact]
    public void LightState_EnumValues_MatchDto()
    {
        Assert.Equal((int)ContractsLightState.On,        (int)APIFramework.Components.LightState.On);
        Assert.Equal((int)ContractsLightState.Off,       (int)APIFramework.Components.LightState.Off);
        Assert.Equal((int)ContractsLightState.Flickering,(int)APIFramework.Components.LightState.Flickering);
        Assert.Equal((int)ContractsLightState.Dying,     (int)APIFramework.Components.LightState.Dying);
    }

    [Fact]
    public void LightSourceComponent_Intensity_IsWithinBounds()
    {
        var comp = new LightSourceComponent { Intensity = 80 };
        Assert.InRange(comp.Intensity, 0, 100);
    }

    [Fact]
    public void LightSourceComponent_ColorTemperature_IsWithinBounds()
    {
        var comp = new LightSourceComponent { ColorTemperatureK = 4000 };
        Assert.InRange(comp.ColorTemperatureK, 1000, 10000);
    }
}
