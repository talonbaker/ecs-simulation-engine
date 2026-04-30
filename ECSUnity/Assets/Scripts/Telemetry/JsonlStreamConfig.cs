using UnityEngine;

/// <summary>
/// Configuration ScriptableObject for <see cref="JsonlStreamEmitter"/> (WP-3.1.F).
///
/// Create an instance via Assets > Create > Warden > JsonlStreamConfig
/// or use the pre-built DefaultJsonlStreamConfig asset in Assets/Settings/.
///
/// NOTE: This class is NOT gated behind #if WARDEN so that RETAIL code that
/// holds a null reference to it still compiles.  The emitter itself is WARDEN-only.
/// </summary>
[CreateAssetMenu(
    menuName = "Warden/JsonlStreamConfig",
    fileName = "DefaultJsonlStreamConfig")]
public sealed class JsonlStreamConfig : ScriptableObject
{
    // ── Cadence ───────────────────────────────────────────────────────────────

    [Tooltip("Emit one JSONL line every N engine ticks. Default 30 = ~1/s at 50 ticks/s.")]
    [Range(1, 1000)]
    public int EmitEveryNTicks = 30;

    // ── Output path ───────────────────────────────────────────────────────────

    [Tooltip("Output file path. Relative paths resolve from Application.dataPath parent. " +
             "Default 'Logs/worldstate.jsonl' writes alongside the Unity executable.")]
    public string OutputPath = "Logs/worldstate.jsonl";

    // ── Rotation ──────────────────────────────────────────────────────────────

    [Tooltip("Rotate the JSONL file when its size exceeds this many bytes. Default 100 MB.")]
    public long RotationSizeBytes = 100L * 1024L * 1024L;  // 100 MB

    // ── Formatting ────────────────────────────────────────────────────────────

    [Tooltip("Pretty-print JSON. Never enable in production; debug/comparison only. " +
             "Pretty-printed output is NOT consumed correctly by ai-stream consumers.")]
    public bool PrettyPrint = false;

    // ── Runtime-accessible defaults ───────────────────────────────────────────

    /// <summary>
    /// Estimated bytes per JSONL line at default field density.
    /// Used by the CadenceDebugOverlay to estimate disk impact.
    /// Approximate; actual size depends on world complexity.
    /// </summary>
    public const int EstimatedBytesPerLine = 4096;

    /// <summary>
    /// Estimated bytes written per minute at the default cadence (30 ticks/emit,
    /// 50 ticks/s = 100 lines/min * 4 KB/line = ~400 KB/min).
    /// </summary>
    public static long EstimatedBytesPerMinute(int emitEveryNTicks, float ticksPerSecond = 50f)
    {
        float emitsPerSecond = ticksPerSecond / emitEveryNTicks;
        float emitsPerMinute = emitsPerSecond * 60f;
        return (long)(emitsPerMinute * EstimatedBytesPerLine);
    }
}
