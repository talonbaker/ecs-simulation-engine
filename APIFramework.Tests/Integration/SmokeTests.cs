using APIFramework.Components;
using APIFramework.Core;
using Xunit;

namespace APIFramework.Tests.Integration;

/// <summary>
/// Integration smoke tests — run a real headless simulation and assert that the
/// end-to-end system pipeline produces sane output.
///
/// WHAT THESE TESTS ARE (AND AREN'T)
/// ───────────────────────────────────
/// These are NOT exhaustive property tests. They are the "does it not explode
/// over a full day" check — the safety net that would have caught the
/// zero-nutrition starvation bug (v0.7.2) before it ever reached a real run.
///
/// Each test stands up a complete SimulationBootstrapper using compiled defaults
/// (no SimConfig.json required — the bootstrapper falls back gracefully).
/// TimeScale is left at 120 (default) so 1 real second = 120 game seconds.
///
/// TIMING
/// ───────
/// One game day = 86 400 game-seconds.
/// With TimeScale 120 and a 1-second real-time tick, 720 ticks = 1 game day.
/// At the engine's ~276 000 ticks/sec throughput this takes less than 3ms.
///
/// INVARIANT VIOLATIONS
/// ─────────────────────
/// The InvariantSystem clamps impossible values and records them. A small number
/// of violations is expected from floating-point drift as the stomach empties
/// toward zero. Sustained violations (same property every tick) indicate a
/// broken system — those are caught by the violation-count assertion.
/// </summary>
public class SmokeTests
{
    // ── Constants ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Real-time seconds per tick. With TimeScale=120, each tick advances the
    /// game clock by 120 game-seconds (2 game-minutes).
    /// </summary>
    private const float TickDelta = 1f;

    /// <summary>Number of ticks needed to simulate exactly 24 game hours.</summary>
    /// <remarks>86 400 game-seconds / 120 (TimeScale) = 720 real ticks.</remarks>
    private const int TicksPerDay = 720;

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs a full 24-hour simulation using compiled defaults and returns the
    /// bootstrapper so assertions can inspect the final state.
    /// </summary>
    private static SimulationBootstrapper RunOneDay()
    {
        var sim = new SimulationBootstrapper(); // no config path → uses defaults
        for (int i = 0; i < TicksPerDay; i++)
            sim.Engine.Update(TickDelta);
        return sim;
    }

    // ── Core pipeline health ───────────────────────────────────────────────────

    [Fact]
    public void FullDayRun_CompletesWithoutException()
    {
        // The most basic smoke test: the engine must not throw during a full day.
        var ex = Record.Exception(RunOneDay);
        Assert.Null(ex);
    }

    [Fact]
    public void FullDayRun_EntityManager_StillHasHumanEntity()
    {
        // The human entity must survive the day — not be accidentally destroyed.
        var sim    = RunOneDay();
        var humans = sim.EntityManager.Query<IdentityComponent>()
            .Where(e => e.Get<IdentityComponent>().Name == "Human")
            .ToList();

        Assert.Single(humans);
    }

    [Fact]
    public void FullDayRun_Satiation_RemainsWithinValidRange()
    {
        // MetabolismComponent.Satiation must stay in [0, 100] after a full day.
        var sim  = RunOneDay();
        var human = sim.EntityManager.Query<MetabolismComponent>().First();
        var meta  = human.Get<MetabolismComponent>();

        Assert.InRange(meta.Satiation, 0f, 100f);
    }

    [Fact]
    public void FullDayRun_Hydration_RemainsWithinValidRange()
    {
        var sim   = RunOneDay();
        var human = sim.EntityManager.Query<MetabolismComponent>().First();
        var meta  = human.Get<MetabolismComponent>();

        Assert.InRange(meta.Hydration, 0f, 100f);
    }

    [Fact]
    public void FullDayRun_Energy_RemainsWithinValidRange()
    {
        var sim   = RunOneDay();
        var human = sim.EntityManager.Query<EnergyComponent>().First();
        var energy = human.Get<EnergyComponent>();

        Assert.InRange(energy.Energy,     0f, 100f);
        Assert.InRange(energy.Sleepiness, 0f, 100f);
    }

    // ── Nutrition pipeline contract ────────────────────────────────────────────

    /// <summary>
    /// THE KEY REGRESSION TEST.
    ///
    /// This is the exact failure mode of the v0.7.2 zero-nutrition bug:
    ///   - Stomach volume climbed to 1000 ml
    ///   - NutrientStores stayed at exactly zero
    ///   - Satiation / Hydration never changed
    ///
    /// After the IncludeFields = true fix, food actually flows through:
    ///   FeedingSystem → EsophagusSystem → StomachComponent → DigestionSystem → NutrientStores
    ///
    /// Asserting NutrientStores > 0 is the simplest proof the full pipeline ran.
    /// </summary>
    [Fact]
    public void FullDayRun_NutrientStores_Accumulate_CarbohydratesAndWater()
    {
        var sim   = RunOneDay();
        var human = sim.EntityManager.Query<MetabolismComponent>().First();
        var meta  = human.Get<MetabolismComponent>();

        // Carbohydrates must be positive — food went through the pipeline.
        Assert.True(meta.NutrientStores.Carbohydrates > 0f,
            $"Expected accumulated carbohydrates in NutrientStores after 1 game day; " +
            $"got {meta.NutrientStores.Carbohydrates:F2}g. " +
            "If this is 0, check IncludeFields = true in SimConfig.JsonOptions.");

        // Water must be positive — drinking went through the pipeline.
        Assert.True(meta.NutrientStores.Water > 0f,
            $"Expected accumulated water in NutrientStores after 1 game day; " +
            $"got {meta.NutrientStores.Water:F2}ml. " +
            "If this is 0, the DrinkingSystem / DigestionSystem pipeline is broken.");
    }

    [Fact]
    public void FullDayRun_Stomach_NeverStuckat_MaxVolume_WithZeroNutrients()
    {
        // The old bug: stomach fills to exactly MaxVolumeMl (1000ml) and
        // NutrientsQueued stays at zero forever. FeedingSystem sees IsFull=true
        // and never fires again. Detect by checking end state.
        var sim   = RunOneDay();
        var human = sim.EntityManager.Query<StomachComponent>().First();
        var stomach = human.Get<StomachComponent>();

        // If the bug is present: volume = 1000ml, nutrients = 0.
        // Either the stomach empties (volume < 1000ml) or it has real nutrients queued.
        bool stomachFull            = stomach.CurrentVolumeMl >= StomachComponent.MaxVolumeMl;
        bool noNutrientsQueued      = stomach.NutrientsQueued.Calories < 0.01f;
        bool bugPatternDetected     = stomachFull && noNutrientsQueued;

        Assert.False(bugPatternDetected,
            $"Bug pattern detected: stomach is at {stomach.CurrentVolumeMl:F0}ml with " +
            $"{stomach.NutrientsQueued.Calories:F1} kcal queued. " +
            "This is the zero-nutrition stall from v0.7.2. Check IncludeFields = true.");
    }

    // ── Invariant system ───────────────────────────────────────────────────────

    [Fact]
    public void FullDayRun_InvariantViolations_AreFew()
    {
        // A small number of violations is acceptable (floating-point drift as the
        // stomach empties toward exactly zero). A large count means a system is
        // continuously producing out-of-range values.
        //
        // Threshold: 50 violations across 720 ticks / 13 systems / 1 entity.
        // The v0.7.2 healthy run produced 0 violations. This is a loose guard.
        var sim       = RunOneDay();
        int count     = sim.Invariants.Violations.Count;

        Assert.True(count < 50,
            $"Too many invariant violations: {count}. " +
            "Check Invariants.Violations for the specific property that's out of range.");
    }

    [Fact]
    public void FullDayRun_NoSustained_SatiationAtZero()
    {
        // If Satiation hits 0 and STAYS there for the whole second half of the day,
        // feeding is broken. We check mid-day and end-of-day don't both equal 0.
        var sim = new SimulationBootstrapper();

        // Run half a day
        for (int i = 0; i < TicksPerDay / 2; i++)
            sim.Engine.Update(TickDelta);

        float midSatiation = sim.EntityManager.Query<MetabolismComponent>()
            .First().Get<MetabolismComponent>().Satiation;

        // Run the second half
        for (int i = 0; i < TicksPerDay / 2; i++)
            sim.Engine.Update(TickDelta);

        float endSatiation = sim.EntityManager.Query<MetabolismComponent>()
            .First().Get<MetabolismComponent>().Satiation;

        // At least ONE of the two readings should be above 5 (entity fed at some point)
        Assert.True(midSatiation > 5f || endSatiation > 5f,
            $"Satiation was near zero all day (mid={midSatiation:F1}, end={endSatiation:F1}). " +
            "FeedingSystem or DigestionSystem is not providing sustenance.");
    }

    // ── Multi-day stability ────────────────────────────────────────────────────

    [Fact]
    public void ThreeDayRun_CompletesWithoutException()
    {
        // Longer run to catch any accumulation issues (growing lists, etc.)
        // Three days = 2160 ticks. Still sub-millisecond on modern hardware.
        var sim = new SimulationBootstrapper();
        var ex  = Record.Exception(() =>
        {
            for (int i = 0; i < TicksPerDay * 3; i++)
                sim.Engine.Update(TickDelta);
        });
        Assert.Null(ex);
    }

    [Fact]
    public void ThreeDayRun_InvariantViolations_UnderReasonableLimit()
    {
        var sim = new SimulationBootstrapper();
        for (int i = 0; i < TicksPerDay * 3; i++)
            sim.Engine.Update(TickDelta);

        // Three times as many ticks → three times the tolerance.
        Assert.True(sim.Invariants.Violations.Count < 150,
            $"Excessive violations over 3 days: {sim.Invariants.Violations.Count}");
    }
}
