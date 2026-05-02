#if WARDEN

/// <summary>
/// Contract for a single scenario sub-verb dispatched by <see cref="ScenarioCommand"/>.
/// </summary>
public interface IScenarioSubverb
{
    /// <summary>Primary sub-verb token (e.g. "choke", "kill"). Always lower-case.</summary>
    string Name { get; }

    /// <summary>Human-readable usage shown by <c>scenario help &lt;subverb&gt;</c>.</summary>
    string Usage { get; }

    /// <summary>One-line description shown by <c>scenario</c> (no args).</summary>
    string Description { get; }

    /// <summary>
    /// Execute the sub-verb.
    /// <paramref name="args"/> excludes both "scenario" and the sub-verb name itself;
    /// args[0] is the first sub-verb argument.
    /// </summary>
    string Execute(string[] args, DevCommandContext ctx);
}

#endif
