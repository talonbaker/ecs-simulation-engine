namespace APIFramework.Components;

public struct EsophagusTransitComponent
{
    public float Progress;       // 0.0 to 1.0 (internal math)
    public float Speed;          // How fast it moves
    public Guid TargetEntityId;  // The ID of the Human/Cat swallowing it

    // This property fixes the UI/Logic errors by mapping Position to Progress
    public int Position => (int)(Progress * 100);

    public override string ToString() => $"Pos: {Position}% (Speed: {Speed:F2}/s)";
}