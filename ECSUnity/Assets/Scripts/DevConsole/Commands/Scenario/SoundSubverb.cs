#if WARDEN
using APIFramework.Systems.Audio;

/// <summary>
/// scenario sound &lt;SoundTriggerKind&gt; [at &lt;x,y,z&gt;]
/// Emits the named SoundTriggerKind directly to the bus. Default position = camera focus.
/// </summary>
public sealed class SoundSubverb : IScenarioSubverb
{
    public string Name        => "sound";
    public string Usage       => "scenario sound <kind> [at <x,z>]  (kind: Cough|ChairSqueak|BulbBuzz|…)";
    public string Description => "Emit a sound trigger at the camera focus (or a specified position).";

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length == 0)
            return "ERROR: Usage: " + Usage;

        if (!System.Enum.TryParse<SoundTriggerKind>(args[0], ignoreCase: true, out var kind))
        {
            return $"ERROR: Unknown SoundTriggerKind '{args[0]}'. " +
                   $"Valid: Cough, ChairSqueak, BulbBuzz, Footstep, SpeechFragment, " +
                   $"Crash, Glass, Thud, Heimlich, DoorUnlock (or any SoundTriggerKind value).";
        }

        if (ctx.Host?.SoundBus == null)
            return "ERROR: SoundBus not available.";

        // Resolve position: optional "at x,z" or camera focus.
        float emitX = 0f, emitZ = 0f;
        bool posResolved = false;

        if (args.Length >= 3 && string.Equals(args[1], "at", System.StringComparison.OrdinalIgnoreCase))
        {
            var parts = args[2].Split(',');
            if (parts.Length >= 2 &&
                float.TryParse(parts[0], out float px) &&
                float.TryParse(parts[1], out float pz))
            {
                emitX = px;
                emitZ = pz;
                posResolved = true;
            }
            else
            {
                return $"ERROR: Could not parse position '{args[2]}'. Expected x,z (e.g. 5,3).";
            }
        }

        if (!posResolved)
        {
            // Fall back to camera focus if available.
            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                emitX = cam.transform.position.x;
                emitZ = cam.transform.position.z;
            }
        }

        ctx.Host.SoundBus.Emit(kind, System.Guid.Empty, emitX, emitZ, 1.0f, ctx.Host.TickCount);
        return $"Emitted {kind} at ({emitX:F1}, {emitZ:F1}).";
    }
}
#endif
