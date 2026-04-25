using APIFramework.Components;
using Xunit;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;

namespace APIFramework.Tests.Systems;

/// <summary>
/// Unit tests for BrainSystem — the drive priority queue — and DriveComponent's
/// Dominant property.
///
/// BrainSystem is the arbiter of all action. It reads physiological state, scores
/// three drives (Eat / Drink / Sleep), applies mood modifiers, and stamps the
/// winning drive onto DriveComponent.Dominant. Action systems (FeedingSystem,
/// DrinkingSystem, SleepSystem) must check Dominant before acting.
///
/// Testing strategy:
///   • Test DriveComponent.Dominant logic in isolation — it's a pure readonly property.
///   • Test BrainSystem.Update with a fresh SimulationClock (starts at 6 AM,
///     CircadianFactor = 0.10). This makes SleepUrgency predictable in tests.
/// </summary>
public class BrainSystemTests
{
    // ── Shared configuration ───────────────────────────────────────────────────

    private static readonly BrainSystemConfig Cfg = new()
    {
        EatMaxScore          = 1.0f,
        DrinkMaxScore        = 1.0f,
        SleepMaxScore        = 0.9f,
        BoredUrgencyBonus    = 0.04f,
        MinUrgencyThreshold  = 0.05f,
        SadnessUrgencyMult   = 0.80f,
        GriefUrgencyMult     = 0.50f,
    };

    // SimulationClock starts at dawn (6:00 AM) → CircadianFactor = 0.10
    private static BrainSystem MakeSys() => new(Cfg, new SimulationClock());

    private static (EntityManager em, Entity entity) Build(
        float satiation    = 80f,  // Hunger = 20
        float hydration    = 80f,  // Thirst = 20
        float sleepiness   = 20f)
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new MetabolismComponent { Satiation = satiation, Hydration = hydration });
        entity.Add(new EnergyComponent     { Sleepiness = sleepiness });
        entity.Add(new DriveComponent());
        return (em, entity);
    }

    // ── DriveComponent.Dominant ────────────────────────────────────────────────

    [Fact]
    public void Dominant_IsNone_WhenAllUrgenciesAtZero()
    {
        var drives = new DriveComponent { EatUrgency = 0f, DrinkUrgency = 0f, SleepUrgency = 0f };
        Assert.Equal(DesireType.None, drives.Dominant);
    }

    [Fact]
    public void Dominant_IsEat_WhenEatHighest()
    {
        var drives = new DriveComponent { EatUrgency = 0.8f, DrinkUrgency = 0.4f, SleepUrgency = 0.3f };
        Assert.Equal(DesireType.Eat, drives.Dominant);
    }

    [Fact]
    public void Dominant_IsDrink_WhenDrinkHighest()
    {
        var drives = new DriveComponent { EatUrgency = 0.3f, DrinkUrgency = 0.9f, SleepUrgency = 0.2f };
        Assert.Equal(DesireType.Drink, drives.Dominant);
    }

    [Fact]
    public void Dominant_IsSleep_WhenSleepHighest()
    {
        var drives = new DriveComponent { EatUrgency = 0.1f, DrinkUrgency = 0.1f, SleepUrgency = 0.7f };
        Assert.Equal(DesireType.Sleep, drives.Dominant);
    }

    [Fact]
    public void Dominant_IsNone_WhenBelowEpsilon()
    {
        // Just below the 0.001 guard — should return None.
        var drives = new DriveComponent { EatUrgency = 0.0005f };
        Assert.Equal(DesireType.None, drives.Dominant);
    }

    // ── BrainSystem: base score calculation ────────────────────────────────────

    [Fact]
    public void EatUrgency_ProportionalTo_Hunger()
    {
        // Hunger = 100 - Satiation. Satiation=0 → Hunger=100 → EatUrgency = 1.0 * 1.0 = 1.0
        var (em, entity) = Build(satiation: 0f);
        MakeSys().Update(em, deltaTime: 1f);

        Assert.Equal(1.0f, entity.Get<DriveComponent>().EatUrgency, precision: 3);
    }

    [Fact]
    public void DrinkUrgency_ProportionalTo_Thirst()
    {
        // Satiation=100 (Hunger=0), Hydration=0 (Thirst=100) → DrinkUrgency = 1.0
        var (em, entity) = Build(satiation: 100f, hydration: 0f);
        MakeSys().Update(em, deltaTime: 1f);

        Assert.Equal(1.0f, entity.Get<DriveComponent>().DrinkUrgency, precision: 3);
    }

    [Fact]
    public void SleepUrgency_ScaledBy_CircadianFactor()
    {
        // Sleepiness=100, CircadianFactor=0.10 (morning start), SleepMaxScore=0.9
        // SleepUrgency = (100/100) * 0.9 * 0.10 = 0.09
        var (em, entity) = Build(sleepiness: 100f, satiation: 100f, hydration: 100f);
        MakeSys().Update(em, deltaTime: 1f);

        Assert.Equal(0.09f, entity.Get<DriveComponent>().SleepUrgency, precision: 3);
    }

    [Fact]
    public void SleepUrgency_IsZero_Without_EnergyComponent()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new MetabolismComponent { Satiation = 0f, Hydration = 100f });
        // No EnergyComponent

        MakeSys().Update(em, deltaTime: 1f);

        Assert.Equal(0f, entity.Get<DriveComponent>().SleepUrgency);
    }

    // ── Dominant drive selection ───────────────────────────────────────────────

    [Fact]
    public void HungryEntity_DominantIsEat()
    {
        // Satiation=0 (Hunger=100), well hydrated (Thirst=0)
        var (em, entity) = Build(satiation: 0f, hydration: 100f, sleepiness: 10f);
        MakeSys().Update(em, deltaTime: 1f);

        Assert.Equal(DesireType.Eat, entity.Get<DriveComponent>().Dominant);
    }

    [Fact]
    public void ThirstyEntity_DominantIsDrink()
    {
        // Well fed, very thirsty
        var (em, entity) = Build(satiation: 100f, hydration: 0f, sleepiness: 10f);
        MakeSys().Update(em, deltaTime: 1f);

        Assert.Equal(DesireType.Drink, entity.Get<DriveComponent>().Dominant);
    }

    [Fact]
    public void ContentEntity_DominantIsNone()
    {
        // All needs met — all urgencies below MinUrgencyThreshold
        var (em, entity) = Build(satiation: 100f, hydration: 100f, sleepiness: 0f);
        MakeSys().Update(em, deltaTime: 1f);

        Assert.Equal(DesireType.None, entity.Get<DriveComponent>().Dominant);
    }

    // ── MinUrgencyThreshold floor ──────────────────────────────────────────────

    [Fact]
    public void AllUrgencies_ZeroedOut_WhenBelowMinThreshold()
    {
        // Satiation=98 → Hunger=2 → EatUrgency=0.02 < 0.05 MinThreshold
        // Hydration=98 → Thirst=2 → DrinkUrgency=0.02 < 0.05
        // Sleepiness=2  → SleepUrgency≈0.002 < 0.05
        // All below — should all be zeroed.
        var (em, entity) = Build(satiation: 98f, hydration: 98f, sleepiness: 2f);
        MakeSys().Update(em, deltaTime: 1f);

        var drives = entity.Get<DriveComponent>();
        Assert.Equal(0f, drives.EatUrgency);
        Assert.Equal(0f, drives.DrinkUrgency);
        Assert.Equal(0f, drives.SleepUrgency);
    }

    // ── Mood modifiers ─────────────────────────────────────────────────────────

    [Fact]
    public void BoredTag_IncreasesAllUrgencies()
    {
        var (em, entity) = Build(satiation: 50f, hydration: 50f, sleepiness: 20f);
        entity.Add(new BoredTag());
        MakeSys().Update(em, deltaTime: 1f);

        var drives = entity.Get<DriveComponent>();
        // EatUrgency base = 0.50. With BoredBonus = 0.04 → 0.54
        Assert.True(drives.EatUrgency   > 0.50f, $"EatUrgency should exceed base; got {drives.EatUrgency}");
        Assert.True(drives.DrinkUrgency > 0.50f, $"DrinkUrgency should exceed base; got {drives.DrinkUrgency}");
    }

    [Fact]
    public void SadTag_ReducesAllUrgencies()
    {
        var (em, entity) = Build(satiation: 0f, hydration: 100f); // strong hunger
        entity.Add(new SadTag());
        MakeSys().Update(em, deltaTime: 1f);

        // EatUrgency without sad = 1.0; with SadnessMult=0.80 → 0.80
        var drives = entity.Get<DriveComponent>();
        Assert.Equal(0.80f, drives.EatUrgency, precision: 3);
    }

    [Fact]
    public void GriefTag_StronglyReducesAllUrgencies()
    {
        var (em, entity) = Build(satiation: 0f, hydration: 100f); // strong hunger
        entity.Add(new GriefTag());
        MakeSys().Update(em, deltaTime: 1f);

        // EatUrgency without grief = 1.0; with GriefMult=0.50 → 0.50
        var drives = entity.Get<DriveComponent>();
        Assert.Equal(0.50f, drives.EatUrgency, precision: 3);
    }

    [Fact]
    public void GriefTag_TakesPrecedence_OverSadTag()
    {
        // Both tags present — GriefTag should win (it's the stronger modifier).
        var (em, entity) = Build(satiation: 0f, hydration: 100f);
        entity.Add(new GriefTag());
        entity.Add(new SadTag());
        MakeSys().Update(em, deltaTime: 1f);

        // GriefMult=0.50, not 0.80 (Sad)
        var drives = entity.Get<DriveComponent>();
        Assert.Equal(0.50f, drives.EatUrgency, precision: 3);
    }

    // ── Entity without DriveComponent ──────────────────────────────────────────

    [Fact]
    public void Entity_WithoutDriveComponent_GetsOneCreated()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new MetabolismComponent { Satiation = 0f, Hydration = 100f });
        // No DriveComponent added manually

        MakeSys().Update(em, deltaTime: 1f);

        Assert.True(entity.Has<DriveComponent>());
        Assert.Equal(DesireType.Eat, entity.Get<DriveComponent>().Dominant);
    }
}
