namespace APIFramework.Components;

/// <summary>
/// Tracks the age and decay state of a food entity over game time.
///
/// HOW IT WORKS
/// ────────────
/// RotSystem increments AgeSeconds every tick.  Once AgeSeconds exceeds RotStartAge
/// (the freshness window), RotLevel begins climbing at RotRate per game-second.
/// When RotLevel exceeds the RotSystem's configured threshold, RotTag is applied.
///
/// FRESHNESS WINDOW (RotStartAge examples at TimeScale 120)
/// ─────────────────────────────────────────────────────────
///   0 game-seconds   → spoils instantly (pre-rotted food for testing)
///   3600             → 1 game-hour of freshness
///   86400            → 1 game-day (a banana left on the counter)
///   259200           → 3 game-days (definitely overripe)
///   604800           → 1 game-week (completely inedible)
///
/// PLANNED: once proximity/world-grid exists, RotLevel also raises Disgust in
/// entities nearby — smelling rot before eating it.
/// </summary>
public struct RotComponent
{
    /// <summary>Total game-seconds this food entity has existed since spawn.</summary>
    public float AgeSeconds;

    /// <summary>Current decay level (0 = perfectly fresh, 100 = completely rotten).</summary>
    public float RotLevel;

    /// <summary>
    /// Game-seconds of freshness before decay begins.
    /// 0 = spoils immediately (useful for testing pre-rotted food).
    /// </summary>
    public float RotStartAge;

    /// <summary>RotLevel gained per game-second once past the freshness window.</summary>
    public float RotRate;

    /// <summary>True when this food entity is past its freshness window.</summary>
    public readonly bool IsDecaying => AgeSeconds >= RotStartAge;

    /// <summary>Normalised freshness (1.0 = perfectly fresh, 0.0 = completely rotten).</summary>
    public readonly float Freshness => MathF.Max(0f, 1f - RotLevel / 100f);

    /// <summary>Debug-friendly age/rot/state summary.</summary>
    public override string ToString() =>
        $"Age: {AgeSeconds:F0}s  Rot: {RotLevel:F1}%  {(IsDecaying ? "DECAYING" : "fresh")}";
}
