#if WARDEN
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Parses console input and dispatches to registered <see cref="IDevConsoleCommand"/> — WP-3.1.H.
///
/// PARSING
/// ───────
/// Input is tokenised on whitespace; quoted sub-strings are treated as single tokens.
/// Example: spawn "the cynic" 5 10  ->  ["spawn", "the cynic", "5", "10"]
///
/// DISPATCH
/// ────────
/// First token = command name (case-insensitive).
/// Remaining tokens = args array passed to IDevConsoleCommand.Execute.
///
/// OUTPUT CONVENTIONS (used by DevConsolePanel to colour-code lines):
///   "ERROR: …"   — error (red)
///   "INFO: …"    — informational (grey)
///   (anything else) — success (green)
///   null/empty   — silent success
/// </summary>
public sealed class DevConsoleCommandDispatcher
{
    private readonly Dictionary<string, IDevConsoleCommand> _commands
        = new Dictionary<string, IDevConsoleCommand>(StringComparer.OrdinalIgnoreCase);

    private DevCommandContext _context;

    // ── Registration ──────────────────────────────────────────────────────────

    /// <summary>Register a command. Also registers its aliases.</summary>
    public void RegisterCommand(IDevConsoleCommand cmd)
    {
        if (cmd == null) throw new ArgumentNullException(nameof(cmd));
        _commands[cmd.Name] = cmd;
        if (cmd.Aliases != null)
        {
            foreach (var alias in cmd.Aliases)
                if (!string.IsNullOrEmpty(alias))
                    _commands[alias] = cmd;
        }
    }

    /// <summary>All registered commands (by primary name, deduped).</summary>
    public IReadOnlyDictionary<string, IDevConsoleCommand> Commands => _commands;

    // ── Context ───────────────────────────────────────────────────────────────

    public void SetContext(DevCommandContext ctx) => _context = ctx;
    public DevCommandContext Context => _context;

    // ── Execute ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Tokenise and execute <paramref name="input"/>.
    /// </summary>
    /// <param name="input">Raw user input including the command name.</param>
    /// <param name="output">Command output (may be null for silent success).</param>
    /// <returns>True on success; false if the command was not found.</returns>
    public bool Execute(string input, out string output)
    {
        output = null;
        if (string.IsNullOrWhiteSpace(input)) return true;

        var tokens = Tokenize(input);
        if (tokens.Length == 0) return true;

        string cmdName = tokens[0].ToLowerInvariant();

        if (!_commands.TryGetValue(cmdName, out var cmd))
        {
            output = $"ERROR: Unknown command '{cmdName}'. Type 'help' for a list.";
            return false;
        }

        string[] args = new string[tokens.Length - 1];
        Array.Copy(tokens, 1, args, 0, args.Length);

        try
        {
            output = cmd.Execute(args, _context ?? new DevCommandContext());
            return true;
        }
        catch (Exception ex)
        {
            output = $"ERROR: {cmdName} threw {ex.GetType().Name}: {ex.Message}";
            Debug.LogError($"[DevConsole] Command '{cmdName}' threw: {ex}");
            return false;
        }
    }

    // ── Tokeniser ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Splits <paramref name="input"/> into tokens on whitespace, respecting double-quoted strings.
    /// Example: 'spawn "the cynic" 5 10' → ["spawn", "the cynic", "5", "10"]
    /// </summary>
    public static string[] Tokenize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Array.Empty<string>();

        var tokens = new List<string>();
        var sb     = new StringBuilder();
        bool inQuote = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (c == '"')
            {
                inQuote = !inQuote;
                continue;
            }

            if (c == ' ' && !inQuote)
            {
                if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                    sb.Clear();
                }
                continue;
            }

            sb.Append(c);
        }

        if (sb.Length > 0)
            tokens.Add(sb.ToString());

        return tokens.ToArray();
    }
}

#endif
