namespace APIFramework.Components;

/// <summary>
/// Carried by a bolus or liquid entity in transit down the esophagus.
/// EsophagusTransitSystem advances <see cref="Progress"/> each tick; on arrival the
/// entity is destroyed and its nutrients are transferred to the target's
/// <see cref="StomachComponent"/>.
/// </summary>
public struct EsophagusTransitComponent
{
    /// <summary>Transit progress in [0.0, 1.0]. 1.0 means the bolus has reached the stomach.</summary>
    public float Progress;       // 0.0 to 1.0 (internal math)
    /// <summary>Progress gained per game-second (how fast the bolus moves).</summary>
    public float Speed;          // How fast it moves
    /// <summary>Entity id of the consumer swallowing this bolus/liquid.</summary>
    public Guid TargetEntityId;  // The ID of the Human/Cat swallowing it

    /// <summary>
    /// Integer percentage form of <see cref="Progress"/> (0–100) for UI display
    /// without exposing the internal float math.
    /// </summary>
    public int Position => (int)(Progress * 100);

    /// <summary>Debug-friendly transit-percentage and speed summary.</summary>
    public override string ToString() => $"Pos: {Position}% (Speed: {Speed:F2}/s)";
}