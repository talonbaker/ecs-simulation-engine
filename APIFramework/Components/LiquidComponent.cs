namespace APIFramework.Components;

public struct LiquidComponent
{
    public float HydrationValue; // How much this drink satisfies thirst
    public string LiquidType;    // "Water", "Coffee", etc.

    public override string ToString() => $"{LiquidType} ({HydrationValue} units)";
}