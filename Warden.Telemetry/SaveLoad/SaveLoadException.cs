using System;

namespace Warden.Telemetry.SaveLoad;

/// <summary>
/// Thrown when a save/load operation cannot proceed due to a schema mismatch,
/// malformed JSON, or missing required fields.
/// </summary>
public sealed class SaveLoadException : Exception
{
    public SaveLoadException(string message) : base(message) { }
    public SaveLoadException(string message, Exception inner) : base(message, inner) { }
}
