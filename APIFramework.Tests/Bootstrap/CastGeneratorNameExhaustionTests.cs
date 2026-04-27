using System;
using APIFramework.Bootstrap;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using Xunit;

namespace APIFramework.Tests.Bootstrap;

/// <summary>
/// AT-06 — Requesting more NPCs than pool entries throws InvalidOperationException
///         with a descriptive message containing "exhausted".
/// </summary>
public class CastGeneratorNameExhaustionTests
{
    private static readonly CastGeneratorConfig Cfg = new();

    private static ArchetypeCatalog LoadCatalog()
        => ArchetypeCatalog.LoadDefault()
           ?? throw new InvalidOperationException("Could not locate archetypes.json.");

    [Fact]
    public void AT06_MoreNpcsThanPoolSize_ThrowsInvalidOperationException()
    {
        var tinyPool = new NamePoolDto
        {
            SchemaVersion = "0.1.0",
            FirstNames    = new[] { "Donna", "Frank" }
        };

        var catalog = LoadCatalog();
        var em      = new EntityManager();
        var rng     = new SeededRandom(1);

        for (int i = 0; i < 3; i++)
        {
            var slot = em.CreateEntity();
            slot.Add(new NpcSlotTag());
            slot.Add(new NpcSlotComponent { X = i, Y = 0, ArchetypeHint = "the-newbie" });
        }

        var ex = Assert.Throws<InvalidOperationException>(
            () => CastGenerator.SpawnAll(catalog, em, rng, Cfg, tinyPool));

        Assert.Contains("exhausted", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AT06_ExceptionMessage_MentionsNamePoolJson()
    {
        var tinyPool = new NamePoolDto
        {
            SchemaVersion = "0.1.0",
            FirstNames    = new[] { "Donna" }
        };

        var catalog = LoadCatalog();
        var em      = new EntityManager();
        var rng     = new SeededRandom(1);

        for (int i = 0; i < 2; i++)
        {
            var slot = em.CreateEntity();
            slot.Add(new NpcSlotTag());
            slot.Add(new NpcSlotComponent { X = i, Y = 0, ArchetypeHint = "the-newbie" });
        }

        var ex = Assert.Throws<InvalidOperationException>(
            () => CastGenerator.SpawnAll(catalog, em, rng, Cfg, tinyPool));

        Assert.Contains("name-pool.json", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
