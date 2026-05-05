using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using Xunit;

namespace APIFramework.Tests.Systems.LifeState;

/// <summary>
/// AT-13: Alive NPC with IsFaintingTag → tag removed after Update.
/// AT-14: Alive NPC with IsFaintingTag + FaintingComponent → both removed.
/// AT-15: Incapacitated NPC with IsFaintingTag (still unconscious) → tag NOT removed.
/// </summary>
public class FaintingCleanupSystemTests
{
    // -- Helpers ---------------------------------------------------------------

    private static Entity BuildFaintingNpc(EntityManager em, LifeState state)
    {
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = state });
        npc.Add(new IsFaintingTag());
        return npc;
    }

    // -- AT-13: Alive NPC → IsFaintingTag removed ------------------------------

    [Fact]
    public void AT13_AliveNpc_WithIsFaintingTag_TagIsRemoved()
    {
        var em  = new EntityManager();
        var npc = BuildFaintingNpc(em, LifeState.Alive);

        new FaintingCleanupSystem().Update(em, 1f);

        Assert.False(npc.Has<IsFaintingTag>());
    }

    // -- AT-14: Alive NPC → both tag and component removed --------------------

    [Fact]
    public void AT14_AliveNpc_WithFaintingComponent_ComponentAlsoRemoved()
    {
        var em  = new EntityManager();
        var npc = BuildFaintingNpc(em, LifeState.Alive);
        npc.Add(new FaintingComponent { FaintStartTick = 1, RecoveryTick = 21 });

        new FaintingCleanupSystem().Update(em, 1f);

        Assert.False(npc.Has<IsFaintingTag>());
        Assert.False(npc.Has<FaintingComponent>());
    }

    // -- AT-15: Incapacitated NPC → tag preserved (still unconscious) ---------

    [Fact]
    public void AT15_IncapacitatedNpc_IsFaintingTag_NotRemoved()
    {
        var em  = new EntityManager();
        var npc = BuildFaintingNpc(em, LifeState.Incapacitated);

        new FaintingCleanupSystem().Update(em, 1f);

        Assert.True(npc.Has<IsFaintingTag>());
    }
}
