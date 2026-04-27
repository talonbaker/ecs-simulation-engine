using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Bootstrap;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using Xunit;

namespace APIFramework.Tests.Bootstrap;

/// <summary>
/// AT-03 — Given seed S and a 5-NPC cast, all 5 NPCs have distinct IdentityComponent.Name
///         populated from the pool.
/// AT-04 — Same seed reproduces the same name assignments.
/// AT-05 — Different seeds produce different name assignments (verified across 10 seeds).
/// </summary>
public class CastGeneratorNameAssignmentTests
{
    private static readonly CastGeneratorConfig Cfg = new();

    private static ArchetypeCatalog LoadCatalog()
        => ArchetypeCatalog.LoadDefault()
           ?? throw new InvalidOperationException("Could not locate archetypes.json.");

    private static NamePoolDto MakePool() => new()
    {
        SchemaVersion = "0.1.0",
        FirstNames = new[]
        {
            "Donna", "Frank", "Greg", "Karen", "Bob", "Linda", "Steve", "Susan",
            "Mark", "Dave", "Jennifer", "Mike", "Kevin", "Patricia", "Brian",
        }
    };

    private static void SeedSlots(EntityManager em, int count, string archetypeHint = "the-newbie")
    {
        for (int i = 0; i < count; i++)
        {
            var slot = em.CreateEntity();
            slot.Add(new NpcSlotTag());
            slot.Add(new NpcSlotComponent { X = i * 2, Y = 0, ArchetypeHint = archetypeHint });
        }
    }

    private static IReadOnlyList<Entity> SpawnFive(SeededRandom rng, NamePoolDto pool)
    {
        var catalog = LoadCatalog();
        var em      = new EntityManager();
        SeedSlots(em, 5);
        return CastGenerator.SpawnAll(catalog, em, rng, Cfg, pool);
    }

    // ── AT-03: All 5 NPCs have distinct non-empty names from the pool ─────────

    [Fact]
    public void AT03_FiveNpcs_AllHaveIdentityComponent()
    {
        var npcs = SpawnFive(new SeededRandom(42), MakePool());
        Assert.All(npcs, npc => Assert.True(npc.Has<IdentityComponent>(),
            $"NPC {npc.Id} is missing IdentityComponent."));
    }

    [Fact]
    public void AT03_FiveNpcs_AllNamesAreNonEmpty()
    {
        var npcs = SpawnFive(new SeededRandom(42), MakePool());
        Assert.All(npcs, npc =>
        {
            var name = npc.Get<IdentityComponent>().Name;
            Assert.False(string.IsNullOrWhiteSpace(name),
                $"NPC {npc.Id} has a blank name.");
        });
    }

    [Fact]
    public void AT03_FiveNpcs_AllNamesAreDistinct()
    {
        var npcs  = SpawnFive(new SeededRandom(42), MakePool());
        var names = npcs.Select(npc => npc.Get<IdentityComponent>().Name).ToList();
        Assert.Equal(5, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AT03_AllAssignedNames_ComeFromPool()
    {
        var pool  = MakePool();
        var npcs  = SpawnFive(new SeededRandom(42), pool);
        var names = npcs.Select(npc => npc.Get<IdentityComponent>().Name).ToList();
        Assert.All(names, name =>
            Assert.Contains(name, pool.FirstNames, StringComparer.Ordinal));
    }

    // ── AT-04: Same seed → same name assignments ──────────────────────────────

    [Fact]
    public void AT04_SameSeed_ProducesSameNameAssignments()
    {
        var pool   = MakePool();
        var npcs1  = SpawnFive(new SeededRandom(7), pool);
        var npcs2  = SpawnFive(new SeededRandom(7), pool);

        var names1 = npcs1.Select(npc => npc.Get<IdentityComponent>().Name).ToList();
        var names2 = npcs2.Select(npc => npc.Get<IdentityComponent>().Name).ToList();

        Assert.Equal(names1, names2);
    }

    // ── AT-05: Different seeds → different name assignments ───────────────────

    [Fact]
    public void AT05_TenDifferentSeeds_ProduceAtLeastTwoDifferentNameAssignments()
    {
        var pool = MakePool();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int seed = 0; seed < 10; seed++)
        {
            var npcs = SpawnFive(new SeededRandom(seed), pool);
            // Represent the name assignment as an ordered string for comparison.
            var key = string.Join(",",
                npcs.Select(npc => npc.Get<IdentityComponent>().Name));
            seen.Add(key);
        }

        Assert.True(seen.Count > 1,
            "All 10 different seeds produced identical name assignments — " +
            "name selection is not seed-sensitive.");
    }
}
