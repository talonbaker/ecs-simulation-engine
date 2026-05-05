using System;
using APIFramework.Cast;
using Xunit;

namespace APIFramework.Tests.Cast;

/// <summary>
/// WP-4.0.M.1 — Cast badge generation. Per-tier composition tests + determinism +
/// dept-stamp heuristic.
/// </summary>
public class CastBadgeGeneratorTests
{
    private static readonly CastNameData     Data    = CastNameDataLoader.LoadDefault()!;
    private static readonly CastNameGenerator NameGen = new(Data);
    private static readonly CastBadgeGenerator BadgeGen = new(Data);

    private static CastNameResult ForcedName(CastNameTier tier, int seed = 42)
        => NameGen.Generate(seed, gender: CastGender.Female, forcedTier: tier);

    // ── Per-tier composition (matches JS generateBadge branches) ─────────────────

    [Fact]
    public void Common_HasConditionAndNoteAndAccess_NoStickerOrHighStatus()
    {
        var rng = new Random(1);
        for (int i = 0; i < 50; i++)
        {
            var name  = ForcedName(CastNameTier.Common, seed: 1000 + i);
            var badge = BadgeGen.Generate(rng, name);

            Assert.NotNull(badge.Condition);
            Assert.NotNull(badge.Note);
            Assert.NotNull(badge.Access);
            Assert.Null(badge.Sticker);
            Assert.Null(badge.Clearance);
            Assert.Null(badge.Legacy);
            Assert.Null(badge.Signature);
            Assert.Null(badge.DepartmentStamp);
            // Title is null for common per TitleBuilder rules.
            Assert.Null(badge.Title);
        }
    }

    [Fact]
    public void Uncommon_HasConditionAndStickerAndAccess_NoNote()
    {
        var rng = new Random(2);
        for (int i = 0; i < 50; i++)
        {
            var name  = ForcedName(CastNameTier.Uncommon, seed: 2000 + i);
            var badge = BadgeGen.Generate(rng, name);

            Assert.NotNull(badge.Condition);
            Assert.Null(badge.Note);
            Assert.NotNull(badge.Access);
            Assert.NotNull(badge.Sticker);
            Assert.Null(badge.Clearance);
            Assert.Null(badge.Legacy);
            Assert.Null(badge.Signature);
            Assert.Null(badge.DepartmentStamp);
        }
    }

    [Fact]
    public void Rare_HasConditionAndNoteAndSticker_NoAccess()
    {
        var rng = new Random(3);
        for (int i = 0; i < 50; i++)
        {
            var name  = ForcedName(CastNameTier.Rare, seed: 3000 + i);
            var badge = BadgeGen.Generate(rng, name);

            Assert.NotNull(badge.Condition);
            Assert.NotNull(badge.Note);
            Assert.Null(badge.Access);
            Assert.NotNull(badge.Sticker);
            Assert.Null(badge.Clearance);
            Assert.Null(badge.Legacy);
            Assert.Null(badge.Signature);
            Assert.Null(badge.DepartmentStamp);
        }
    }

    [Fact]
    public void Epic_HasStickerAndDepartmentStamp_NoMundaneCondition()
    {
        var rng = new Random(4);
        for (int i = 0; i < 50; i++)
        {
            var name  = ForcedName(CastNameTier.Epic, seed: 4000 + i);
            var badge = BadgeGen.Generate(rng, name);

            Assert.Null(badge.Condition);
            Assert.Null(badge.Note);
            Assert.Null(badge.Access);
            Assert.NotNull(badge.Sticker);
            Assert.Null(badge.Clearance);
            Assert.Null(badge.Legacy);
            Assert.Null(badge.Signature);
            Assert.NotNull(badge.DepartmentStamp);
        }
    }

    [Fact]
    public void Legendary_HasClearanceAndLegacyAndSignatureAndStamp_NoMundane()
    {
        var rng = new Random(5);
        for (int i = 0; i < 30; i++)
        {
            var name  = ForcedName(CastNameTier.Legendary, seed: 5000 + i);
            var badge = BadgeGen.Generate(rng, name);

            Assert.Null(badge.Condition);
            Assert.Null(badge.Note);
            Assert.Null(badge.Access);
            Assert.Null(badge.Sticker);
            Assert.NotNull(badge.Clearance);
            Assert.NotNull(badge.Legacy);
            Assert.NotNull(badge.Signature);
            Assert.NotNull(badge.DepartmentStamp);
        }
    }

    [Fact]
    public void Mythic_HasSameHighStatusFieldsAsLegendary()
    {
        var rng = new Random(6);
        for (int i = 0; i < 30; i++)
        {
            var name  = ForcedName(CastNameTier.Mythic, seed: 6000 + i);
            var badge = BadgeGen.Generate(rng, name);

            Assert.NotNull(badge.Clearance);
            Assert.NotNull(badge.Legacy);
            Assert.NotNull(badge.Signature);
            Assert.NotNull(badge.DepartmentStamp);
            Assert.Null(badge.Sticker);
        }
    }

    // ── Determinism ──────────────────────────────────────────────────────────────

    [Fact]
    public void SameSeedAndName_ProducesIdenticalBadge()
    {
        var name = ForcedName(CastNameTier.Epic, seed: 9999);
        var a = BadgeGen.Generate(new Random(123), name);
        var b = BadgeGen.Generate(new Random(123), name);
        Assert.Equal(a, b);
    }

    [Fact]
    public void GenerateWithBadge_RoundTripsAcrossSeed()
    {
        var (name1, badge1) = NameGen.GenerateWithBadge(new Random(42), CastGender.Female);
        var (name2, badge2) = NameGen.GenerateWithBadge(new Random(42), CastGender.Female);
        Assert.Equal(name1, name2);
        Assert.Equal(badge1, badge2);
    }

    // ── findDeptStamp heuristic ──────────────────────────────────────────────────

    [Fact]
    public void FindDeptStamp_FindsSnackForTitleContainingSnack()
    {
        var data = Data;
        // We need a title that contains a stamp id. From name-data.json:
        //   stamp ids include "snack", "chaos", "coffee", "synergy", etc.
        // The titleTiers domain.silly includes "Snack" — so the stamp matches via lowercase.
        var stamp = CastBadgeGenerator.FindDeptStamp("Junior Snack Wrangler", data.DepartmentStamps);
        Assert.NotNull(stamp);
        Assert.Equal("snack", stamp!.Id);
    }

    [Fact]
    public void FindDeptStamp_ReturnsNull_WhenTitleHasNoMatch()
    {
        var data = Data;
        var stamp = CastBadgeGenerator.FindDeptStamp("Senior Associate", data.DepartmentStamps);
        Assert.Null(stamp);
    }

    [Fact]
    public void FindDeptStamp_NullOrEmptyTitle_ReturnsNull()
    {
        var data = Data;
        Assert.Null(CastBadgeGenerator.FindDeptStamp(null,  data.DepartmentStamps));
        Assert.Null(CastBadgeGenerator.FindDeptStamp("",    data.DepartmentStamps));
    }

    [Fact]
    public void FindDeptStamp_NullStamps_ReturnsNull()
    {
        Assert.Null(CastBadgeGenerator.FindDeptStamp("anything", null));
    }

    // ── Argument validation ─────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullData_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CastBadgeGenerator(null!));
    }

    [Fact]
    public void Generate_NullRng_Throws()
    {
        var name = ForcedName(CastNameTier.Common);
        Assert.Throws<ArgumentNullException>(() => BadgeGen.Generate(null!, name));
    }

    [Fact]
    public void Generate_NullName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => BadgeGen.Generate(new Random(), null!));
    }
}
