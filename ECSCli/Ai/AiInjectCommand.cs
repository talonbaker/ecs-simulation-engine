using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Text.Json;
using APIFramework.Core;
using Warden.Contracts;
using Warden.Contracts.Handshake;
using Warden.Telemetry;  // CommandDispatcher, DispatchResult

namespace ECSCli.Ai;

/// <summary>
/// <c>ai inject --in &lt;path&gt;</c>
///
/// Loads an <see cref="AiCommandBatch"/> from a JSON file, boots the
/// simulation for one tick (so entity IDs exist), then dispatches the batch
/// via <see cref="CommandDispatcher"/>. Prints the <see cref="DispatchResult"/>
/// to stdout and exits with the appropriate code.
///
/// EXIT CODES
/// ──────────
/// 0  all commands applied successfully.
/// 3  one or more commands were rejected (fail-closed).
/// 1  file not found, JSON parse error, or unexpected exception.
/// </summary>
public static class AiInjectCommand
{
    private const float DeltaTime = 1f / 60f;

    /// <summary>
    /// Builds the <c>inject</c> subcommand with its required <c>--in</c>
    /// option and wires the handler that loads, validates, and applies an
    /// <see cref="AiCommandBatch"/> to a freshly-booted simulation.
    /// </summary>
    /// <returns>The configured <see cref="Command"/> ready to be added to <see cref="AiCommand.Root"/>.</returns>
    public static Command Build()
    {
        var inOpt = new Option<FileInfo>(
            name: "--in",
            description: "Path to the AiCommandBatch JSON file.")
        { IsRequired = true };

        var cmd = new Command("inject",
            "Apply an AiCommandBatch to the simulation. Exit 0 = applied, exit 3 = rejected.");
        cmd.AddOption(inOpt);

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var inFile = ctx.ParseResult.GetValueForOption(inOpt);
            try
            {
                ctx.ExitCode = Run(inFile!);
            }
            catch (FileNotFoundException)
            {
                Console.Error.WriteLine($"[ai inject] ERROR: File not found: {inFile?.FullName}");
                ctx.ExitCode = 1;
            }
            catch (JsonException jex)
            {
                Console.Error.WriteLine($"[ai inject] ERROR: JSON parse failed: {jex.Message}");
                ctx.ExitCode = 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ai inject] ERROR: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });

        return cmd;
    }

    // ── Implementation ────────────────────────────────────────────────────────

    internal static int Run(FileInfo inFile)
    {
        // Deserialise the batch (throws JsonException on bad input — caller catches).
        var json  = File.ReadAllText(inFile.FullName);
        var batch = JsonSerializer.Deserialize<AiCommandBatch>(json, JsonOptions.Wire)
            ?? throw new JsonException("Deserialised AiCommandBatch was null.");

        // Boot sim and run one tick so entity IDs and components are initialised.
        var sim = new SimulationBootstrapper("SimConfig.json", humanCount: 1);
        sim.Engine.Update(DeltaTime);

        // Dispatch.
        var dispatcher = new CommandDispatcher();
        var result     = dispatcher.Apply(sim, batch);

        // Report.
        Console.WriteLine(
            $"[ai inject] Applied: {result.Applied}  Rejected: {result.Rejected}");

        if (result.Rejected > 0)
        {
            Console.Error.WriteLine("[ai inject] Rejection reasons:");
            foreach (var reason in result.Errors)
                Console.Error.WriteLine($"  \u2022 {reason}");
            return 3;
        }

        return 0;
    }
}
