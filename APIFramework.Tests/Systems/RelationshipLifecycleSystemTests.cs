using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// Tests for RelationshipLifecycleSystem: intensity decay, transition table load,
/// no-transition-without-trigger, and canonical pair preservation across ticks.
/// </summary>
public class RelationshipLifecycleSystemTests
{
    private static SocialSystemConfig Cfg(double decayPerTick = 1.0) => new()
    {
        RelationshipIntensityDecayPerTick = decayPerTick
    };

    private static IReadOnlyList<(RelationshipPattern, RelationshipPattern)> StarterTable() =>
        new List<(RelationshipPattern, RelationshipPattern)>
        {
            (RelationshipPattern.Rival,               RelationshipPattern.AlliesOfConvenience),
            (RelationshipPattern.AlliesOfConvenience,  RelationshipPattern.Friend),
            (RelationshipPattern.Friend,              RelationshipPattern.ActiveAffair),
            (RelationshipPattern.OldFlame,            RelationshipPattern.ActiveAffair),
            (RelationshipPattern.ActiveAffair,        RelationshipPattern.SleptWithSpouse),
        };

    private static Entity BuildRelationship(
        EntityManager em, int a, int b,
        IReadOnlyList<RelationshipPattern>? patterns = null,
        int intensity = 80)
    {
        var e = em.CreateEntity();
        e.Add(new RelationshipTag());
        e.Add(new RelationshipComponent(a, b, patterns, intensity));
        return e;
    }

    // AT-07: intensity decays without interaction signals
    [Fact]
    public void Intensity_DecaysOverTicks()
    {
        var em = new EntityManager();
        var e  = BuildRelationship(em, 1, 2, intensity: 80);

        var sys = new RelationshipLifecycleSystem(Cfg(decayPerTick: 1.0), StarterTable());
        for (int i = 0; i < 20; i++)
            sys.Update(em, 1f);

        int final = e.Get<RelationshipComponent>().Intensity;
        Assert.Equal(60, final);
    }

    [Fact]
    public void Intensity_DecaysToZeroNotBelow()
    {
        var em = new EntityManager();
        var e  = BuildRelationship(em, 1, 2, intensity: 5);

        var sys = new RelationshipLifecycleSystem(Cfg(1.0), StarterTable());
        for (int i = 0; i < 100; i++)
            sys.Update(em, 1f);

        Assert.Equal(0, e.Get<RelationshipComponent>().Intensity);
    }

    // AT-08: table contains ≥ 5 entries; no transition fires in 1000 ticks
    [Fact]
    public void TransitionTable_ContainsAtLeast5Entries()
    {
        // Load the real table via the file-based factory
        var sys = RelationshipLifecycleSystem.LoadFromFile(
            new SocialSystemConfig(),
            "APIFramework/Data/RelationshipTransitionTable.json");

        Assert.True(sys.TransitionCount >= 5,
            $"Transition table should have ≥ 5 entries, has {sys.TransitionCount}");
    }

    [Fact]
    public void NoTransition_FiresDuring1000Ticks()
    {
        var em = new EntityManager();
        var e  = BuildRelationship(em, 1, 2,
            patterns: new List<RelationshipPattern> { RelationshipPattern.Rival });

        // Use decayPerTick=0 so only transition code runs
        var sys = new RelationshipLifecycleSystem(
            Cfg(decayPerTick: 0.0), StarterTable());

        for (int i = 0; i < 1000; i++)
            sys.Update(em, 1f);

        // Pattern must still be Rival — no trigger conditions exist yet
        var patterns = e.Get<RelationshipComponent>().Patterns;
        Assert.Single(patterns);
        Assert.Equal(RelationshipPattern.Rival, patterns[0]);
    }

    // Canonical pair preserved across ticks
    [Fact]
    public void CanonicalPair_PreservedAcrossTicks()
    {
        var em = new EntityManager();
        var e  = BuildRelationship(em, 7, 3, intensity: 50);

        var sys = new RelationshipLifecycleSystem(Cfg(0.0), StarterTable());
        for (int i = 0; i < 10; i++)
            sys.Update(em, 1f);

        var rel = e.Get<RelationshipComponent>();
        Assert.Equal(3, rel.ParticipantA);
        Assert.Equal(7, rel.ParticipantB);
    }

    [Fact]
    public void NonRelationshipEntity_IsSkipped()
    {
        var em = new EntityManager();
        var e  = em.CreateEntity();
        e.Add(new NpcTag());  // NpcTag, not RelationshipTag

        var sys = new RelationshipLifecycleSystem(Cfg(1.0), StarterTable());
        var ex  = Record.Exception(() => sys.Update(em, 1f));
        Assert.Null(ex);
    }

    [Fact]
    public void ZeroDecay_IntensityUnchanged()
    {
        var em = new EntityManager();
        var e  = BuildRelationship(em, 1, 2, intensity: 75);

        var sys = new RelationshipLifecycleSystem(Cfg(0.0), StarterTable());
        for (int i = 0; i < 100; i++)
            sys.Update(em, 1f);

        Assert.Equal(75, e.Get<RelationshipComponent>().Intensity);
    }
}
