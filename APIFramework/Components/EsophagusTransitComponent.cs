namespace APIFramework.Components;

public struct EsophagusTransitComponent
{
    public float Progress;
    public float Speed;

    public override string ToString() => $"Pos: {Progress:P0} (Speed: {Speed:F2}/s)";
}