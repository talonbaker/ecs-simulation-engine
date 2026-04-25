using System.CommandLine;

namespace ECSCli.Ai;

/// <summary>
/// Root <c>ai</c> command. Registers all five AI-facing subcommands and
/// exposes a static <see cref="Root"/> property so <c>Program.cs</c> can
/// delegate to it with a single call.
///
/// DESIGN NOTE
/// ───────────
/// <c>System.CommandLine</c> is used ONLY for the <c>ai</c> verb subtree.
/// Top-level ECSCli flags continue to use the existing bespoke parser so that
/// running <c>ECSCli</c> with no arguments is byte-identical to pre-WP-04
/// behaviour.
/// </summary>
public static class AiCommand
{
    private static readonly Lazy<RootCommand> _root = new(Build);

    /// <summary>The fully-wired <c>System.CommandLine</c> root command.</summary>
    public static RootCommand Root => _root.Value;

    private static RootCommand Build()
    {
        var root = new RootCommand(
            "ECSCli ai — structured AI agent interface for the ECS Simulation Engine");

        root.AddCommand(AiDescribeCommand.Build());
        root.AddCommand(AiSnapshotCommand.Build());
        root.AddCommand(AiStreamCommand.Build());
        root.AddCommand(AiNarrativeStreamCommand.Build());
        root.AddCommand(AiInjectCommand.Build());
        root.AddCommand(AiReplayCommand.Build());

        return root;
    }
}
