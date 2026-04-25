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
/// <c>ai replay --seed &lt;n&gt; [--commands &lt;path&gt;] --duration &lt;gs&gt; --out &lt;path&gt;</c>
///
/// Runs the simulation deterministically from a seed and emits a JSONL stream.
/// Two runs with identical <c>--seed</c>, <c>SimConfig.json</c>, and
/// <c>--commands</c> produce byte-identical output after stripping
/// <c>capturedAt</c>.
///
/// DETERMINISM CONTRACT
/// ────────────────────
/// • Seed is passed to <see cref="SimulationBootstrapper"/> which wires it
///   into <see cref="SeededRandom"/> and all systems that consume randomness.
/// • Entity IDs are counter-based (not random Guids), so the same creation
///   order always produces the same IDs.
/// • <c>capturedAt</c> is derived from simulation game time so it too is
///   deterministic (no wall-clock dependency), making full runs byte-identical.
/// • <c>HashSet&lt;Entity&gt;</c> query buckets use <c>Entity.GetHashCode()</c>
///   based on Id, so system iteration order is stable across runs.
///
/// EXIT CODES
/// ──────────
/// 0  success — JSONL written.
/// 1  unexpected error.
/// </summary>
public static class AiReplayCommand
{
    private const float DeltaTime = 1f / 60f;

    // Deterministic anchor for capturedAt — avoids wall-clock divergence.
    private static readonly DateTimeOffset Epoch = DateTimeOffset.UnixEpoch;

    public static Command Build()
    {
        var seedOpt = new Option<int>(
            name: "--seed",
            description: "RNG seed for deterministic replay.")
        { IsRequired = true };

        var commandsOpt = new Option<FileInfo?>(
            name: "--commands",
            description: "Optional path to a timestamped command log.",
            getDefaultValue: () => null);

        var durationOpt = new Option<double>(
            name: "--duration",
            description: "Run for N game-seconds and then stop.")
        { IsRequired = true };

        var outOpt = new Option<FileInfo>(
            name: "--out",
            description: "Path to write the JSONL telemetry stream to.")
        { IsRequired = true };

        var worldDefOpt = new Option<FileInfo?>(
            name: "--world-definition",
            description: "Optional path to a world-definition.json. When supplied, the loader spawns the world; otherwise defaults are used.",
            getDefaultValue: () => null);

        var cmd = new Command("replay",
            "Deterministic replay: same seed + config + commands → byte-identical JSONL.");
        cmd.AddOption(seedOpt);
        cmd.AddOption(commandsOpt);
        cmd.AddOption(durationOpt);
        cmd.AddOption(outOpt);
        cmd.AddOption(worldDefOpt);

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var seed     = ctx.ParseResult.GetValueForOption(seedOpt);
            var commands = ctx.ParseResult.GetValueForOption(commandsOpt);
            var duration = ctx.ParseResult.GetValueForOption(durationOpt);
            var outFile  = ctx.ParseResult.GetValueForOption(outOpt);
            var worldDef = ctx.ParseResult.GetValueForOption(worldDefOpt);
            try
            {
                Run(seed, commands, duration, outFile!, worldDef?.FullName);
                ctx.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ai replay] ERROR: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });

        return cmd;
    }

    // ── Implementation ────────────────────────────────────────────────────────

    internal static void Run(int seed, FileInfo? commandsFile, double duration, FileInfo outFile, string? worldDefinitionPath = null)
    {
        if (duration <= 0)
            throw new ArgumentException("--duration must be > 0.");

        // Boot with seed — all randomness (MovementSystem wander, etc.) is seeded.
        var sim  = new SimulationBootstrapper("SimConfig.json", humanCount: 1, seed: seed, worldDefinitionPath: worldDefinitionPath);
        long tick = 0;

        // Snapshot once per game-second — captures all meaningful state transitions
        // and keeps file size in the single-digit-MB range for a 3600-second run.
        const double snapshotInterval = 1.0;
        double nextSnapshot = snapshotInterval;

        var dir = outFile.Directory;
        if (dir != null && !dir.Exists)
            dir.Create();

        using var writer = new StreamWriter(
            outFile.FullName, append: false, System.Text.Encoding.UTF8)
        {
            AutoFlush = true,
        };

        while (sim.Clock.TotalTime < duration)
        {
            sim.Engine.Update(DeltaTime);
            tick++;

            double gameTime = sim.Clock.TotalTime;

            if (gameTime >= nextSnapshot)
            {
                var snap = sim.Capture();

                // Deterministic capturedAt: Epoch + game-seconds elapsed.
                // This removes all wall-clock variance from the output, so the
                // files are byte-identical even without stripping capturedAt.
                var capturedAt = Epoch.AddSeconds(gameTime);

                var dto = TelemetryProjector.Project(
                    snap,
                    sim.EntityManager,
                    capturedAt:  capturedAt,
                    tick:        tick,
                    seed:        seed,
                    simVersion:  SimVersion.Full);

                writer.WriteLine(JsonSerializer.Serialize(dto, JsonOptions.Wire));

                nextSnapshot += snapshotInterval;
            }
        }

        Console.WriteLine(
            $"[ai replay] seed={seed} duration={duration}gs → {outFile.FullName}");
    }
}
