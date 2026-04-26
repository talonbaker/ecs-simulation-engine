using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>AT-02: StressInitializerSystem spawn-time baseline injection.</summary>
public class StressInitializerSystemTests
{
    private static StressInitializerSystem MakeSys(
        IReadOnlyDictionary<string, double>? baselines = null)
    {
        baselines ??= new Dictionary<string, double>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["the-cynic"]   = 20,
            ["the-vent"]    = 50,
            ["the-newbie"]  = 40,
        };
        return new StressInitializerSystem(baselines);
    }

    [Fact]
    public void NpcWithArchetype_GetsStressComponent()
    {
        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-cynic" });

        MakeSys().Update(em, 1f);

        Assert.True(npc.Has<StressComponent>());
    }

    [Fact]
    public void ChronicLevel_MatchesArchetypeBaseline()
    {
        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-vent" });

        MakeSys().Update(em, 1f);

        Assert.Equal(50.0, npc.Get<StressComponent>().ChronicLevel);
    }

    [Fact]
    public void AcuteLevel_StartsAtZero()
    {
        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });

        MakeSys().Update(em, 1f);

        Assert.Equal(0, npc.Get<StressComponent>().AcuteLevel);
    }

    [Fact]
    public void UnknownArchetype_ChronicLevelZero()
    {
        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-unknown" });

        MakeSys().Update(em, 1f);

        Assert.Equal(0.0, npc.Get<StressComponent>().ChronicLevel);
    }

    [Fact]
    public void AlreadyHasStress_Idempotent()
    {
        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-cynic" });
        npc.Add(new StressComponent { AcuteLevel = 42, ChronicLevel = 77.7 });

        MakeSys().Update(em, 1f);

        var s = npc.Get<StressComponent>();
        Assert.Equal(42, s.AcuteLevel);
        Assert.Equal(77.7, s.ChronicLevel);
    }

    [Fact]
    public void NpcWithoutArchetypeComponent_NotAttached()
    {
        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        // No NpcArchetypeComponent

        MakeSys().Update(em, 1f);

        Assert.False(npc.Has<StressComponent>());
    }

    [Fact]
    public void NonNpcEntity_NotAttached()
    {
        var em = new EntityManager();
        var e  = em.CreateEntity();
        e.Add(new NpcArchetypeComponent { ArchetypeId = "the-cynic" });
        // No NpcTag

        MakeSys().Update(em, 1f);

        Assert.False(e.Has<StressComponent>());
    }

    [Fact]
    public void BaselineAbove100_ClampedTo100()
    {
        var baselines = new Dictionary<string, double>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["over-the-top"] = 150.0,
        };
        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "over-the-top" });

        new StressInitializerSystem(baselines).Update(em, 1f);

        Assert.Equal(100.0, npc.Get<StressComponent>().ChronicLevel);
    }

    [Fact]
    public void MultipleNpcs_AllGetComponents()
    {
        var em = new EntityManager();
        for (int i = 0; i < 5; i++)
        {
            var npc = em.CreateEntity();
            npc.Add(new NpcTag());
            npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-cynic" });
        }

        MakeSys().Update(em, 1f);

        foreach (var e in em.Query<NpcTag>())
            Assert.True(e.Has<StressComponent>());
    }
}
