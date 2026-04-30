using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Spatial;

/// <summary>
/// AT-10: StructuralTaggingSystem attaches StructuralTag to the right entities at boot.
/// </summary>
public class StructuralTaggingSystemTests
{
    private static void RunOneTick(EntityManager em)
    {
        var sys = new StructuralTaggingSystem();
        sys.Update(em, 0.016f);
    }

    [Fact]
    public void ObstacleTagEntities_ReceiveStructuralTag()
    {
        var em = new EntityManager();
        var desk = em.CreateEntity();
        desk.Add(default(ObstacleTag));
        desk.Add(new PositionComponent { X = 3f, Z = 4f });

        RunOneTick(em);

        Assert.True(desk.Has<StructuralTag>());
    }

    [Fact]
    public void AnchorObjectEntities_WithPosition_ReceiveStructuralTag()
    {
        var em = new EntityManager();
        var anchor = em.CreateEntity();
        anchor.Add(new AnchorObjectComponent { Id = "sign-01", RoomId = "r1", Description = "Sign" });
        anchor.Add(new PositionComponent { X = 5f, Z = 5f });

        RunOneTick(em);

        Assert.True(anchor.Has<StructuralTag>());
    }

    [Fact]
    public void NpcEntities_DoNotReceiveStructuralTag()
    {
        var em = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(default(NpcTag));
        npc.Add(new PositionComponent { X = 5f, Z = 5f });

        RunOneTick(em);

        Assert.False(npc.Has<StructuralTag>());
    }

    [Fact]
    public void HumanEntities_DoNotReceiveStructuralTag()
    {
        var em = new EntityManager();
        var human = em.CreateEntity();
        human.Add(default(HumanTag));
        human.Add(new PositionComponent { X = 5f, Z = 5f });

        RunOneTick(em);

        Assert.False(human.Has<StructuralTag>());
    }

    [Fact]
    public void AnchorObjectWithNpcTag_DoesNotReceiveStructuralTag()
    {
        var em = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new AnchorObjectComponent { Id = "npc-anchor", RoomId = "r1", Description = "NPC slot" });
        entity.Add(default(NpcTag));
        entity.Add(new PositionComponent { X = 2f, Z = 2f });

        RunOneTick(em);

        Assert.False(entity.Has<StructuralTag>());
    }

    [Fact]
    public void RunsOnce_SubsequentTicks_DoNotReTag()
    {
        var em = new EntityManager();
        var sys = new StructuralTaggingSystem();

        var desk = em.CreateEntity();
        desk.Add(default(ObstacleTag));
        desk.Add(new PositionComponent { X = 1f, Z = 1f });

        sys.Update(em, 0.016f);
        Assert.True(desk.Has<StructuralTag>());

        // Remove the tag manually
        desk.Remove<StructuralTag>();

        // Second tick should NOT re-add because the system idles after first run
        sys.Update(em, 0.016f);
        Assert.False(desk.Has<StructuralTag>());
    }

    [Fact]
    public void BolusEntities_DoNotReceiveStructuralTag()
    {
        var em = new EntityManager();
        var bolus = em.CreateEntity();
        bolus.Add(default(BolusTag));
        bolus.Add(new PositionComponent { X = 2f, Z = 2f });

        RunOneTick(em);

        Assert.False(bolus.Has<StructuralTag>());
    }
}
