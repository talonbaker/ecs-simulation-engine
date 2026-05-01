using System.CommandLine;

namespace ECSCli.World;

/// <summary>
/// Root <c>world</c> command. Registers spatial visualisation subcommands.
/// Mirrors the <c>ai</c> command pattern (System.CommandLine subtree).
/// </summary>
public static class WorldCommand
{
    private static readonly Lazy<RootCommand> _root = new(Build);

    /// <summary>The fully-wired <c>System.CommandLine</c> root command.</summary>
    public static RootCommand Root => _root.Value;

    private static RootCommand Build()
    {
        var root = new RootCommand(
            "ECSCli world — spatial visualisation commands for the ECS Simulation Engine");

        root.AddCommand(WorldMapCommand.Build());

        return root;
    }
}
