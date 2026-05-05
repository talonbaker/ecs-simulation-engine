using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Text.Json;
using APIFramework.Core;
using Warden.Contracts;
using Warden.Telemetry;

namespace ECSCli.Ai;

/// <summary>
/// <c>ai stream --out &lt;path&gt; --interval &lt;gs&gt; [--duration &lt;gs&gt;]</c>
///
/// Runs the simulation and emits one JSON line (JSONL) per <c>--interval</c>
/// game-seconds, flushed immediately after each write. Designed for a Haiku
/// agent to tail while the simulation runs.
///
/// Output is always compact JSON (no pretty-printing) — one object per line.
///
/// EXIT CODES
/// ----------
/// 0  success — stream complete.
/// 1  unexpected error.
/// </summary>
public static class AiStreamCommand
{
    private const float DeltaTime = 1f / 60f;

    /// <summary>
    /// Builds the <c>stream</c> subcommand with its required <c>--out</c>
    /// and <c>--interval</c> options (plus optional <c>--duration</c> and
    /// <c>--world-definition</c>) and wires the handler that runs the
    /// simulation forever or for a fixed window, emitting one JSON line per
    /// interval.
    /// </summary>
    /// <returns>The configured <see cref="Command"/> ready to be added to <see cref="AiCommand.Root"/>.</returns>
    public static Command Build()
    {
        var outOpt = new Option<FileInfo>(
            name: "--out",
            description: "Path to write the JSONL stream to.")
        { IsRequired = true };

        var intervalOpt = new Option<double>(
            name: "--interval",
            description: "Emit a frame every N game-seconds.")
        { IsRequired = true };

        var durationOpt = new Option<double?>(
            name: "--duration",
            description: "Stop after N game-seconds (omit to run until Ctrl+C).",
            getDefaultValue: () => null);

        var worldDefOpt = new Option<FileInfo?>(
            name: "--world-definition",
            description: "Optional path to a world-definition.json. When supplied, the loader spawns the world; otherwise defaults are used.",
            getDefaultValue: () => null);

        var cmd = new Command("stream",
            "Run simulation and emit JSONL telemetry frames at fixed game-second intervals.");
        cmd.AddOption(outOpt);
        cmd.AddOption(intervalOpt);
        cmd.AddOption(durationOpt);
        cmd.AddOption(worldDefOpt);

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var outFile   = ctx.ParseResult.GetValueForOption(outOpt);
            var interval  = ctx.ParseResult.GetValueForOption(intervalOpt);
            var duration  = ctx.ParseResult.GetValueForOption(durationOpt);
            var worldDef  = ctx.ParseResult.GetValueForOption(worldDefOpt);
            try
            {
                Run(outFile!, interval, duration, worldDef?.FullName);
                ctx.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ai stream] ERROR: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });

        return cmd;
    }

    // -- Implementation --------------------------------------------------------

    internal static void Run(FileInfo outFile, double interval, double? duration, string? worldDefinitionPath = null)
    {
        if (interval <= 0)
            throw new ArgumentException("--interval must be > 0.");

        var sim      = new SimulationBootstrapper("SimConfig.json", humanCount: 1, worldDefinitionPath: worldDefinitionPath);
        long tick    = 0;
        double nextSnapshot = interval;
        bool running = true;

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            running  = false;
        };

        var dir = outFile.Directory;
        if (dir != null && !dir.Exists)
            dir.Create();

        using var writer = new StreamWriter(
            outFile.FullName, append: false, System.Text.Encoding.UTF8)
        {
            AutoFlush = true,   // line-flushed as required by the SRD
        };

        while (running)
        {
            sim.Engine.Update(DeltaTime);
            tick++;

            double gameTime = sim.Clock.TotalTime;

            if (gameTime >= nextSnapshot)
            {
                var snap = sim.Capture();
                var dto  = TelemetryProjector.Project(
                    snap,
                    sim.EntityManager,
                    capturedAt:      DateTimeOffset.UtcNow,
                    tick:            tick,
                    seed:            sim.Random.Seed,
                    simVersion:      SimVersion.Full,
                    sunStateService: sim.SunState);

                // One JSON object per line — always compact.
                writer.WriteLine(JsonSerializer.Serialize(dto, JsonOptions.Wire));

                nextSnapshot += interval;
            }

            if (duration.HasValue && gameTime >= duration.Value)
                break;
        }

        Console.WriteLine($"[ai stream] Done. Output: {outFile.FullName}");
    }
}
