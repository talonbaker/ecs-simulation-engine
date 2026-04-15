namespace APIFramework.Components;

public struct LiquidComponent
{
    public float VolumeMl;       // Physical volume that fills the stomach (ml per gulp)
    public float HydrationValue; // How much this drink reduces Thirst when digested
    public string LiquidType;    // "Water", "Coffee", etc.

    public override string ToString() => $"{LiquidType} ({VolumeMl:F0}ml | Hydr: {HydrationValue})";
}
