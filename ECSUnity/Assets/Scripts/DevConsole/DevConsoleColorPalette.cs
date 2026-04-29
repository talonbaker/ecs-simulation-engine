#if WARDEN
using UnityEngine;

/// <summary>
/// CRT-styled color constants for the developer console — WP-3.1.H.
///
/// Phosphor-green palette with per-severity tints.
/// All colors are tuned for legibility on a dark (#111118) background.
/// </summary>
public static class DevConsoleColorPalette
{
    /// <summary>Phosphor green — success output and the command prompt.</summary>
    public static readonly Color Success = new Color(0.235f, 0.702f, 0.443f); // #3CB371

    /// <summary>CRT red — error output.</summary>
    public static readonly Color Error   = new Color(0.816f, 0.251f, 0.125f); // #D04020

    /// <summary>Mid-grey — informational / neutral output.</summary>
    public static readonly Color Info    = new Color(0.467f, 0.467f, 0.533f); // #777788

    /// <summary>Near-white — the raw command the player typed.</summary>
    public static readonly Color Command = new Color(0.867f, 0.867f, 0.867f); // #DDDDDD

    /// <summary>Amber — non-fatal warning.</summary>
    public static readonly Color Warning = new Color(0.878f, 0.502f, 0.188f); // #E08030

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Maps a <see cref="ConsoleEntryKind"/> to the appropriate color.</summary>
    public static Color FromKind(ConsoleEntryKind kind)
    {
        switch (kind)
        {
            case ConsoleEntryKind.Success: return Success;
            case ConsoleEntryKind.Error:   return Error;
            case ConsoleEntryKind.Warning: return Warning;
            case ConsoleEntryKind.Command: return Command;
            default:                       return Info;
        }
    }

    /// <summary>Converts a Unity Color to an HTML hex string (#RRGGBB).</summary>
    public static string ToHex(Color c)
    {
        return $"#{Mathf.RoundToInt(c.r * 255):X2}" +
               $"{Mathf.RoundToInt(c.g * 255):X2}" +
               $"{Mathf.RoundToInt(c.b * 255):X2}";
    }
}

/// <summary>Kind of a console output entry, used to select its display color.</summary>
public enum ConsoleEntryKind
{
    /// <summary>Command the user typed (near-white).</summary>
    Command,
    /// <summary>Informational / neutral output (grey).</summary>
    Info,
    /// <summary>Success output (phosphor green).</summary>
    Success,
    /// <summary>Error output (red).</summary>
    Error,
    /// <summary>Warning output (amber).</summary>
    Warning,
}

/// <summary>Single entry in the console output history.</summary>
public struct ConsoleEntry
{
    public string           Text;
    public ConsoleEntryKind Kind;

    public ConsoleEntry(string text, ConsoleEntryKind kind)
    {
        Text = text;
        Kind = kind;
    }
}

#endif
