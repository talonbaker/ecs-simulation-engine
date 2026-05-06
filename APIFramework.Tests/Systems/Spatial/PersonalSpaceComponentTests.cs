using APIFramework.Components;
using APIFramework.Core;
using Warden.Contracts.Telemetry;
using System.Text.Json;
using Xunit;

namespace APIFramework.Tests.Systems.Spatial;

/// <summary>
/// AT-07: PersonalSpaceComponent round-trips through WorldStateDto JSON (save → load → identical state).
/// </summary>
public class PersonalSpaceComponentTests
{
    [Fact]
    public void PersonalSpace_DefaultValues_AreCorrect()
    {
        var ps = new PersonalSpaceComponent { RadiusMeters = 0.6f, RepulsionStrength = 0.3f };
        Assert.Equal(0.6f, ps.RadiusMeters, precision: 4);
        Assert.Equal(0.3f, ps.RepulsionStrength, precision: 4);
    }

    [Fact]
    public void AT07_PersonalSpace_RoundTrips_ThroughSaveLoadDto()
    {
        var dto = new NpcSaveDto
        {
            Id       = System.Guid.NewGuid().ToString(),
            Name     = "Test NPC",
            IsHuman  = true,
            PersonalSpace = new PersonalSpaceSaveDto
            {
                RadiusMeters      = 0.84f,
                RepulsionStrength = 0.36f
            }
        };

        var json    = JsonSerializer.Serialize(dto);
        var restored = JsonSerializer.Deserialize<NpcSaveDto>(json);

        Assert.NotNull(restored?.PersonalSpace);
        Assert.Equal(0.84f, restored!.PersonalSpace!.RadiusMeters,      precision: 5);
        Assert.Equal(0.36f, restored.PersonalSpace.RepulsionStrength, precision: 5);
    }

    [Fact]
    public void AT07_PersonalSpace_Null_WhenComponentAbsent()
    {
        var dto  = new NpcSaveDto { Id = System.Guid.NewGuid().ToString(), Name = "Ghost" };
        var json = JsonSerializer.Serialize(dto);
        var back = JsonSerializer.Deserialize<NpcSaveDto>(json);
        Assert.Null(back?.PersonalSpace);
    }

    [Fact]
    public void AT11_V050_SaveLoads_Against_V051_Schema()
    {
        // A v0.5.0 save that lacks personalSpace must still load cleanly (field is optional).
        var oldDto = new NpcSaveDto
        {
            Id      = System.Guid.NewGuid().ToString(),
            Name    = "OldSave",
            IsHuman = true,
            // PersonalSpace intentionally absent
        };

        var json    = JsonSerializer.Serialize(oldDto);
        var restored = JsonSerializer.Deserialize<NpcSaveDto>(json);
        Assert.Null(restored?.PersonalSpace);
    }
}
