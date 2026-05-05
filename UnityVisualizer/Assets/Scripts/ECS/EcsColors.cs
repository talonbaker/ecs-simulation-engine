using UnityEngine;
using APIFramework.Components;
using APIFramework.Core;

/// <summary>
/// Central colour palette for the ECS visualiser.
/// Edit these to restyle the whole scene without touching individual views.
/// </summary>
public static class EcsColors
{
    // -- Organ colours ---------------------------------------------------------
    public static readonly Color Esophagus      = new Color(0.65f, 0.65f, 0.65f); // grey tube
    public static readonly Color Stomach        = new Color(1.00f, 0.60f, 0.10f); // orange  (food being digested)
    public static readonly Color SmallIntestine = new Color(0.95f, 0.55f, 0.20f); // orange-tan (nutrient chyme)
    public static readonly Color LargeIntestine = new Color(0.65f, 0.38f, 0.12f); // medium brown
    public static readonly Color Colon          = new Color(0.38f, 0.18f, 0.04f); // dark brown  (waste)
    public static readonly Color Bladder        = new Color(0.90f, 0.82f, 0.08f); // YELLOW  — urine

    // -- Transit / discharge colours -------------------------------------------
    public static readonly Color Bolus          = new Color(1.00f, 0.55f, 0.10f); // orange  — food bolus in esophagus
    public static readonly Color Water          = new Color(0.20f, 0.80f, 1.00f); // cyan    — water bolus in esophagus
    public static readonly Color Urine          = new Color(0.92f, 0.84f, 0.06f); // yellow  — bladder discharge drop
    public static readonly Color Waste          = new Color(0.35f, 0.16f, 0.03f); // brown   — colon discharge drop

    // -- Status colours --------------------------------------------------------
    public static readonly Color Warn           = new Color(1.00f, 0.65f, 0.00f); // amber
    public static readonly Color Critical       = Color.red;
    public static readonly Color OrganEmpty     = new Color(0.15f, 0.15f, 0.15f); // near-black (empty organ)

    // -- World object colours --------------------------------------------------
    public static readonly Color FridgeColor    = new Color(0.50f, 0.80f, 1.00f); // light blue
    public static readonly Color SinkColor      = new Color(0.75f, 0.88f, 0.92f); // silver-blue
    public static readonly Color ToiletColor    = new Color(0.95f, 0.95f, 0.95f); // off-white
    public static readonly Color BedColor       = new Color(0.70f, 0.50f, 0.30f); // warm wood

    // -- Entity body colour by dominant drive / state --------------------------
    public static Color ForEntity(EntitySnapshot snap)
    {
        if (snap.IsSleeping)
            return new Color(0.30f, 0.30f, 0.80f);   // blue        — sleeping

        if (snap.ColonIsCritical || snap.BladderIsCritical)
            return Critical;                           // red         — emergency

        return snap.Dominant switch
        {
            DesireType.Eat      => snap.IsMoving
                                    ? new Color(1.0f, 0.75f, 0.15f)   // bright gold — walking to fridge
                                    : new Color(0.9f, 0.50f, 0.00f),   // orange      — eating
            DesireType.Drink    => snap.IsMoving
                                    ? new Color(0.3f, 0.90f, 1.00f)   // bright cyan — walking to sink
                                    : new Color(0.1f, 0.60f, 0.90f),   // blue        — drinking
            DesireType.Sleep    => new Color(0.4f, 0.40f, 0.90f),     // purple-blue — sleepy
            DesireType.Defecate => new Color(0.6f, 0.40f, 0.10f),     // brown
            DesireType.Pee      => new Color(0.8f, 0.80f, 0.20f),     // yellow
            _                   => new Color(0.3f, 0.80f, 0.30f),     // green       — idle / all good
        };
    }

    public static Color ForWorldObject(WorldObjectSnapshot obj)
    {
        if (obj.IsFridge)  return FridgeColor;
        if (obj.IsSink)    return SinkColor;
        if (obj.IsToilet)  return ToiletColor;
        if (obj.IsBed)     return BedColor;
        return Color.white;
    }
}
