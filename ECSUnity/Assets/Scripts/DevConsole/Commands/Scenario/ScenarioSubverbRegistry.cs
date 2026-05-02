#if WARDEN
using System.Collections.Generic;

/// <summary>
/// Maps scenario sub-verb names to their handler instances.
/// Populated by <see cref="ScenarioCommand"/> at construction time.
/// </summary>
public sealed class ScenarioSubverbRegistry
{
    private readonly Dictionary<string, IScenarioSubverb> _map =
        new Dictionary<string, IScenarioSubverb>(System.StringComparer.OrdinalIgnoreCase);

    private readonly List<IScenarioSubverb> _ordered = new List<IScenarioSubverb>();

    /// <summary>All registered sub-verbs in registration order.</summary>
    public IReadOnlyList<IScenarioSubverb> All => _ordered;

    /// <summary>Registers a sub-verb. Its Name must be unique.</summary>
    public void Register(IScenarioSubverb sv)
    {
        _map[sv.Name] = sv;
        _ordered.Add(sv);
    }

    /// <summary>Returns true and sets <paramref name="sv"/> if the name is found.</summary>
    public bool TryGet(string name, out IScenarioSubverb sv) =>
        _map.TryGetValue(name, out sv);
}
#endif
