namespace APIFramework.Components;

/// <summary>
/// Tracks the gap between felt and performed drive state for an NPC.
/// Updated per-tick by SocialMaskSystem; crack detection is in MaskCrackSystem.
/// All mask values are 0–100.
/// </summary>
public struct SocialMaskComponent
{
    /// <summary>Suppressed irritation: hiding annoyance.</summary>
    public int IrritationMask;

    /// <summary>Suppressed affection: hiding fondness.</summary>
    public int AffectionMask;

    /// <summary>Suppressed attraction: hiding a crush.</summary>
    public int AttractionMask;

    /// <summary>Suppressed loneliness: pretending fine.</summary>
    public int LonelinessMask;

    /// <summary>Aggregate willpower load this mask currently demands per tick (0–100).</summary>
    public int CurrentLoad;

    /// <summary>Baseline mask strength derived from personality at spawn (0–100).</summary>
    public int Baseline;

    /// <summary>Tick when the last MaskSlip fired for this NPC (0 = never).</summary>
    public long LastSlipTick;
}
