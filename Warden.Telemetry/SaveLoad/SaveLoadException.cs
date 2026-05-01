namespace Warden.Telemetry.SaveLoad;

public sealed class SaveLoadException : Exception
{
    public SaveLoadException(string message) : base(message) { }
    public SaveLoadException(string message, Exception inner) : base(message, inner) { }
}