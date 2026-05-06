using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using APIFramework.Config;
using APIFramework.Core;
using Warden.Contracts.Telemetry;
using Warden.Telemetry;
using Warden.Telemetry.AsciiMap;

namespace ECSCli.World;

#if WARDEN

/// <summary>
/// <c>world map [--floor N] [--no-legend] [--no-hazards] [--no-furniture] [--no-npcs] [--watch]</c>
///
/// Boots a minimal simulation, captures the initial world snapshot, and renders
/// it as a Unicode box-drawing ASCII floor plan to stdout.
///
/// --watch is accepted for forward-compatibility; in this release it prints a
/// one-shot snapshot (live tick-by-tick tailing is a follow-up packet).
///
/// EXIT CODES
/// ──────────
/// 0  success — map printed to stdout.
/// 1  unexpected error.
/// </summary>
public static class WorldMapCommand
{
    /// <summary>
    /// Constructs the <c>map</c> sub-command with its option set and handler.
    /// Returns a <see cref="Command"/> ready to be added to <see cref="WorldCommand.Root"/>.
    /// </summary>
    public static Command Build()
    {
        var floorOpt = new Option<int>(
            name: "--floor",
            description: "Floor index to render (0 = Basement, 1 = First, 2 = Top).",
            getDefaultValue: () => 0);

        var noLegendOpt   = new Option<bool>("--no-legend",    "Omit the LEGEND section.");
        var noHazardsOpt  = new Option<bool>("--no-hazards",   "Omit hazard glyphs.");
        var noFurnitureOpt= new Option<bool>("--no-furniture",  "Omit furniture glyphs.");
        var noNpcsOpt     = new Option<bool>("--no-npcs",       "Omit NPC glyphs.");
        var watchOpt      = new Option<int?>(
            name: "--watch",
            description: "Re-render every N ticks (omit for one-shot).");

        var cmd = new Command("map", "Render the current world snapshot as an ASCII floor plan.");
        cmd.AddOption(floorOpt);
        cmd.AddOption(noLegendOpt);
        cmd.AddOption(noHazardsOpt);
        cmd.AddOption(noFurnitureOpt);
        cmd.AddOption(noNpcsOpt);
        cmd.AddOption(watchOpt);

        cmd.SetHandler((InvocationContext ctx) =>
        {
            int   floor       = ctx.ParseResult.GetValueForOption(floorOpt);
            bool  noLegend    = ctx.ParseResult.GetValueForOption(noLegendOpt);
            bool  noHazards   = ctx.ParseResult.GetValueForOption(noHazardsOpt);
            bool  noFurniture = ctx.ParseResult.GetValueForOption(noFurnitureOpt);
            bool  noNpcs      = ctx.ParseResult.GetValueForOption(noNpcsOpt);
            int?  watch       = ctx.ParseResult.GetValueForOption(watchOpt);

            var opts = new AsciiMapOptions(
                FloorIndex:    floor,
                IncludeLegend: !noLegend,
                ShowHazards:   !noHazards,
                ShowFurniture: !noFurniture,
                ShowNpcs:      !noNpcs);

            try
            {
                Run(opts, watch);
                ctx.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[world map] ERROR: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });

        return cmd;
    }

    /// <summary>
    /// Executes the verb against a freshly-bootstrapped simulation. When
    /// <paramref name="watchEveryN"/> is non-null, advances the engine and re-renders
    /// every N ticks; otherwise prints a one-shot snapshot and returns.
    /// </summary>
    internal static void Run(AsciiMapOptions opts, int? watchEveryN)
    {
        var sim  = new SimulationBootstrapper(new InMemoryConfigProvider(new SimConfig()), humanCount: 1);
        var snap = sim.Capture();
        var dto  = TelemetryProjector.Project(snap, sim.EntityManager,
            DateTimeOffset.UtcNow, tick: 0, seed: 0, simVersion: "cli");

        Console.WriteLine(AsciiMapProjector.Render(dto, opts));

        // --watch: advance the sim and re-render every N ticks until Ctrl+C
        if (watchEveryN.HasValue && watchEveryN.Value > 0)
        {
            Console.WriteLine("[world map --watch] Press Ctrl+C to stop.");
            int n = watchEveryN.Value;
            while (true)
            {
                for (int i = 0; i < n; i++)
                    sim.Engine.Update(1f / 60f);
                snap = sim.Capture();
                dto  = TelemetryProjector.Project(snap, sim.EntityManager,
                    DateTimeOffset.UtcNow, tick: sim.Engine.Clock.CurrentTick,
                    seed: 0, simVersion: "cli");
                Console.Clear();
                Console.WriteLine(AsciiMapProjector.Render(dto, opts));
            }
        }
    }
}

#endif
