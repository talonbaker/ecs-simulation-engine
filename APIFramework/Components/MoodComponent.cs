namespace APIFramework.Components;

/// <summary>
/// Stores Billy's emotional state as eight continuous float values (0–100),
/// one per primary emotion on Plutchik's Wheel.
///
/// DESIGN INTENT
/// ─────────────
/// These are raw intensities, not tags.  MoodSystem reads them each tick and
/// applies the appropriate intensity-level tag (e.g. Disgust 12 → BoredTag,
/// Disgust 45 → DisgustTag, Disgust 80 → LoathingTag).
///
/// All values default to 0.  MoodSystem will raise or lower them based on
/// incoming stimuli from other systems once those connections are wired.
///
/// EMOTION FAMILIES  (Plutchik's Wheel — outer petal → middle ring → center)
/// ─────────────────────────────────────────────────────────────────────────
///   Joy          →  Serenity / Joy / Ecstasy
///   Trust        →  Acceptance / Trust / Admiration
///   Fear         →  Apprehension / Fear / Terror
///   Surprise     →  Distraction / Surprise / Amazement
///   Sadness      →  Pensiveness / Sadness / Grief
///   Disgust      →  Boredom / Disgust / Loathing
///   Anger        →  Annoyance / Anger / Rage
///   Anticipation →  Interest / Anticipation / Vigilance
///
/// PLANNED INPUTS (not yet wired)
/// ───────────────────────────────
///   Joy          ← needs met (satiation + hydration + energy all above thresholds)
///   Trust        ← stable familiar environment, no threats
///   Fear         ← threat entities nearby, sudden vital-stat drops
///   Surprise     ← unexpected entity spawns, large stat deltas in one tick
///   Sadness      ← sustained HungerTag/ThirstTag without relief, prolonged ExhaustedTag
///   Disgust      ← proximity to RotTag entities, forced rotten-food consumption,
///                   idle state (Dominant == None sustained) → low-intensity Boredom
///   Anger        ← sustained IrritableTag unresolved, drive repeatedly blocked
///   Anticipation ← food/water entity visible but not consumed, drive urgency rising
/// </summary>
public struct MoodComponent
{
    // ── Primary Emotions (0 = absent, 100 = peak intensity) ──────────────────

    /// <summary>Positive emotion from needs being met and comfort. Low = serenity, High = ecstasy.</summary>
    public float Joy;

    /// <summary>Positive social/environmental emotion. Low = acceptance, High = admiration.</summary>
    public float Trust;

    /// <summary>Aversive emotion triggered by threats or danger. Low = apprehension, High = terror.</summary>
    public float Fear;

    /// <summary>Reactive emotion from unexpected stimuli. Low = distraction, High = amazement.</summary>
    public float Surprise;

    /// <summary>Negative emotion from sustained deprivation or loss. Low = pensiveness, High = grief.</summary>
    public float Sadness;

    /// <summary>
    /// Aversive emotion from foul stimuli or understimulation.
    /// At low intensity this is Boredom — the same drive, just without a specific target.
    /// Low = boredom, High = loathing.
    /// </summary>
    public float Disgust;

    /// <summary>Reactive emotion from blocked drives or sustained irritability. Low = annoyance, High = rage.</summary>
    public float Anger;

    /// <summary>Forward-directed emotion toward upcoming rewards or goals. Low = interest, High = vigilance.</summary>
    public float Anticipation;

    // ── Choking / acute-panic (WP-3.0.1) ────────────────────────────────────

    /// <summary>
    /// Acute panic level (0..1) set by <see cref="APIFramework.Systems.LifeState.ChokingDetectionSystem"/>
    /// at the moment of a choke event. MoodSystem decays this toward 0 each tick
    /// at <see cref="APIFramework.Config.MoodSystemConfig.NegativeDecayRate"/> per game-second (follow-up:
    /// add explicit PanicDecayRate to MoodSystemConfig if tuning is needed).
    ///
    /// While <c>PanicLevel &gt; 0</c>, the NPC is in a panic state. The Incapacitated
    /// life-state guard in FacingSystem already freezes facing; this field provides a
    /// queryable hook for future systems (UI animations, witness reactions, etc.).
    /// </summary>
    public float PanicLevel;

    // ── Bereavement / grief (WP-3.0.2) ───────────────────────────────────────

    /// <summary>
    /// Bereavement grief intensity (0–100), applied by
    /// <see cref="APIFramework.Systems.LifeState.BereavementSystem"/> at the moment a
    /// death event is received. Uses the same 0–100 scale as the eight primary Plutchik
    /// emotions; MoodSystem decays this toward 0 each tick at
    /// <see cref="APIFramework.Config.MoodSystemConfig.NegativeDecayRate"/> per game-second.
    ///
    /// Distinct from the structural <see cref="Sadness"/> field so that bereavement grief
    /// can be set and tracked independently of hunger/thirst-driven sadness.
    /// </summary>
    public float GriefLevel;

    // ── Convenience ───────────────────────────────────────────────────────────

    /// <summary>True if any emotion is above a negligible threshold (not emotionally neutral).</summary>
    public readonly bool HasAnyEmotion =>
        Joy > 5f || Trust > 5f || Fear > 5f || Surprise > 5f ||
        Sadness > 5f || Disgust > 5f || Anger > 5f || Anticipation > 5f;

    /// <summary>
    /// Net emotional valence: positive emotions minus negative ones.
    /// Positive = net positive mood. Negative = net negative mood.
    /// </summary>
    public readonly float Valence =>
        (Joy + Trust + Anticipation) - (Fear + Sadness + Disgust + Anger);

    /// <summary>Debug-friendly summary of all eight Plutchik intensity values.</summary>
    public override string ToString() =>
        $"Joy:{Joy:F0} Trust:{Trust:F0} Fear:{Fear:F0} Surprise:{Surprise:F0} " +
        $"Sadness:{Sadness:F0} Disgust:{Disgust:F0} Anger:{Anger:F0} Anticipation:{Anticipation:F0}";
}
