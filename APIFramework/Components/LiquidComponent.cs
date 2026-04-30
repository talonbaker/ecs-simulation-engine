namespace APIFramework.Components;

/// <summary>
/// A gulp of liquid traveling down the esophagus toward the stomach.
///
/// The `Nutrients` field carries Water (ml) plus any dissolved macros/minerals
/// (milk carries fats + calcium, orange juice carries carbs + vitamin C, etc.).
/// DigestionSystem absorbs them proportionally once the gulp arrives.
///
/// VolumeMl is physical stomach displacement, conceptually equal to Nutrients.Water
/// for pure water but potentially different for viscous or solute-heavy liquids.
/// </summary>
public struct LiquidComponent
{
    /// <summary>Physical volume filling the stomach per gulp (ml).</summary>
    public float VolumeMl;

    /// <summary>Full nutritional breakdown released to the stomach on arrival.</summary>
    public NutrientProfile Nutrients;

    /// <summary>Human-readable liquid label ("Water", "Milk", "Coffee", etc.).</summary>
    public string LiquidType;

    /// <summary>Debug-friendly liquid-type/volume/water summary.</summary>
    public override string ToString() =>
        $"{LiquidType} ({VolumeMl:F0}ml | Water: {Nutrients.Water:F0}ml)";
}
