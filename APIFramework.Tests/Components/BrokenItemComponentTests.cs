using APIFramework.Components;
using APIFramework.Core;
using Xunit;

namespace APIFramework.Tests.Components;

public class BrokenItemComponentTests
{
    [Fact]
    public void DefaultStruct_DefaultValues()
    {
        var c = new BrokenItemComponent();
        Assert.Null(c.OriginalKind);
        Assert.Equal(BreakageKind.Smashed, c.Breakage);  // default enum = 0 = Smashed
        Assert.Equal(0L, c.CreatedAtTick);
        Assert.Null(c.ChronicleEntryId);
    }

    [Fact]
    public void Fields_RoundTrip()
    {
        var c = new BrokenItemComponent
        {
            OriginalKind     = "coffee-mug",
            Breakage         = BreakageKind.Dropped,
            CreatedAtTick    = 150L,
            ChronicleEntryId = "entry-xyz",
        };

        Assert.Equal("coffee-mug",   c.OriginalKind);
        Assert.Equal(BreakageKind.Dropped, c.Breakage);
        Assert.Equal(150L,           c.CreatedAtTick);
        Assert.Equal("entry-xyz",    c.ChronicleEntryId);
    }

    [Fact]
    public void BreakageKind_AllValuesAccessible()
    {
        Assert.Equal(0, (int)BreakageKind.Smashed);
        Assert.Equal(1, (int)BreakageKind.Dropped);
        Assert.Equal(2, (int)BreakageKind.ForceImpact);
        Assert.Equal(3, (int)BreakageKind.Unknown);
    }

    [Fact]
    public void EntityTemplates_BrokenItem_AddsRequiredComponents()
    {
        var em     = new EntityManager();
        var entity = EntityTemplates.BrokenItem(em,
            originalKind: "laptop",
            roomId: null,
            x: 4f, z: 9f,
            breakageKind: BreakageKind.ForceImpact,
            chronicleEntryId: "entry-002",
            createdAtTick: 25L);

        Assert.True(entity.Has<BrokenItemTag>());
        Assert.True(entity.Has<PositionComponent>());
        Assert.True(entity.Has<BrokenItemComponent>());

        var pos = entity.Get<PositionComponent>();
        Assert.Equal(4f, pos.X, precision: 5);
        Assert.Equal(9f, pos.Z, precision: 5);

        var bc = entity.Get<BrokenItemComponent>();
        Assert.Equal("laptop",              bc.OriginalKind);
        Assert.Equal(BreakageKind.ForceImpact, bc.Breakage);
        Assert.Equal(25L,                   bc.CreatedAtTick);
        Assert.Equal("entry-002",           bc.ChronicleEntryId);
    }

    [Fact]
    public void EntityTemplates_BrokenItem_UnknownBreakage_Stores()
    {
        var em     = new EntityManager();
        var entity = EntityTemplates.BrokenItem(em, "chair", null, 0f, 0f,
            BreakageKind.Unknown, "e", 1L);

        var bc = entity.Get<BrokenItemComponent>();
        Assert.Equal(BreakageKind.Unknown, bc.Breakage);
    }
}
