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
/// <c>ai snapshot --out &lt;path&gt; [--pretty]</c>
///
/// Boots the simulation, runs one update tick, captures a
/// <c>SimulationSnapshot</c>, projects it to a <c>WorldStateDto</c>, and
/// writes it as JSON to <c>--out</c>.
///
/// EXIT CODES
/// ──────────
/// 0  success — file written.
/// 2  invariant violation count > 0 after the first tick.
/// 1  unexpected error.
/// </summary>
public static class AiSnapshotCommand
{
    private const float DeltaTime = 1f / 60f;

    /// <summary>
    /// Builds the <c>snapshot</c> subcommand with its required <c>--out</c>
    /// option and optional <c>--pretty</c> flag, wiring the handler that boots
    /// the simulation, advances one tick, and writes a <c>WorldStateDto</c>
    /// JSON file.
    /// </summary>
    /// <returns>The configured <see cref="Command"/> ready to be added to <see cref="AiCommand.Root"/>.</returns>
    public static Command Build()
    {
        var outOpt = new Option<FileInfo>(
            name: "--out",
            description: "Path to write the JSON snapshot to.")
        { IsRequired = true };

        var prettyOpt = new Option<bool>(
            name: "--pretty",
            description: "Pretty-print the JSON output (default: compact).");

        var cmd = new Command("snapshot",
            "Boot, run one tick, capture world state, write to JSON.");
        cmd.AddOption(outOpt);
        cmd.AddOption(prettyOpt);

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var outFile = ctx.ParseResult.GetValueForOption(outOpt);
            var pretty  = ctx.ParseResult.GetValueForOption(prettyOpt);
            try
            {
                ctx.ExitCode = Run(outFile!, pretty);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ai snapshot] ERROR: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });

        return cmd;
    }

    // ── Implementation ────────────────────────────────────────────────────────

    internal static int Run(FileInfo outFile, bool pretty)
    {
        var sim = new SimulationBootstrapper("SimConfig.json", humanCount: 1);
        var now = DateTimeOffset.UtcNow;

        sim.Engine.Update(DeltaTime);

        var snap = sim.Capture();
        var dto  = TelemetryProjector.Project(
            snap,
            sim.EntityManager,
            capturedAt:     now,
            tick:           1,
            seed:           sim.Random.Seed,
            simVersion:     SimVersion.Full,
            sunStateService: sim.SunState);

        var opts = pretty ? JsonOptions.Pretty : JsonOptions.Wire;

        var dir = outFile.Directory;
        if (dir != null && !dir.Exists)
            dir.Create();

        File.WriteAllText(outFile.FullName, JsonSerializer.Serialize(dto, opts));
        Console.WriteLine($"[ai snapshot] Written: {outFile.FullName}");

        // Exit 2 if any invariants fired on the very first tick.
        if (snap.ViolationCount > 0)
        {
            Console.Error.WriteLine(
                $"[ai snapshot] WARNING: {snap.ViolationCount} invariant violation(s) detected.");
            return 2;
        }

        return 0;
    }
}
