#if WARDEN
using System.Text;

/// <summary>
/// Top-level "scenario" dev-console command — WP-PT.1.
/// Dispatches to registered <see cref="IScenarioSubverb"/> handlers.
///
/// Usage:
///   scenario                           — list all sub-verbs
///   scenario help &lt;subverb&gt;           — detailed help for one sub-verb
///   scenario &lt;subverb&gt; [args…]        — execute a sub-verb
/// </summary>
public sealed class ScenarioCommand : IDevConsoleCommand
{
    private readonly ScenarioSubverbRegistry _registry;

    public string   Name        => "scenario";
    public string   Usage       => "scenario <subverb> [args]  |  scenario  |  scenario help <subverb>";
    public string   Description => "Trigger any simulation scenario event on demand (WARDEN only).";
    public string[] Aliases     => System.Array.Empty<string>();

    public ScenarioCommand()
    {
        _registry = new ScenarioSubverbRegistry();
        _registry.Register(new ChokeSubverb());
        _registry.Register(new SlipSubverb());
        _registry.Register(new FaintSubverb());
        _registry.Register(new LockoutSubverb());
        _registry.Register(new KillSubverb());
        _registry.Register(new RescueSubverb());
        _registry.Register(new ChoreMicrowaveToSubverb());
        _registry.Register(new ThrowSubverb());
        _registry.Register(new SoundSubverb());
        _registry.Register(new SetTimeSubverb());
        _registry.Register(new SeedStainsSubverb());
        _registry.Register(new SeedBereavementSubverb());
    }

    public string Execute(string[] args, DevCommandContext ctx)
    {
        // No args: print full sub-verb list.
        if (args.Length == 0)
            return BuildListHelp();

        // "scenario help <subverb>" — detailed help for one verb.
        if (string.Equals(args[0], "help", System.StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2)
                return BuildListHelp();

            if (!_registry.TryGet(args[1], out var target))
                return $"ERROR: Unknown sub-verb '{args[1]}'. Type 'scenario' to list all.";

            return $"{target.Name}\n  Usage:  {target.Usage}\n  {target.Description}";
        }

        // Dispatch to sub-verb.
        if (!_registry.TryGet(args[0], out var sv))
            return $"ERROR: Unknown scenario sub-verb '{args[0]}'. Type 'scenario' to list all.";

        var subArgs = new string[args.Length - 1];
        System.Array.Copy(args, 1, subArgs, 0, subArgs.Length);
        return sv.Execute(subArgs, ctx);
    }

    /// <summary>Sub-verb registry accessor for registration tests.</summary>
    public ScenarioSubverbRegistry Registry => _registry;

    private string BuildListHelp()
    {
        var sb = new StringBuilder("Scenario sub-verbs:\n");
        foreach (var sv in _registry.All)
            sb.AppendLine($"  {sv.Name,-26} {sv.Description}");
        return sb.ToString().TrimEnd();
    }
}
#endif
