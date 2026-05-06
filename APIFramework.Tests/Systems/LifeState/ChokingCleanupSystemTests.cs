using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.LifeState;

/// <summary>
/// AT-01: Deceased NPC with IsChokingTag → tag removed after Update.
/// AT-02: Deceased NPC with IsChokingTag + ChokingComponent → both removed.
/// AT-03: Incapacitated NPC with IsChokingTag (still in progress) → tag NOT removed.
/// AT-04: Alive NPC with IsChokingTag (unusual but possible in tests) → tag NOT removed.
/// </summary>
public class ChokingCleanupSystemTests
{
    // -- Helpers ---------------------------------------------------------------

    private static Entity BuildChokingNpc(EntityManager em, LS state)
    {
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = state });
        npc.Add(new IsChokingTag());
        return npc;
    }

    // -- AT-01: Deceased NPC → IsChokingTag removed ---------------------------

    [Fact]
    public void AT01_DeceasedNpc_WithIsChokingTag_TagIsRemoved()
    {
        var em  = new EntityManager();
        var npc = BuildChokingNpc(em, LS.Deceased);

        new ChokingCleanupSystem().Update(em, 1f);

        Assert.False(npc.Has<IsChokingTag>());
    }

    // -- AT-02: Deceased NPC → both tag and component removed -----------------

    [Fact]
    public void AT02_DeceasedNpc_WithChokingComponent_ComponentAlsoRemoved()
    {
        var em  = new EntityManager();
        var npc = BuildChokingNpc(em, LS.Deceased);
        npc.Add(new ChokingComponent { ChokeStartTick = 10, RemainingTicks = 5 });

        new ChokingCleanupSystem().Update(em, 1f);

        Assert.False(npc.Has<IsChokingTag>());
        Assert.False(npc.Has<ChokingComponent>());
    }

    // -- AT-03: Incapacitated NPC → tag preserved (still choking) -------------

    [Fact]
    public void AT03_IncapacitatedNpc_IsChokingTag_NotRemoved()
    {
        var em  = new EntityManager();
        var npc = BuildChokingNpc(em, LS.Incapacitated);

        new ChokingCleanupSystem().Update(em, 1f);

        Assert.True(npc.Has<IsChokingTag>());
    }

    // -- AT-04: Alive NPC → tag preserved -------------------------------------

    [Fact]
    public void AT04_AliveNpc_IsChokingTag_NotRemoved()
    {
        var em  = new EntityManager();
        var npc = BuildChokingNpc(em, LS.Alive);

        new ChokingCleanupSystem().Update(em, 1f);

        Assert.True(npc.Has<IsChokingTag>());
    }
}
