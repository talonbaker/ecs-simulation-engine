using APIFramework.Components;
using APIFramework.Core;
using System.Diagnostics;

namespace ECSCli;

/// <summary>
/// Renders the current simulation state as a text snapshot to stdout.
/// Mirrors what the Avalonia GUI shows, but as plain ASCII.
/// No UI dependencies — reads from EntityManager directly.
/// </summary>
public static class CliRenderer
{
    private const int BarWidth  = 14;
    private const int LineWidth = 60;

    // ─────────────────────────────────────────────────────────────────────────

    public static void PrintSnapshot(SimulationBootstrapper sim, long tick, long wallStartTimestamp)
    {
        double wallSeconds = (double)(Stopwatch.GetTimestamp() - wallStartTimestamp)
                           / Stopwatch.Frequency;

        double simSpeed = wallSeconds > 0.001
            ? sim.Clock.TotalTime / wallSeconds
            : 0;

        var simTime = TimeSpan.FromSeconds(sim.Clock.TotalTime);

        // ── Header ──────────────────────────────────────────────────────────
        string line = new('─', LineWidth);
        Console.WriteLine();
        Console.WriteLine(line);
        Console.WriteLine(
            $"  TICK {tick,-8:N0}  SIM {simTime:hh\\:mm\\:ss}  " +
            $"WALL {wallSeconds:F2}s  SPEED {simSpeed:F0}x");
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
                PrintEntity(entity);
        }

        // ── Esophagus pipeline ───────────────────────────────────────────────
        var pipeline = sim.EntityManager.Query<EsophagusTransitComponent>().ToList();

        if (pipeline.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  ESOPHAGUS PIPELINE");
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
        else
        {
            Console.WriteLine();
            Console.WriteLine("  ESOPHAGUS PIPELINE  — nothing in transit —");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static void PrintEntity(Entity entity)
    {
        string name = entity.Has<IdentityComponent>()
            ? entity.Get<IdentityComponent>().Name
            : $"Entity {entity.ShortId}";

        Console.WriteLine();
        Console.WriteLine($"  ◈ {name}  [{entity.ShortId}]");

        // Tags — only print if any are active
        var tags = BuildTagList(entity);
        if (tags.Count > 0)
            Console.WriteLine($"    Tags      {string.Join("  ·  ", tags)}");

        // Metabolism resources + derived sensations
        var meta = entity.Get<MetabolismComponent>();

        Console.WriteLine(
            $"    Satiation  {ProgressBar(meta.Satiation, 100f)}  {meta.Satiation,5:F1}%  " +
            $"  hunger {meta.Hunger,5:F1}%");

        Console.WriteLine(
            $"    Hydration  {ProgressBar(meta.Hydration, 100f)}  {meta.Hydration,5:F1}%  " +
            $"  thirst {meta.Thirst,5:F1}%");

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
        if (entity.Has<IrritableTag>())  tags.Add("IRRITABLE");
        return tags;
    }

    /// <summary>Renders a filled/empty block progress bar.</summary>
    private static string ProgressBar(float value, float max)
    {
        float ratio  = Math.Clamp(value / max, 0f, 1f);
        int   filled = (int)(ratio * BarWidth);
        int   empty  = BarWidth - filled;
        return "[" + new string('█', filled) + new string('░', empty) + "]";
    }
}
