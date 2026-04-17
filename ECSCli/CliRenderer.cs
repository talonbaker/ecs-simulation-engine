using APIFramework.Components;
using APIFramework.Core;
using System.Diagnostics;

namespace ECSCli;

/// <summary>
/// Renders the current simulation state as a text snapshot to stdout.
/// Mirrors what the Avalonia GUI shows, but as plain ASCII.
/// No UI dependencies — reads from EntityManager and SimulationClock directly.
/// </summary>
public static class CliRenderer
{
    private const int BarWidth  = 14;
    private const int LineWidth = 62;

    // ─────────────────────────────────────────────────────────────────────────

    public static void PrintSnapshot(SimulationBootstrapper sim, long tick, long wallStartTimestamp)
    {
        double wallSeconds = (double)(Stopwatch.GetTimestamp() - wallStartTimestamp)
                           / Stopwatch.Frequency;

        double simSpeed = wallSeconds > 0.001
            ? sim.Clock.TotalTime / wallSeconds
            : 0;

        // ── Header ──────────────────────────────────────────────────────────
        string line     = new('─', LineWidth);
        string dayNight = sim.Clock.IsDaytime ? "day" : "night";

        Console.WriteLine();
        Console.WriteLine(line);
        Console.WriteLine($"  {SimVersion.Full}");
        Console.WriteLine(
            $"  TICK {tick,-8:N0}  {sim.Clock.DayTimeDisplay,-22}" +
            $"  [{dayNight}]  WALL {wallSeconds:F1}s  ({simSpeed:F0}x)");
        Console.WriteLine(line);

        // ── Living entities ──────────────────────────────────────────────────
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

        // ── Esophagus pipeline ───────────────────────────────────────────────
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

                string content = e.Has<LiquidComponent>()
                    ? e.Get<LiquidComponent>().LiquidType
                    : e.Has<BolusComponent>()
                        ? e.Get<BolusComponent>().FoodType
                        : "Unknown";

                string bar = ProgressBar(transit.Progress, 1f);
                Console.WriteLine($"    {content,-14} {bar}  {transit.Progress * 100,5:F1}%");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static void PrintEntity(Entity entity, SimulationClock clock)
    {
        string name = entity.Has<IdentityComponent>()
            ? entity.Get<IdentityComponent>().Name
            : $"Entity {entity.ShortId}";

        Console.WriteLine();
        Console.WriteLine($"  ◈ {name}  [{entity.ShortId}]");

        // Tags — always shown ("— none —" means healthy)
        var tagList = BuildTagList(entity);
        string tags = tagList.Count > 0
            ? string.Join("  ·  ", tagList)
            : "— none —";
        Console.WriteLine($"    Tags      {tags}");

        // Metabolism resources + derived sensations
        var meta = entity.Get<MetabolismComponent>();
        Console.WriteLine(
            $"    Satiation  {ProgressBar(meta.Satiation, 100f)}  {meta.Satiation,5:F1}%  " +
            $"  hunger {meta.Hunger,5:F1}%");
        Console.WriteLine(
            $"    Hydration  {ProgressBar(meta.Hydration, 100f)}  {meta.Hydration,5:F1}%  " +
            $"  thirst {meta.Thirst,5:F1}%");

        // Energy / Sleep
        if (entity.Has<EnergyComponent>())
        {
            var en = entity.Get<EnergyComponent>();
            string sleepState = en.IsSleeping ? "SLEEPING" : "awake";
            Console.WriteLine(
                $"    Energy     {ProgressBar(en.Energy, 100f)}  {en.Energy,5:F1}%  " +
                $"  {sleepState}");
            Console.WriteLine(
                $"    Sleepiness {ProgressBar(en.Sleepiness, 100f)}  {en.Sleepiness,5:F1}%  " +
                $"  circadian ×{clock.CircadianFactor:F2}");
        }

        // Brain — dominant desire + urgency scores
        if (entity.Has<DriveComponent>())
        {
            var d = entity.Get<DriveComponent>();
            string dominant = d.Dominant.ToString().ToUpperInvariant();
            Console.WriteLine(
                $"    Brain      dominant {dominant,-6}" +
                $"  eat {d.EatUrgency:F2}  drink {d.DrinkUrgency:F2}  sleep {d.SleepUrgency:F2}");
        }

        // Stomach (optional component)
        if (entity.Has<StomachComponent>())
        {
            var s = entity.Get<StomachComponent>();
            Console.WriteLine(
                $"    Stomach    {ProgressBar(s.Fill, 1f)}  {s.Fill * 100,5:F1}%  " +
                $"  ({s.CurrentVolumeMl:F0} / {StomachComponent.MaxVolumeMl:F0} ml)");
            Console.WriteLine(
                $"               queued  nutr {s.NutritionQueued:F1}  " +
                $"hydr {s.HydrationQueued:F1}");
        }
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

    private static string ProgressBar(float value, float max)
    {
        float ratio  = Math.Clamp(value / max, 0f, 1f);
        int   filled = (int)(ratio * BarWidth);
        int   empty  = BarWidth - filled;
        return "[" + new string('█', filled) + new string('░', empty) + "]";
    }
}
