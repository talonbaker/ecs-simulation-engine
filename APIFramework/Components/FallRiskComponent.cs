namespace APIFramework.Components;

/// <summary>
/// Marks an entity as a fall hazard, with a risk level that drives slip-and-fall probability.
/// Attached to:
/// - Stain entities (water, blood, oil) with risk values from stain-fall-risk.json.
/// - Broken-item entities (broken mug, shattered glass) with default risk per kind.
/// - Future: ice patches, banana peels, polished-just-now floors.
///
/// Not all stains are fall risks, and not all fall risks are stains. Decoupling keeps the surface clean.
/// </summary>
public struct FallRiskComponent
{
    /// <summary>Fall risk level in range [0.0, 1.0]. Used as a multiplier in the slip roll.</summary>
    public float RiskLevel;
}
