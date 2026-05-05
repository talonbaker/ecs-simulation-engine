using APIFramework.Components;
using Xunit;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;

namespace APIFramework.Tests.Systems;

/// <summary>
/// Unit tests for EnergySystem — awake/sleep energy cycling and state tags.
///
/// EnergySystem drives:
///   - Energy drain (awake) / restore (sleeping)
///   - Sleepiness gain (awake) / drain (sleeping)
///   - SleepingTag  — mirrors IsSleeping flag
///   - TiredTag     — Energy below TiredThreshold
///   - ExhaustedTag — Energy below ExhaustedThreshold (supersedes TiredTag)
///
/// This system takes an EnergySystemConfig in its constructor, which makes
/// the threshold values explicit and injectable in tests — no SimConfig.json
/// loading required.
/// </summary>
public class EnergySystemTests
{
    // -- Standard config used in most tests ------------------------------------

    private static readonly EnergySystemConfig Cfg = new()
    {
        TiredThreshold     = 60f,
        ExhaustedThreshold = 25f,
    };

    private static EnergySystem Sys => new(Cfg);

    // -- Helpers ----------------------------------------------------------------

    private static (EntityManager em, Entity entity) BuildWithEnergy(
        float energy           = 80f,
        float sleepiness       = 20f,
        bool  isSleeping       = false,
        float energyDrain      = 1f,
        float sleepinessGain   = 1f,
        float energyRestore    = 2f,
        float sleepinessDrain  = 1f)
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new EnergyComponent
        {
            Energy            = energy,
            Sleepiness        = sleepiness,
            IsSleeping        = isSleeping,
            EnergyDrainRate   = energyDrain,
            SleepinessGainRate= sleepinessGain,
            EnergyRestoreRate = energyRestore,
            SleepinessDrainRate = sleepinessDrain,
        });
        return (em, entity);
    }

    // -- Awake behaviour --------------------------------------------------------

    [Fact]
    public void Awake_EnergyShouldDrain_By_DrainRate_Times_DeltaTime()
    {
        var (em, entity) = BuildWithEnergy(energy: 80f, energyDrain: 3f);

        Sys.Update(em, deltaTime: 2f);

        // 80 - 3*2 = 74
        Assert.Equal(74f, entity.Get<EnergyComponent>().Energy, precision: 3);
    }

    [Fact]
    public void Awake_SleepinessShouldGrow_By_GainRate_Times_DeltaTime()
    {
        var (em, entity) = BuildWithEnergy(sleepiness: 10f, sleepinessGain: 4f);

        Sys.Update(em, deltaTime: 5f);

        // 10 + 4*5 = 30
        Assert.Equal(30f, entity.Get<EnergyComponent>().Sleepiness, precision: 3);
    }

    [Fact]
    public void Awake_Energy_DoesNotGoBelowZero()
    {
        var (em, entity) = BuildWithEnergy(energy: 1f, energyDrain: 100f);

        Sys.Update(em, deltaTime: 10f);

        Assert.Equal(0f, entity.Get<EnergyComponent>().Energy);
    }

    [Fact]
    public void Awake_Sleepiness_DoesNotExceedHundred()
    {
        var (em, entity) = BuildWithEnergy(sleepiness: 95f, sleepinessGain: 10f);

        Sys.Update(em, deltaTime: 10f);

        Assert.Equal(100f, entity.Get<EnergyComponent>().Sleepiness);
    }

    // -- Sleeping behaviour -----------------------------------------------------

    [Fact]
    public void Sleeping_EnergyShouldRestore_By_RestoreRate_Times_DeltaTime()
    {
        var (em, entity) = BuildWithEnergy(energy: 50f, isSleeping: true, energyRestore: 5f);

        Sys.Update(em, deltaTime: 4f);

        // 50 + 5*4 = 70
        Assert.Equal(70f, entity.Get<EnergyComponent>().Energy, precision: 3);
    }

    [Fact]
    public void Sleeping_SleepinessShouldDrain_By_DrainRate_Times_DeltaTime()
    {
        var (em, entity) = BuildWithEnergy(sleepiness: 60f, isSleeping: true, sleepinessDrain: 3f);

        Sys.Update(em, deltaTime: 6f);

        // 60 - 3*6 = 42
        Assert.Equal(42f, entity.Get<EnergyComponent>().Sleepiness, precision: 3);
    }

    [Fact]
    public void Sleeping_Energy_DoesNotExceedHundred()
    {
        var (em, entity) = BuildWithEnergy(energy: 95f, isSleeping: true, energyRestore: 50f);

        Sys.Update(em, deltaTime: 10f);

        Assert.Equal(100f, entity.Get<EnergyComponent>().Energy);
    }

    [Fact]
    public void Sleeping_Sleepiness_DoesNotGoBelowZero()
    {
        var (em, entity) = BuildWithEnergy(sleepiness: 5f, isSleeping: true, sleepinessDrain: 10f);

        Sys.Update(em, deltaTime: 10f);

        Assert.Equal(0f, entity.Get<EnergyComponent>().Sleepiness);
    }

    // -- SleepingTag mirrors IsSleeping -----------------------------------------

    [Fact]
    public void IsSleeping_True_AddsSleepingTag()
    {
        var (em, entity) = BuildWithEnergy(isSleeping: true);

        Sys.Update(em, deltaTime: 1f);

        Assert.True(entity.Has<SleepingTag>());
    }

    [Fact]
    public void IsSleeping_False_RemovesSleepingTag()
    {
        var (em, entity) = BuildWithEnergy(isSleeping: false);
        entity.Add(new SleepingTag()); // pre-existing tag from last tick

        Sys.Update(em, deltaTime: 1f);

        Assert.False(entity.Has<SleepingTag>());
    }

    // -- TiredTag / ExhaustedTag ------------------------------------------------

    [Fact]
    public void TiredTag_Applied_When_Energy_BelowTiredThreshold()
    {
        // Start just above the threshold, drain past it.
        // TiredThreshold = 60. Energy = 62, drain = 10/sec, dt = 1s → energy = 52 → below 60.
        var (em, entity) = BuildWithEnergy(energy: 62f, energyDrain: 10f);

        Sys.Update(em, deltaTime: 1f);

        Assert.True(entity.Has<TiredTag>());
        Assert.False(entity.Has<ExhaustedTag>());
    }

    [Fact]
    public void TiredTag_Removed_When_Energy_AboveTiredThreshold()
    {
        // Sleeping restores energy above threshold; TiredTag should be cleared.
        var (em, entity) = BuildWithEnergy(energy: 55f, isSleeping: true, energyRestore: 20f);
        entity.Add(new TiredTag()); // pre-existing

        Sys.Update(em, deltaTime: 1f); // 55 + 20 = 75 > 60

        Assert.False(entity.Has<TiredTag>());
    }

    [Fact]
    public void ExhaustedTag_Applied_When_Energy_BelowExhaustedThreshold()
    {
        // ExhaustedThreshold = 25. Start at 30, drain 20/sec, dt = 1s → 10 < 25.
        var (em, entity) = BuildWithEnergy(energy: 30f, energyDrain: 20f);

        Sys.Update(em, deltaTime: 1f);

        Assert.True(entity.Has<ExhaustedTag>());
    }

    [Fact]
    public void ExhaustedTag_Supersedes_TiredTag()
    {
        // When exhausted, TiredTag should NOT also be present — ExhaustedTag takes over.
        var (em, entity) = BuildWithEnergy(energy: 30f, energyDrain: 20f);
        entity.Add(new TiredTag()); // pre-existing tired tag

        Sys.Update(em, deltaTime: 1f); // energy drops to 10 → exhausted

        Assert.True(entity.Has<ExhaustedTag>());
        Assert.False(entity.Has<TiredTag>()); // cleared by the system
    }

    [Fact]
    public void NoTirednessTag_When_Energy_AboveAllThresholds()
    {
        // Energy = 80, well above both thresholds. No tags should be set.
        var (em, entity) = BuildWithEnergy(energy: 80f, energyDrain: 0f);

        Sys.Update(em, deltaTime: 1f);

        Assert.False(entity.Has<TiredTag>());
        Assert.False(entity.Has<ExhaustedTag>());
    }

    // -- Multiple entities ------------------------------------------------------

    [Fact]
    public void TwoEntities_UpdatedIndependently()
    {
        var em = new EntityManager();

        var sleeper = em.CreateEntity();
        sleeper.Add(new EnergyComponent
        {
            Energy = 50f, IsSleeping = true,
            EnergyRestoreRate = 5f, SleepinessDrainRate = 1f
        });

        var awake = em.CreateEntity();
        awake.Add(new EnergyComponent
        {
            Energy = 90f, IsSleeping = false,
            EnergyDrainRate = 2f, SleepinessGainRate = 1f
        });

        Sys.Update(em, deltaTime: 2f);

        Assert.Equal(60f, sleeper.Get<EnergyComponent>().Energy, precision: 3); // 50 + 5*2
        Assert.Equal(86f, awake.Get<EnergyComponent>().Energy, precision: 3);   // 90 - 2*2
        Assert.True(sleeper.Has<SleepingTag>());
        Assert.False(awake.Has<SleepingTag>());
    }
}
