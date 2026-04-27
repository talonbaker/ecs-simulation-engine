using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Components;

/// <summary>AT-01: SocialMaskComponent defaults and MaskInitializerSystem baseline derivation.</summary>
public class SocialMaskComponentTests
{
    [Fact]
    public void AT01_DefaultStruct_AllFieldsZero()
    {
        var mask = new SocialMaskComponent();

        Assert.Equal(0, mask.IrritationMask);
        Assert.Equal(0, mask.AffectionMask);
        Assert.Equal(0, mask.AttractionMask);
        Assert.Equal(0, mask.LonelinessMask);
        Assert.Equal(0, mask.CurrentLoad);
        Assert.Equal(0, mask.Baseline);
        Assert.Equal(0L, mask.LastSlipTick);
    }

    [Fact]
    public void AT01_Initializer_NoPersonality_Baseline30()
    {
        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());

        new MaskInitializerSystem().Update(em, 1f);

        Assert.True(npc.Has<SocialMaskComponent>());
        Assert.Equal(30, npc.Get<SocialMaskComponent>().Baseline);
    }

    [Theory]
    [InlineData( 2,  0, 50)]   // C=2, E=0  → 20 + 0 + 30 = 50
    [InlineData( 0,  2, 20)]   // C=0, E=2  → 0 - 10 + 30 = 20
    [InlineData( 2,  2, 40)]   // C=2, E=2  → 20 - 10 + 30 = 40
    [InlineData(-2,  2,  0)]   // C=-2, E=2 → -20 - 10 + 30 = 0 (clamped)
    [InlineData( 2, -2, 60)]   // C=2, E=-2 → 20 + 10 + 30 = 60
    [InlineData( 0,  0, 30)]   // neutral   → 30
    public void AT01_Initializer_Baseline_DerivedFromPersonality(
        int conscientiousness, int extraversion, int expected)
    {
        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, conscientiousness, extraversion, 0, 0));

        new MaskInitializerSystem().Update(em, 1f);

        Assert.Equal(expected, npc.Get<SocialMaskComponent>().Baseline);
    }

    [Fact]
    public void AT01_Initializer_Idempotent_DoesNotOverwriteExistingComponent()
    {
        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new SocialMaskComponent { IrritationMask = 77, Baseline = 42 });

        new MaskInitializerSystem().Update(em, 1f);

        var mask = npc.Get<SocialMaskComponent>();
        Assert.Equal(77, mask.IrritationMask);
        Assert.Equal(42, mask.Baseline);
    }

    [Fact]
    public void AT01_NonNpc_GetsNoComponent()
    {
        var em = new EntityManager();
        em.CreateEntity(); // no NpcTag

        new MaskInitializerSystem().Update(em, 1f);

        Assert.Empty(em.Query<SocialMaskComponent>());
    }
}
