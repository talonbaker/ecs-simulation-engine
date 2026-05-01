#if WARDEN
// InspectRoomCommand.cs
// Dumps the state of a room from the WorldStateDto snapshot, including bounds and
// illumination data. Rooms are identified by Id or Name (case-insensitive).
//
// Note: this command reads from the WorldStateDto snapshot, not directly from any
// room entity. Room data is populated by the lighting/room systems each tick.
// If WorldState has not yet been published (e.g., engine not yet running), the
// command returns an informative error.
//
// Usage:
//   inspect-room <roomId|name>
//
// Example:
//   inspect-room kitchen
//   inspect-room 3f2504e0-4f89-11d3-9a0c-0305e82c3301
//
// Return conventions:
//   Plain multi-line string on success.
//   "ERROR: ..."  on failure.

using System.Text;
using Warden.Contracts.Telemetry;

public sealed class InspectRoomCommand : IDevConsoleCommand
{
    public string Name        => "inspect-room";
    public string Usage       => "inspect-room <roomId|name>";
    public string Description => "Print room state including occupants and lighting.";
    public string[] Aliases   => System.Array.Empty<string>();

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length == 0)
            return "ERROR: Usage: " + Usage;

        if (ctx.Host?.WorldState?.Rooms == null)
            return "ERROR: No room data available. Is the engine running?";

        string query = args[0].ToLowerInvariant();
        RoomDto room = null;

        foreach (var r in ctx.Host.WorldState.Rooms)
        {
            if (r == null) continue;

            bool matchId   = r.Id?.ToLowerInvariant()   == query;
            bool matchName = r.Name?.ToLowerInvariant() == query;

            if (matchId || matchName) { room = r; break; }
        }

        if (room == null)
            return $"ERROR: Room '{args[0]}' not found.";

        var sb = new StringBuilder();
        sb.AppendLine($"Room: {room.Id}");
        sb.AppendLine($"  Name:   {room.Name}");
        sb.AppendLine($"  Bounds: origin ({room.X:F1}, {room.Z:F1})  size {room.Width:F1} x {room.Height:F1}");

        // Illumination block — only present when the lighting system has run.
        if (room.Illumination != null)
        {
            sb.AppendLine($"  Ambient level:       {room.Illumination.AmbientLevel:F2}");
            sb.AppendLine($"  Color temperature K: {room.Illumination.ColorTemperatureK}");
        }
        else
        {
            sb.AppendLine($"  Illumination: (not yet computed)");
        }

        return sb.ToString().TrimEnd();
    }
}
#endif
