using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Diagnostics;
using System.Diagnostics;

namespace ECSCli;

/// <summary>
/// Renders simulation state and diagnostics to stdout.
/// Two outputs:
///   PrintSnapshot() — live state every N game-minutes during a run
///   PrintReport()   — full metrics + balancing analysis at end of run
/// </summary>
public static class CliRenderer
{
    private const int BarWidth  = 14;
    private const int LineWidth = 66;

    // ═══════════════════════════════════════════════════════════════════════════
    //  LIVE SNAPSHOT
    // ═══════════════════════════════════════════════════════════════════════════

    public static void PrintSnapshot(SimulationBootstrapper sim, long tick, long wallStartTimestamp)
    {
        double wallSeconds = Elapsed(wallStartTimestamp);
        double simSpeed    = wallSeconds > 0.001 ? sim.Clock.TotalTime / wallSeconds : 0;
        string dayNight    = sim.Clock.IsDaytime ? "day" : "night";

        string line = new('─', LineWidth);
        Console.WriteLine();
        Console.WriteLine(line);
        Console.WriteLine($"  {SimVersion.Full}");
        Console.WriteLine(
            $"  TICK {tick,-8:N0}  {sim.Clock.DayTimeDisplay,-22}" +
            $"  [{dayNight}]  WALL {wallSeconds:F1}s  ({simSpeed:F0}x)");
        Console.WriteLine(line);

        var living = sim.EntityManager.Query<MetabolismComponent>().ToList();
        if (living.Count == 0)
        {
            Console.WriteLine("  (no living entities)");
        }
        else
        {
            foreach (var entity in living)
                PrintEntity(entity, sim.Clock);
        }

        var pipeline = sim.EntityManager.Query<EsophagusTransitComponent>().ToList();
        Console.WriteLine();
        Console.WriteLine("  ESOPHAGUS PIPELINE");
        if (pipeline.Count == 0)
        {
            Console.WriteLine("    — nothing in transit —");
        }
        else
        {
            foreach (var e in pipeline)
            {
                var transit = e.Get<EsophagusTransitComponent>();
                string content = e.Has<LiquidComponent>()  ? e.Get<LiquidComponent>().LiquidType
                               : e.Has<BolusComponent>()   ? e.Get<BolusComponent>().FoodType
                               : "Unknown";
                Console.WriteLine($"    {content,-14} {Bar(transit.Progress, 1f)}  {transit.Progress * 100,5:F1}%");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  END-OF-RUN REPORT
    // ═══════════════════════════════════════════════════════════════════════════

    public static void PrintReport(SimulationBootstrapper sim, SimMetrics metrics,
                                   long tick, long wallStartTimestamp)
    {
        double wallSec  = Elapsed(wallStartTimestamp);
        double gameSec  = sim.Clock.TotalTime;
        double gameHours = gameSec / 3600.0;
        int    dayCount  = sim.Clock.DayNumber - 1; // days completed

        string line  = new('═', LineWidth);
        string dash  = new('─', LineWidth);
        var    violations = sim.Invariants.Violations;

        Console.WriteLine();
        Console.WriteLine(line);
        Console.WriteLine($"  BALANCING REPORT  —  {SimVersion.Full}");
        Console.WriteLine($"  Config     : SimConfig.json");
        Console.WriteLine($"  Ran        : {gameHours:F1} game-hours  ({dayCount} full day(s))");
        Console.WriteLine($"  Wall time  : {wallSec:F1}s at {sim.Clock.TimeScale}x");
        Console.WriteLine($"  Ticks      : {tick:N0}");
        Console.WriteLine(line);

        // ── Invariant summary ────────────────────────────────────────────────
        Console.WriteLine();
        if (violations.Count == 0)
        {
            Console.WriteLine("  ✓  INVARIANTS     0 violations — all values stayed in range");
        }
        else
        {
            Console.WriteLine($"  ⚠  INVARIANTS     {violations.Count} violation(s) detected");
            Console.WriteLine();

            // Group by component.property and show the worst (most extreme) examples
            var grouped = violations
                .GroupBy(v => $"{v.Component}.{v.Property}")
                .OrderByDescending(g => g.Count());

            foreach (var group in grouped)
            {
                var worst = group.OrderByDescending(v => Math.Abs(v.ActualValue - v.ClampedTo)).First();
                Console.WriteLine(
                    $"    {group.Key,-40}  ×{group.Count(),4}  " +
                    $"worst: {worst.ActualValue:F4}  (valid [{worst.ValidMin}, {worst.ValidMax}])  " +
                    $"entity: {worst.EntityName}");
            }
        }

        // ── Per-entity stats ─────────────────────────────────────────────────
        foreach (var em in metrics.EntityStats)
        {
            Console.WriteLine();
            Console.WriteLine(dash);
            Console.WriteLine($"  {em.EntityName.ToUpperInvariant()}  —  LIFECYCLE");
            Console.WriteLine(dash);

            // Lifecycle events in chronological order
            if (em.Events.Count == 0)
            {
                Console.WriteLine("    No notable events recorded.");
            }
            else
            {
                foreach (var ev in em.Events.OrderBy(e => e.GameTime))
                    Console.WriteLine($"    {ev.GameTimeDisplay,-22}  {ev.EventName}");
            }

            Console.WriteLine();
            Console.WriteLine("  ACTIVITY COUNTS");
            Console.WriteLine($"    Feed events   : {em.FeedEvents}");
            Console.WriteLine($"    Drink events  : {em.DrinkEvents}");
            Console.WriteLine($"    Sleep cycles  : {em.SleepCycles}");

            Console.WriteLine();
            Console.WriteLine("  RESOURCE RANGES  (min → mean → max)");
            PrintResourceLine("Satiation  ", em.Satiation);
            PrintResourceLine("Hydration  ", em.Hydration);
            PrintResourceLine("Energy     ", em.Energy);
            PrintResourceLine("Sleepiness ", em.Sleepiness);
            PrintResourceLine("Stomach %  ", em.StomachFill);

            // ── Balancing hints ───────────────────────────────────────────────
            Console.WriteLine();
            Console.WriteLine("  BALANCING HINTS");
            PrintHints(em, gameHours);
        }

        Console.WriteLine();
        Console.WriteLine(line);
        Console.WriteLine("  Adjust values in SimConfig.json and re-run (or hot-reload).");
        Console.WriteLine(line);
    }

    // ── Balancing hint engine ─────────────────────────────────────────────────

    private static void PrintHints(EntityMetrics em, double gameHours)
    {
        bool anyHint = false;

        // Resource stuck near zero
        if (em.Satiation.Min < 2f && em.FeedEvents < 1)
            Hint(ref anyHint, "⚠ Satiation hit near-zero but no feed events detected — FeedingSystem may not be triggering.");

        if (em.Hydration.Min < 2f && em.DrinkEvents < 1)
            Hint(ref anyHint, "⚠ Hydration hit near-zero but no drink events detected — DrinkingSystem may not be triggering.");

        // Resource never drops (threshold too aggressive — entity never experiences the need)
        if (em.Satiation.Min > 60f)
            Hint(ref anyHint, "  Satiation never dropped below 60% — hunger threshold may be set too high, or drain rate too low.");

        if (em.Hydration.Min > 60f)
            Hint(ref anyHint, "  Hydration never dropped below 60% — thirst threshold may be set too high, or drain rate too low.");

        // Machine-gun feeding (too many events relative to game hours)
        double feedsPerHour  = gameHours > 0 ? em.FeedEvents  / gameHours : 0;
        double drinksPerHour = gameHours > 0 ? em.DrinkEvents / gameHours : 0;

        if (feedsPerHour > 4)
            Hint(ref anyHint, $"⚠ {feedsPerHour:F1} feed events/game-hour — possible machine-gun feeding. Check hunger threshold or nutrition queue cap.");

        if (drinksPerHour > 6)
            Hint(ref anyHint, $"⚠ {drinksPerHour:F1} drink events/game-hour — possible rapid-gulping. Check thirst threshold or hydration queue cap.");

        // No sleep at all in a long run
        if (gameHours > 20 && em.SleepCycles == 0)
            Hint(ref anyHint, $"⚠ No sleep cycles in {gameHours:F0} game-hours — sleepiness may never reach the brain threshold. Check sleepinessGainRate or sleepMaxScore.");

        // Sleep energy never recovered
        if (em.Energy.Max < 60f)
            Hint(ref anyHint, "⚠ Energy never exceeded 60% — entity may not be sleeping long enough, or energyRestoreRate is too low.");

        if (!anyHint)
            Console.WriteLine("    All patterns look reasonable for this run length.");
    }

    private static void Hint(ref bool anyHint, string msg)
    {
        anyHint = true;
        Console.WriteLine($"    {msg}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void PrintEntity(Entity entity, SimulationClock clock)
    {
        string name = entity.Has<IdentityComponent>()
            ? entity.Get<IdentityComponent>().Name
            : $"Entity {entity.ShortId}";

        Console.WriteLine();
        Console.WriteLine($"  ◈ {name}  [{entity.ShortId}]");

        var tagList = BuildTagList(entity);
        Console.WriteLine($"    Tags      {(tagList.Count > 0 ? string.Join("  ·  ", tagList) : "— none —")}");

        var meta = entity.Get<MetabolismComponent>();
        Console.WriteLine($"    Satiation  {Bar(meta.Satiation, 100f)}  {meta.Satiation,5:F1}%    hunger {meta.Hunger,5:F1}%");
        Console.WriteLine($"    Hydration  {Bar(meta.Hydration, 100f)}  {meta.Hydration,5:F1}%    thirst {meta.Thirst,5:F1}%");

        if (entity.Has<EnergyComponent>())
        {
            var en = entity.Get<EnergyComponent>();
            string sleepState = en.IsSleeping ? "SLEEPING" : "awake";
            Console.WriteLine($"    Energy     {Bar(en.Energy,     100f)}  {en.Energy,5:F1}%    {sleepState}");
            Console.WriteLine($"    Sleepiness {Bar(en.Sleepiness, 100f)}  {en.Sleepiness,5:F1}%    circadian ×{clock.CircadianFactor:F2}");
        }

        if (entity.Has<DriveComponent>())
        {
            var d = entity.Get<DriveComponent>();
            Console.WriteLine($"    Brain      dominant {d.Dominant.ToString().ToUpperInvariant(),-6}" +
                              $"  eat {d.EatUrgency:F2}  drink {d.DrinkUrgency:F2}  sleep {d.SleepUrgency:F2}");
        }

        if (entity.Has<StomachComponent>())
        {
            var s = entity.Get<StomachComponent>();
            Console.WriteLine($"    Stomach    {Bar(s.Fill, 1f)}  {s.Fill * 100,5:F1}%    ({s.CurrentVolumeMl:F0}/{StomachComponent.MaxVolumeMl:F0} ml)");
            Console.WriteLine($"               queued  nutr {s.NutritionQueued:F1}  hydr {s.HydrationQueued:F1}");
        }
    }

    private static void PrintResourceLine(string label, ResourceStats s)
    {
        Console.WriteLine($"    {label}   {s.Min,5:F1}% → {s.Mean,5:F1}% → {s.Max,5:F1}%");
    }

    private static List<string> BuildTagList(Entity entity)
    {
        var tags = new List<string>();
        if (entity.Has<HungerTag>())     tags.Add("HUNGRY");
        if (entity.Has<ThirstTag>())     tags.Add("THIRSTY");
        if (entity.Has<StarvingTag>())   tags.Add("STARVING");
        if (entity.Has<DehydratedTag>()) tags.Add("DEHYDRATED");
        if (entity.Has<TiredTag>())      tags.Add("TIRED");
        if (entity.Has<ExhaustedTag>())  tags.Add("EXHAUSTED");
        if (entity.Has<SleepingTag>())   tags.Add("SLEEPING");
        if (entity.Has<IrritableTag>())  tags.Add("IRRITABLE");
        return tags;
    }

    private static string Bar(float value, float max)
    {
        float ratio  = Math.Clamp(value / max, 0f, 1f);
        int   filled = (int)(ratio * BarWidth);
        int   empty  = BarWidth - filled;
        return "[" + new string('█', filled) + new string('░', empty) + "]";
    }

    private static double Elapsed(long wallStartTimestamp) =>
        (double)(Stopwatch.GetTimestamp() - wallStartTimestamp) / Stopwatch.Frequency;
}
