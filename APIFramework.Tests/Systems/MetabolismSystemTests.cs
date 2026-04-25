using APIFramework.Components;
using Xunit;
using APIFramework.Core;
using APIFramework.Systems;

namespace APIFramework.Tests.Systems;

/// <summary>
/// Unit tests for MetabolismSystem — the biological resource drain.
///
/// MetabolismSystem reads MetabolismComponent from every entity that has one,
/// drains Satiation and Hydration over time, and writes the updated component
/// back. Two multipliers can change the drain rate:
///
///   SleepingTag present  → drain *= SleepMetabolismMultiplier (typically 0.10)
///   AngryTag/RagingTag   → drain *= 1.25 (cortisol / stress effect)
///
/// The system is a pure function over ECS data — no file I/O, no randomness,
/// no external state. That makes it ideal for testing: set up components,
/// call Update, assert the new values.
/// </summary>
public class MetabolismSystemTests
{
    private static readonly MetabolismSystem Sys = new();

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Creates an EntityManager with one entity that has a MetabolismComponent.</summary>
    private static (EntityManager em, Entity entity) BuildWithMetab(
        float satiation       = 80f,
        float hydration       = 80f,
        float satiationDrain  = 1f,
        float hydrationDrain  = 1f,
        float sleepMult       = 0.10f)
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new MetabolismComponent
        {
            Satiation                 = satiation,
            Hydration                 = hydration,
            SatiationDrainRate        = satiationDrain,
            HydrationDrainRate        = hydrationDrain,
            SleepMetabolismMultiplier = sleepMult,
        });
        return (em, entity);
    }

    // ── Basic drain ────────────────────────────────────────────────────────────

    [Fact]
    public void AwakeEntity_DrainsSatiation_ByRate_Times_DeltaTime()
    {
        var (em, entity) = BuildWithMetab(satiation: 80f, satiationDrain: 2f);

        Sys.Update(em, deltaTime: 3f);

        // 80 - (2 * 1.0 * 3) = 74
        Assert.Equal(74f, entity.Get<MetabolismComponent>().Satiation, precision: 3);
    }

    [Fact]
    public void AwakeEntity_DrainsHydration_ByRate_Times_DeltaTime()
    {
        var (em, entity) = BuildWithMetab(hydration: 60f, hydrationDrain: 4f);

        Sys.Update(em, deltaTime: 2f);

        // 60 - (4 * 1.0 * 2) = 52
        Assert.Equal(52f, entity.Get<MetabolismComponent>().Hydration, precision: 3);
    }

    [Fact]
    public void Drain_DoesNotGoBelow_Zero_Satiation()
    {
        var (em, entity) = BuildWithMetab(satiation: 1f, satiationDrain: 10f);

        Sys.Update(em, deltaTime: 5f); // would drain 50 from 1

        Assert.Equal(0f, entity.Get<MetabolismComponent>().Satiation);
    }

    [Fact]
    public void Drain_DoesNotGoBelow_Zero_Hydration()
    {
        var (em, entity) = BuildWithMetab(hydration: 0.5f, hydrationDrain: 5f);

        Sys.Update(em, deltaTime: 10f);

        Assert.Equal(0f, entity.Get<MetabolismComponent>().Hydration);
    }

    // ── Sleep multiplier ───────────────────────────────────────────────────────

    [Fact]
    public void SleepingEntity_DrainIsReduced_By_SleepMultiplier()
    {
        var (em, entity) = BuildWithMetab(
            satiation:    80f,
            satiationDrain: 10f,
            sleepMult:    0.10f);

        entity.Add(new SleepingTag());
        Sys.Update(em, deltaTime: 1f);

        // 80 - (10 * 0.10 * 1) = 79
        Assert.Equal(79f, entity.Get<MetabolismComponent>().Satiation, precision: 3);
    }

    [Fact]
    public void NoSleepingTag_DrainIsFullRate()
    {
        var (em, entity) = BuildWithMetab(
            satiation: 80f,
            satiationDrain: 10f,
            sleepMult: 0.10f);

        // No SleepingTag — full rate applies
        Sys.Update(em, deltaTime: 1f);

        // 80 - (10 * 1.0 * 1) = 70
        Assert.Equal(70f, entity.Get<MetabolismComponent>().Satiation, precision: 3);
    }

    // ── Anger multiplier ───────────────────────────────────────────────────────

    [Fact]
    public void AngryTag_IncreaseDrainBy25Percent()
    {
        var (em, entity) = BuildWithMetab(satiation: 80f, satiationDrain: 4f);
        entity.Add(new AngryTag());

        Sys.Update(em, deltaTime: 1f);

        // 80 - (4 * 1.25 * 1) = 75
        Assert.Equal(75f, entity.Get<MetabolismComponent>().Satiation, precision: 3);
    }

    [Fact]
    public void RagingTag_IncreaseDrainBy25Percent()
    {
        var (em, entity) = BuildWithMetab(hydration: 60f, hydrationDrain: 8f);
        entity.Add(new RagingTag());

        Sys.Update(em, deltaTime: 1f);

        // 60 - (8 * 1.25 * 1) = 50
        Assert.Equal(50f, entity.Get<MetabolismComponent>().Hydration, precision: 3);
    }

    [Fact]
    public void SleepingAndAngry_MultipliersStack()
    {
        // Both tags active → totalMult = SleepMult * AngerMult = 0.10 * 1.25 = 0.125
        var (em, entity) = BuildWithMetab(satiation: 80f, satiationDrain: 8f, sleepMult: 0.10f);
        entity.Add(new SleepingTag());
        entity.Add(new AngryTag());

        Sys.Update(em, deltaTime: 1f);

        // 80 - (8 * 0.125 * 1) = 79
        float expected = 80f - (8f * 0.10f * 1.25f * 1f);
        Assert.Equal(expected, entity.Get<MetabolismComponent>().Satiation, precision: 3);
    }

    // ── Multi-entity isolation ─────────────────────────────────────────────────

    [Fact]
    public void TwoEntities_DrainsAreIndependent()
    {
        var em = new EntityManager();

        var fast = em.CreateEntity();
        fast.Add(new MetabolismComponent { Satiation = 90f, SatiationDrainRate = 5f, HydrationDrainRate = 0f });

        var slow = em.CreateEntity();
        slow.Add(new MetabolismComponent { Satiation = 90f, SatiationDrainRate = 1f, HydrationDrainRate = 0f });

        Sys.Update(em, deltaTime: 2f);

        Assert.Equal(80f, fast.Get<MetabolismComponent>().Satiation, precision: 3); // 90 - 5*2
        Assert.Equal(88f, slow.Get<MetabolismComponent>().Satiation, precision: 3); // 90 - 1*2
    }

    [Fact]
    public void EntityWithout_MetabolismComponent_IsIgnored()
    {
        var em = new EntityManager();
        em.CreateEntity(); // bare entity — no components

        // Should complete without throwing.
        var ex = Record.Exception(() => Sys.Update(em, deltaTime: 1f));
        Assert.Null(ex);
    }
}
