using APIFramework.Components;
using APIFramework.Core;
using Xunit;

namespace APIFramework.Tests.Components;

/// <summary>Round-trip and invariant tests for <see cref="StainComponent"/> and the <see cref="EntityTemplates.Stain"/> factory — default values, field round-trip, magnitude clamping to 100, and the magnitude-based <see cref="ObstacleTag"/> threshold.</summary>
public class StainComponentTests
{
    [Fact]
    public void DefaultStruct_ZeroFields()
    {
        var c = new StainComponent();
        Assert.Null(c.Source);
        Assert.Equal(0, c.Magnitude);
        Assert.Equal(0L, c.CreatedAtTick);
        Assert.Null(c.ChronicleEntryId);
    }

    [Fact]
    public void Fields_RoundTrip()
    {
        var c = new StainComponent
        {
            Source           = "participant:7",
            Magnitude        = 45,
            CreatedAtTick    = 300L,
            ChronicleEntryId = "abc-def-ghi",
        };

        Assert.Equal("participant:7",  c.Source);
        Assert.Equal(45,               c.Magnitude);
        Assert.Equal(300L,             c.CreatedAtTick);
        Assert.Equal("abc-def-ghi",    c.ChronicleEntryId);
    }

    [Fact]
    public void EntityTemplates_Stain_AddsRequiredComponents()
    {
        var em     = new EntityManager();
        var entity = EntityTemplates.Stain(em, null, 3f, 7f,
            source: "participant:1", magnitude: 40,
            chronicleEntryId: "entry-001", createdAtTick: 10L);

        Assert.True(entity.Has<StainTag>());
        Assert.True(entity.Has<PositionComponent>());
        Assert.True(entity.Has<StainComponent>());

        var pos = entity.Get<PositionComponent>();
        Assert.Equal(3f, pos.X, precision: 5);
        Assert.Equal(7f, pos.Z, precision: 5);

        var sc = entity.Get<StainComponent>();
        Assert.Equal("participant:1", sc.Source);
        Assert.Equal(40,              sc.Magnitude);
        Assert.Equal(10L,             sc.CreatedAtTick);
        Assert.Equal("entry-001",     sc.ChronicleEntryId);
    }

    [Fact]
    public void EntityTemplates_Stain_MagnitudeClampedTo100()
    {
        var em     = new EntityManager();
        var entity = EntityTemplates.Stain(em, null, 0f, 0f,
            source: "x", magnitude: 150, chronicleEntryId: "e", createdAtTick: 1L);

        var sc = entity.Get<StainComponent>();
        Assert.Equal(100, sc.Magnitude);
    }

    [Fact]
    public void EntityTemplates_Stain_LowMagnitude_NoObstacleTag()
    {
        var em     = new EntityManager();
        var entity = EntityTemplates.Stain(em, null, 0f, 0f,
            source: "x", magnitude: 40,  // < 50 → no ObstacleTag
            chronicleEntryId: "e", createdAtTick: 1L);

        Assert.False(entity.Has<ObstacleTag>());
    }

    [Fact]
    public void EntityTemplates_Stain_HighMagnitude_GetsObstacleTag()
    {
        var em     = new EntityManager();
        var entity = EntityTemplates.Stain(em, null, 0f, 0f,
            source: "x", magnitude: 60,  // ≥ 50 → ObstacleTag
            chronicleEntryId: "e", createdAtTick: 1L);

        Assert.True(entity.Has<ObstacleTag>());
    }
}
