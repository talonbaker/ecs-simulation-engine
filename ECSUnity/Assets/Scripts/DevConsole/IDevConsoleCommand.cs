#if WARDEN

/// <summary>
/// Contract for a single developer-console command — WP-3.1.H.
///
/// Each command is registered by name in <see cref="DevConsoleCommandDispatcher"/>.
/// The dispatcher calls <see cref="Execute"/> after tokenising the user's input.
///
/// Return value conventions:
///   - Non-null, non-empty string: success output (printed in green).
///   - String starting with "ERROR:": error (printed in red).
///   - String starting with "INFO:":  informational (printed in grey).
///   - Null or empty string:           silent success (nothing printed).
/// </summary>
public interface IDevConsoleCommand
{
    /// <summary>Primary command name (e.g. "help", "inspect"). Always lower-case.</summary>
    string Name { get; }

    /// <summary>Human-readable usage hint shown by the help command.</summary>
    string Usage { get; }

    /// <summary>One-line description shown by the help command.</summary>
    string Description { get; }

    /// <summary>Optional aliases (e.g. "?" for "help"). May be empty.</summary>
    string[] Aliases { get; }

    /// <summary>
    /// Execute the command.
    /// </summary>
    /// <param name="args">
    /// Tokenised arguments, NOT including the command name itself.
    /// args[0] is the first argument.
    /// </param>
    /// <param name="ctx">Runtime dependencies (engine host, mutation API, etc.).</param>
    /// <returns>Output string to display in the console history.</returns>
    string Execute(string[] args, DevCommandContext ctx);
}

#endif
