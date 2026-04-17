namespace APIFramework.Components;

/// <summary>
/// The large intestine (colon) — the water-reclamation and waste-compaction organ.
///
/// PIPELINE POSITION
/// ─────────────────
///   SmallIntestineSystem → LargeIntestineComponent → LargeIntestineSystem
///                        → MetabolismComponent.NutrientStores (recaptured water)
///                        → WasteReadyMl → (v0.7.3) RectumComponent
///
/// SmallIntestineSystem forwards its unabsorbed residue here: primarily fiber,
/// the unabsorbed water fraction, and the mineral fraction that escaped SI
/// absorption. LargeIntestineSystem then recaptures ~90% of the remaining water
/// into MetabolismComponent.NutrientStores, and compacts the dry remainder into
/// WasteReadyMl — the pre-rectal waste volume that will feed RectumComponent and
/// trigger the defecation desire in v0.7.3.
///
/// BIOLOGY NOTE
/// ─────────────
/// The large intestine is primarily a desiccation organ, not an absorption organ.
/// Almost no macronutrients or vitamins are absorbed here — the job is water
/// recovery and final waste packaging. The 90% water recapture fraction reflects
/// real physiology: the colon recovers roughly 1.3 L of the ~1.5 L of water that
/// enters it daily, leaving a formed stool of ~150-200 ml.
///
/// FORWARD COMPAT — v0.7.3
/// ────────────────────────
/// WasteReadyMl is the handoff field for RectumComponent. When it arrives in
/// v0.7.3, LargeIntestineSystem will transfer WasteReadyMl → RectumComponent
/// once the volume crosses a threshold (configured as TransitThresholdMl in
/// LargeIntestineSystemConfig). For now the field accumulates without being
/// drained — which is intentional: waste has to go somewhere, and v0.7.3
/// provides the outlet.
/// </summary>
public struct LargeIntestineComponent
{
    /// <summary>Capacity of the large intestine in ml. Roughly 500 ml is the practical adult limit.</summary>
    public const float CapacityMl = 500f;

    /// <summary>Current volume of intestinal contents still being processed (ml).</summary>
    public float CurrentVolumeMl;

    /// <summary>
    /// Nutrient breakdown of the contents still present in the large intestine.
    /// Primarily fiber and unabsorbed water at this stage. Macronutrients and
    /// most vitamins were absorbed upstream in the small intestine.
    /// </summary>
    public NutrientProfile Contents;

    /// <summary>
    /// Compacted dry waste volume (ml) ready to move to the rectum.
    /// Accumulates each tick as LargeIntestineSystem extracts water from
    /// the transiting mass. This is the v0.7.3 handoff field — RectumComponent
    /// will drain from here when it lands. Currently grows unbounded (until
    /// rectum/elimination arrives), which is biologically honest: waste must go
    /// somewhere. In practice this stays small because the SI residue fraction
    /// is a small percentage of the original food volume.
    /// </summary>
    public float WasteReadyMl;

    /// <summary>Normalised fill of the processing volume: 0.0 = empty, 1.0 = at capacity.</summary>
    public readonly float Fill => CurrentVolumeMl / CapacityMl;

    public readonly bool IsEmpty => CurrentVolumeMl <= 0f;

    public override string ToString() =>
        $"LargeIntestine: {Fill:P0} ({CurrentVolumeMl:F1}/{CapacityMl:F0} ml) | waste ready {WasteReadyMl:F1} ml";
}
