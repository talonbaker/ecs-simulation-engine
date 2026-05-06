using System.Collections.Generic;
using APIFramework.Systems.Rescue;
using Xunit;

namespace APIFramework.Tests.Systems.Rescue;

/// <summary>
/// AT-11: archetype-rescue-bias.json loads; all cast-bible archetypes are covered.
/// </summary>
public class RescueAcceptanceBiasJsonTests
{
    // All archetypes declared in the cast bible (WP-3.2.4 spec)
    private static readonly IReadOnlyList<string> CastBibleArchetypes = new[]
    {
        "the-newbie",
        "the-old-hand",
        "the-cynic",
        "the-climber",
        "the-recovering",
        "the-vent",
        "the-hermit",
        "the-founders-nephew",
        "the-affair",
        "the-crush",
    };

    private static ArchetypeRescueBiasCatalog LoadCatalog() =>
        ArchetypeRescueBiasCatalog.LoadFromFile();

    [Fact]
    public void CatalogLoads_NotEmpty()
    {
        var catalog = LoadCatalog();
        Assert.NotEmpty(catalog.ArchetypeIds);
    }

    [Theory]
    [InlineData("the-newbie")]
    [InlineData("the-old-hand")]
    [InlineData("the-cynic")]
    [InlineData("the-climber")]
    [InlineData("the-recovering")]
    [InlineData("the-vent")]
    [InlineData("the-hermit")]
    [InlineData("the-founders-nephew")]
    [InlineData("the-affair")]
    [InlineData("the-crush")]
    public void AllCastBibleArchetypes_HaveRescueBias(string archetype)
    {
        var catalog = LoadCatalog();
        float bias = catalog.GetBias(archetype);
        Assert.True(bias > 0f || archetype == "the-founders-nephew",
            $"{archetype} should have a non-zero bias (or be explicitly zero for the-founders-nephew)");
    }

    [Fact]
    public void AllCastBibleArchetypes_ArePresentInCatalog()
    {
        var catalog  = LoadCatalog();
        var present  = new HashSet<string>(catalog.ArchetypeIds, System.StringComparer.OrdinalIgnoreCase);
        var missing  = new List<string>();
        foreach (var a in CastBibleArchetypes)
            if (!present.Contains(a)) missing.Add(a);

        Assert.Empty(missing);
    }

    [Fact]
    public void BiasValues_AreInValidRange()
    {
        var catalog = LoadCatalog();
        foreach (var archetype in catalog.ArchetypeIds)
        {
            float bias = catalog.GetBias(archetype);
            Assert.InRange(bias, 0f, 1f);
        }
    }

    [Fact]
    public void CompetenceValues_AreInValidRange()
    {
        var catalog = LoadCatalog();
        foreach (var archetype in catalog.ArchetypeIds)
        {
            foreach (var kind in new[] { RescueKind.Heimlich, RescueKind.CPR, RescueKind.DoorUnlock })
            {
                float comp = catalog.GetCompetence(archetype, kind);
                Assert.InRange(comp, 0f, 1f);
            }
        }
    }

    [Fact]
    public void FoundersNephew_HasLowestBias()
    {
        var catalog = LoadCatalog();
        float nephewBias = catalog.GetBias("the-founders-nephew");
        foreach (var archetype in CastBibleArchetypes)
        {
            float bias = catalog.GetBias(archetype);
            Assert.True(bias >= nephewBias, $"{archetype} bias {bias} should be >= founder's nephew {nephewBias}");
        }
    }

    [Fact]
    public void Newbie_HasHighBias()
    {
        var catalog = LoadCatalog();
        Assert.True(catalog.GetBias("the-newbie") >= 0.80f,
            "the-newbie should have a high rescue bias (≥0.80)");
    }
}
