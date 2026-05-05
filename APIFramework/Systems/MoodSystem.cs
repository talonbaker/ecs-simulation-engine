using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Manages Billy's emotional state each tick using Plutchik's eight primary emotions.
///
/// PIPELINE POSITION
/// ------------------
/// Position 5 — after BiologicalConditionSystem sets vital tags, before BrainSystem
/// scores drives.  This means:
///   - Biological tags (HungerTag, IrritableTag, etc.) are current this tick.
///   - DriveComponent.Dominant contains the PREVIOUS tick's value (one tick lag,
///     negligible for game-time scales).  Used to detect the idle/boredom state.
///   - BrainSystem will read emotion tags we set this tick (BoredTag for urgency bonus,
///     SadTag for urgency suppression).
///
/// INPUT WIRING
/// -------------
///   JOY          ← all of satiation, hydration, energy above comfortable thresholds
///   TRUST        ← (stub) no inputs yet — no environmental stability model
///   FEAR         ← (stub) no inputs yet — no threat entities
///   SURPRISE     ← (stub) no inputs yet — no sudden-change detection
///   SADNESS      ← HungerTag or ThirstTag present this tick
///   DISGUST      ← DesireType.None last tick (boredom accumulation)
///                ← ConsumedRottenFoodTag spike (ate something rotten)
///   ANGER        ← IrritableTag present this tick
///   ANTICIPATION ← hunger or thirst above a low threshold but drive not yet dominant
///
/// OUTPUT EFFECTS (read by other systems)
/// ---------------------------------------
///   BoredTag     → BrainSystem adds a flat urgency bonus to all drives
///   SadTag       → BrainSystem suppresses all drive urgency scores
///   GriefTag     → BrainSystem heavily suppresses all drive urgency scores
///   AngryTag     → MetabolismSystem raises drain rates (stress physiology)
/// </summary>
/// <remarks>
/// Reads: <see cref="MoodComponent"/>, <see cref="MetabolismComponent"/>,
/// <see cref="EnergyComponent"/>, <see cref="DriveComponent"/>,
/// <see cref="HungerTag"/>, <see cref="ThirstTag"/>, <see cref="IrritableTag"/>,
/// <see cref="ConsumedRottenFoodTag"/>, <see cref="LifeStateComponent"/>.<br/>
/// Writes: <see cref="MoodComponent"/> (single writer), all Plutchik intensity tags
/// (Serene/Joyful/Ecstatic, Accepting/Trusting/Admiring, Apprehensive/Fearful/Terror,
/// Distracted/Surprised/Amazed, Pensive/Sad/Grief, Bored/Disgust/Loathing,
/// Annoyed/Angry/Raging, Interested/Anticipating/Vigilant); consumes
/// <see cref="ConsumedRottenFoodTag"/>.<br/>
/// Phase: Cognition, before <see cref="BrainSystem"/>.
/// </remarks>
public class MoodSystem : ISystem
{
    private readonly MoodSystemConfig _cfg;

    /// <summary>Constructs the mood system with its tuning.</summary>
    /// <param name="cfg">Mood tuning (decay rates, gain rates, intensity thresholds).</param>
    public MoodSystem(MoodSystemConfig cfg) => _cfg = cfg;

    /// <summary>Per-tick emotion update; decays all emotions, applies gains, then refreshes intensity tags.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<MoodComponent>())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip non-Alive NPCs
            var mood = entity.Get<MoodComponent>();

            // -- Decay all emotions toward zero --------------------------------
            mood.Joy          = Decay(mood.Joy,          _cfg.PositiveDecayRate, deltaTime);
            mood.Trust        = Decay(mood.Trust,        _cfg.PositiveDecayRate, deltaTime);
            mood.Anticipation = Decay(mood.Anticipation, _cfg.PositiveDecayRate, deltaTime);
            mood.Fear         = Decay(mood.Fear,         _cfg.NegativeDecayRate, deltaTime);
            mood.Sadness      = Decay(mood.Sadness,      _cfg.NegativeDecayRate, deltaTime);
            mood.Disgust      = Decay(mood.Disgust,      _cfg.NegativeDecayRate, deltaTime);
            mood.Anger        = Decay(mood.Anger,        _cfg.NegativeDecayRate, deltaTime);
            mood.Surprise     = Decay(mood.Surprise,     _cfg.SurpriseDecayRate, deltaTime);

            // -- JOY: needs comfortably met ------------------------------------
            if (entity.Has<MetabolismComponent>() && entity.Has<EnergyComponent>())
            {
                var meta   = entity.Get<MetabolismComponent>();
                var energy = entity.Get<EnergyComponent>();

                bool comfortable = meta.Satiation  >= _cfg.JoyComfortThreshold
                                && meta.Hydration  >= _cfg.JoyComfortThreshold
                                && energy.Energy   >= _cfg.JoyComfortThreshold
                                && !energy.IsSleeping; // awake enjoyment only

                if (comfortable)
                    mood.Joy = MathF.Min(100f, mood.Joy + _cfg.JoyGainRate * deltaTime);
            }

            // -- ANGER: irritability unresolved --------------------------------
            if (entity.Has<IrritableTag>())
                mood.Anger = MathF.Min(100f, mood.Anger + _cfg.AngerGainRate * deltaTime);

            // -- SADNESS: sustained hunger or thirst ---------------------------
            if (entity.Has<HungerTag>() || entity.Has<ThirstTag>())
                mood.Sadness = MathF.Min(100f, mood.Sadness + _cfg.SadnessGainRate * deltaTime);

            // -- DISGUST (BOREDOM): idle state sustained -----------------------
            // Read last tick's DriveComponent — if nothing was dominant, accumulate boredom.
            if (entity.Has<DriveComponent>())
            {
                if (entity.Get<DriveComponent>().Dominant == DesireType.None)
                    mood.Disgust = MathF.Min(100f, mood.Disgust + _cfg.BoredGainRate * deltaTime);
            }

            // -- DISGUST: rotten food consumed (spike) -------------------------
            if (entity.Has<ConsumedRottenFoodTag>())
            {
                mood.Disgust = MathF.Min(100f, mood.Disgust + _cfg.RottenFoodDisgustSpike);
                mood.Surprise = MathF.Min(100f, mood.Surprise + _cfg.RottenFoodDisgustSpike * 0.5f);
                entity.Remove<ConsumedRottenFoodTag>(); // consume the signal
            }

            // -- ANTICIPATION: drive building but not yet critical -------------
            if (entity.Has<MetabolismComponent>())
            {
                var meta = entity.Get<MetabolismComponent>();
                bool hungerBuilding = meta.Hunger > _cfg.AnticipationHungerMin
                                   && meta.Hunger < _cfg.AnticipationHungerMax;
                bool thirstBuilding = meta.Thirst > _cfg.AnticipationHungerMin
                                   && meta.Thirst < _cfg.AnticipationHungerMax;

                if (hungerBuilding || thirstBuilding)
                    mood.Anticipation = MathF.Min(100f, mood.Anticipation + _cfg.AnticipationGainRate * deltaTime);
            }

            // -- Write back ----------------------------------------------------
            entity.Add(mood);

            // -- Apply intensity tags ------------------------------------------
            ApplyEmotionTags(entity, mood);
        }
    }

    // -- Tag application -------------------------------------------------------

    private void ApplyEmotionTags(Entity entity, MoodComponent mood)
    {
        SetIntensityTag<SereneTag,      JoyfulTag,       EcstaticTag>   (entity, mood.Joy);
        SetIntensityTag<AcceptingTag,   TrustingTag,     AdmiringTag>   (entity, mood.Trust);
        SetIntensityTag<ApprehensiveTag,FearfulTag,      TerrorTag>     (entity, mood.Fear);
        SetIntensityTag<DistractedTag,  SurprisedTag,    AmazedTag>     (entity, mood.Surprise);
        SetIntensityTag<PensiveTag,     SadTag,          GriefTag>      (entity, mood.Sadness);
        SetIntensityTag<BoredTag,       DisgustTag,      LoathingTag>   (entity, mood.Disgust);
        SetIntensityTag<AnnoyedTag,     AngryTag,        RagingTag>     (entity, mood.Anger);
        SetIntensityTag<InterestedTag,  AnticipatingTag, VigilantTag>   (entity, mood.Anticipation);
    }

    private void SetIntensityTag<TLow, TMid, THigh>(Entity entity, float value)
        where TLow  : struct
        where TMid  : struct
        where THigh : struct
    {
        entity.Remove<TLow>();
        entity.Remove<TMid>();
        entity.Remove<THigh>();

        if      (value >= _cfg.HighThreshold) entity.Add(new THigh());
        else if (value >= _cfg.MidThreshold)  entity.Add(new TMid());
        else if (value >= _cfg.LowThreshold)  entity.Add(new TLow());
    }

    // -- Helpers ---------------------------------------------------------------

    private static float Decay(float value, float rate, float deltaTime) =>
        MathF.Max(0f, value - rate * deltaTime);
}
