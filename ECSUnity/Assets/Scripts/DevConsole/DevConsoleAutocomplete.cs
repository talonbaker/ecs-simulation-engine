#if WARDEN
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;

/// <summary>
/// Tab-autocomplete engine for the developer console — WP-3.1.H AT-15.
///
/// TAB BEHAVIOUR
/// ─────────────
/// First Tab press: collect candidates, show first.
/// Subsequent Tab presses: cycle through candidates.
/// Any other key resets the cycle.
///
/// CANDIDATE SOURCES
/// ──────────────────
/// When input is empty or contains only the first token (no space yet):
///   → command names from the dispatcher.
/// When input has a space (arg mode) and the command is known to take an NPC arg:
///   → NPC display names from EngineHost.WorldState.Entities.
/// When arg mode and command takes a component name:
///   → hardcoded list of common component short-names.
/// </summary>
public sealed class DevConsoleAutocomplete
{
    private readonly DevConsoleCommandDispatcher _dispatcher;
    private List<string>  _candidates   = new List<string>();
    private int           _cycleIndex   = -1;
    private string        _lastInput    = null;

    // Commands whose first arg is an NPC identifier.
    private static readonly HashSet<string> NpcArgCommands = new HashSet<string>
    {
        "inspect", "move", "despawn", "force-kill", "force-faint", "set-component"
    };

    public DevConsoleAutocomplete(DevConsoleCommandDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call on Tab key press. Returns the completed input string.
    /// Resets cycle if <paramref name="currentInput"/> changed since the last Tab.
    /// </summary>
    public string Cycle(string currentInput, EngineHost host)
    {
        if (currentInput != _lastInput)
        {
            _lastInput  = currentInput;
            _cycleIndex = -1;
            _candidates = BuildCandidates(currentInput, host);
        }

        if (_candidates.Count == 0) return currentInput;

        _cycleIndex = (_cycleIndex + 1) % _candidates.Count;
        return _candidates[_cycleIndex];
    }

    /// <summary>Returns all current autocomplete candidates (read-only).</summary>
    public IReadOnlyList<string> GetCandidates(string currentInput, EngineHost host)
        => BuildCandidates(currentInput, host);

    /// <summary>Reset the cycle (called when any non-Tab key is pressed).</summary>
    public void Reset()
    {
        _cycleIndex = -1;
        _lastInput  = null;
        _candidates.Clear();
    }

    // ── Candidate building ────────────────────────────────────────────────────

    private List<string> BuildCandidates(string input, EngineHost host)
    {
        if (string.IsNullOrWhiteSpace(input))
            return CommandNames().ToList();

        var tokens = DevConsoleCommandDispatcher.Tokenize(input);

        if (tokens.Length <= 1)
        {
            // Still typing the command name — complete against command names.
            string partial = tokens.Length == 1 ? tokens[0].ToLowerInvariant() : string.Empty;
            return CommandNames()
                .Where(n => n.StartsWith(partial))
                .ToList();
        }

        // Arg mode: determine what the second token should be.
        string cmd = tokens[0].ToLowerInvariant();
        if (NpcArgCommands.Contains(cmd))
        {
            string partial = tokens.Length > 1 ? tokens[tokens.Length - 1].ToLowerInvariant() : string.Empty;
            return NpcNames(host)
                .Where(n => n.ToLowerInvariant().StartsWith(partial))
                .Select(n => $"{cmd} {n}")
                .ToList();
        }

        return new List<string>();
    }

    private IEnumerable<string> CommandNames()
    {
        if (_dispatcher?.Commands == null) return Enumerable.Empty<string>();
        return _dispatcher.Commands.Keys.OrderBy(k => k);
    }

    private static IEnumerable<string> NpcNames(EngineHost host)
    {
        if (host?.Engine?.Entities == null) yield break;
        foreach (var entity in host.Engine.Entities)
        {
            if (entity.Has<IdentityComponent>())
            {
                var id = entity.Get<IdentityComponent>();
                if (!string.IsNullOrEmpty(id.Name))
                    yield return id.Name.ToLowerInvariant();
            }
        }
    }
}

#endif
