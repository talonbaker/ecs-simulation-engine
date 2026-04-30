using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Text;
using System.Text.Json;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Narrative;
using Warden.Contracts;

namespace ECSCli.Ai;

/// <summary>
/// <c>ai narrative-stream --out &lt;path.jsonl&gt; [--interval &lt;gs&gt;] [--duration &lt;gs&gt;] [--seed &lt;n&gt;]</c>
///
/// Runs the simulation and emits one JSON line per <see cref="NarrativeEventCandidate"/>
/// to stdout (or <c>--out</c> file). Designed to be tailed by a Sonnet during
/// development to observe what notable moments the world is producing.
///
/// Pattern matches <see cref="AiStreamCommand"/>. Output is always compact JSON
/// (no pretty-printing) — one candidate object per line, flushed immediately.
///
/// EXIT CODES
/// ──────────
/// 0  success — stream complete.
/// 1  unexpected error.
/// </summary>
public static class AiNarrativeStreamCommand
{
    private const float DeltaTime   = 1f / 60f;
    private const int   HumanCount  = 10;

    /// <summary>
    /// Builds the <c>narrative-stream</c> subcommand with its <c>--out</c>,
    /// <c>--interval</c>, <c>--duration</c>, and <c>--seed</c> options and
    /// wires the handler that runs the simulation while emitting one JSON line
    /// per <see cref="NarrativeEventCandidate"/>.
    /// </summary>
    /// <returns>The configured <see cref="Command"/> ready to be added to <see cref="AiCommand.Root"/>.</returns>
    public static Command Build()
    {
        var outOpt = new Option<FileInfo?>(
            name: "--out",
            description: "Path to write the JSONL candidate stream to. Omit for stdout.",
            getDefaultValue: () => null);

        var intervalOpt = new Option<double>(
            name: "--interval",
            description: "Minimum game-seconds between output flushes. 0 = emit each candidate immediately.",
            getDefaultValue: () => 0.0);

        var durationOpt = new Option<double?>(
            name: "--duration",
            description: "Stop after N game-seconds (omit to run until Ctrl+C).",
            getDefaultValue: () => null);

        var seedOpt = new Option<int>(
            name: "--seed",
            description: "RNG seed for deterministic replay. Default 0.",
            getDefaultValue: () => 0);

        var cmd = new Command("narrative-stream",
            "Run simulation and emit JSONL narrative event candidates.");
        cmd.AddOption(outOpt);
        cmd.AddOption(intervalOpt);
        cmd.AddOption(durationOpt);
        cmd.AddOption(seedOpt);

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var outFile  = ctx.ParseResult.GetValueForOption(outOpt);
            var interval = ctx.ParseResult.GetValueForOption(intervalOpt);
            var duration = ctx.ParseResult.GetValueForOption(durationOpt);
            var seed     = ctx.ParseResult.GetValueForOption(seedOpt);
            try
            {
                Run(outFile, duration, seed);
                ctx.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ai narrative-stream] ERROR: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });

        return cmd;
    }

    // ── Implementation ────────────────────────────────────────────────────────

    internal static void Run(FileInfo? outFile, double? duration, int seed = 0)
    {
        var sim = new SimulationBootstrapper("SimConfig.json", humanCount: HumanCount, seed: seed);

        // Wire social + proximity components onto spawned humans so the narrative
        // detector has drive and willpower state to watch.  SpawnHuman doesn't
        // add these yet (that's the cast-generator's job); we add them here for
        // the narrative-stream command.
        foreach (var e in sim.EntityManager.Query<HumanTag>().ToList())
        {
            EntityTemplates.WithSocial(e);
            EntityTemplates.WithProximity(e);
        }

        TextWriter writer;
        bool ownsWriter;

        if (outFile is null)
        {
            writer     = Console.Out;
            ownsWriter = false;
        }
        else
        {
            var dir = outFile.Directory;
            if (dir != null && !dir.Exists) dir.Create();
            writer     = new StreamWriter(outFile.FullName, append: false, Encoding.UTF8) { AutoFlush = true };
            ownsWriter = true;
        }

        try
        {
            RunCore(sim, writer, duration);
        }
        finally
        {
            if (ownsWriter) writer.Dispose();
        }

        if (outFile is not null)
            Console.WriteLine($"[ai narrative-stream] Done. Output: {outFile.FullName}");
    }

    /// <summary>
    /// Core loop — subscribes to the narrative bus and emits one JSON line per
    /// candidate.  Exposed <c>internal</c> so integration tests can inject a
    /// custom <see cref="SimulationBootstrapper"/> and <see cref="TextWriter"/>.
    /// </summary>
    internal static void RunCore(SimulationBootstrapper sim, TextWriter writer, double? duration)
    {
        bool running = true;

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            running  = false;
        };

        sim.NarrativeBus.OnCandidateEmitted += candidate =>
        {
            var json = JsonSerializer.Serialize(candidate, JsonOptions.Wire);
            writer.WriteLine(json);
        };

        while (running)
        {
            sim.Engine.Update(DeltaTime);

            if (duration.HasValue && sim.Clock.TotalTime >= duration.Value)
                break;
        }
    }
}
