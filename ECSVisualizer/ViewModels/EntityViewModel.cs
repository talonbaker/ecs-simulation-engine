using APIFramework.Components;
using APIFramework.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace ECSVisualizer.ViewModels;

/// <summary>
/// Maps one ECS Entity's current component state into observable properties for the UI.
/// Update() is called every tick by MainViewModel — this class never touches the engine directly.
/// </summary>
public partial class EntityViewModel : ObservableObject
{
    // ── Identity ─────────────────────────────────────────────────────────────
    [ObservableProperty] private string _entityId = "";
    [ObservableProperty] private string _name     = "";

    // ── Active Tags ───────────────────────────────────────────────────────────
    [ObservableProperty] private string _activeTags    = "";
    [ObservableProperty] private bool   _hasActiveTags = false;

    // ── Metabolism — Resources ────────────────────────────────────────────────
    [ObservableProperty] private bool   _hasMetabolism    = false;
    [ObservableProperty] private float  _satiation        = 100f;
    [ObservableProperty] private float  _hydration        = 100f;
    [ObservableProperty] private string _satiationLabel   = "100%";
    [ObservableProperty] private string _hydrationLabel   = "100%";

    // ── Metabolism — Body Temperature ────────────────────────────────────────
    [ObservableProperty] private float  _bodyTemp      = 37.0f;
    [ObservableProperty] private string _bodyTempLabel = "37.0°C";
    // Tier 0=hypothermia(<35), 1=normal(35–37.5), 2=fever(37.6–39), 3=hyperthermia(>39)
    [ObservableProperty] private int    _bodyTempTier  = 1;

    // ── Metabolism — Sensations ───────────────────────────────────────────────
    [ObservableProperty] private string _hungerLabel  = "Not hungry";
    [ObservableProperty] private string _thirstLabel  = "Not thirsty";

    // ── Stomach ───────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _hasStomach      = false;
    [ObservableProperty] private float  _stomachFill     = 0f;
    [ObservableProperty] private string _stomachLabel    = "";
    [ObservableProperty] private string _digestionLabel  = "";

    // ── Body Nutrient Stores (v0.7.0+) ────────────────────────────────────────
    // Cumulative real-biology stores extracted by DigestionSystem.
    [ObservableProperty] private bool   _hasNutrients     = false;
    [ObservableProperty] private float  _nutrientCalories = 0f;
    [ObservableProperty] private string _nutrientCaloriesLabel = "0 kcal";
    [ObservableProperty] private string _nutrientMacrosLabel   = "";
    [ObservableProperty] private string _nutrientWaterLabel    = "";
    [ObservableProperty] private string _nutrientVitaminsLabel = "";
    [ObservableProperty] private string _nutrientMineralsLabel = "";

    // ── Energy / Sleep ────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _hasEnergy        = false;
    [ObservableProperty] private float  _energy           = 100f;
    [ObservableProperty] private float  _sleepiness       = 0f;
    [ObservableProperty] private string _energyLabel      = "100%";
    [ObservableProperty] private string _sleepinessLabel  = "0%";
    [ObservableProperty] private string _sleepStateLabel  = "Awake";
    [ObservableProperty] private bool   _isSleeping       = false;

    // ── Brain / Priority Queue ────────────────────────────────────────────────
    [ObservableProperty] private bool   _hasDrives       = false;
    [ObservableProperty] private string _dominantDesire  = "NONE";
    [ObservableProperty] private string _driveScores     = "";

    // ── Mood / Plutchik Emotions ──────────────────────────────────────────────
    [ObservableProperty] private bool   _hasMood          = false;
    [ObservableProperty] private float  _moodJoy          = 0f;
    [ObservableProperty] private float  _moodTrust        = 0f;
    [ObservableProperty] private float  _moodAnticipation = 0f;
    [ObservableProperty] private float  _moodAnger        = 0f;
    [ObservableProperty] private float  _moodSadness      = 0f;
    [ObservableProperty] private float  _moodDisgust      = 0f;
    [ObservableProperty] private float  _moodFear         = 0f;
    [ObservableProperty] private float  _moodSurprise     = 0f;
    [ObservableProperty] private string _moodValence      = "+0.0";
    [ObservableProperty] private string _activeEmotionTags = "";
    [ObservableProperty] private bool   _hasActiveEmotions = false;

    // ── GI Pipeline (v0.7.3+) ─────────────────────────────────────────────────
    // Fill values are 0–100 for ProgressBar binding.
    [ObservableProperty] private bool   _hasGiPipeline    = false;
    [ObservableProperty] private float  _siFill           = 0f;
    [ObservableProperty] private float  _liFill           = 0f;
    [ObservableProperty] private float  _colonFill        = 0f;
    [ObservableProperty] private string _siFillLabel      = "0%";
    [ObservableProperty] private string _liFillLabel      = "0%";
    [ObservableProperty] private string _colonFillLabel   = "0%";
    // Colon status — three mutually exclusive bools drive IsVisible for colour-coded text
    [ObservableProperty] private bool   _colonIsOk       = true;
    [ObservableProperty] private bool   _colonIsUrge     = false;
    [ObservableProperty] private bool   _colonIsCritical = false;

    // ── Bladder (v0.7.4+) ─────────────────────────────────────────────────────
    // Fill is 0–100 for ProgressBar binding.
    [ObservableProperty] private bool   _hasBladder        = false;
    [ObservableProperty] private float  _bladderFill       = 0f;
    [ObservableProperty] private string _bladderFillLabel  = "0%";
    [ObservableProperty] private bool   _bladderIsOk       = true;
    [ObservableProperty] private bool   _bladderIsUrge     = false;
    [ObservableProperty] private bool   _bladderIsCritical = false;

    // ── Esophagus Transit ─────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isInTransit     = false;
    [ObservableProperty] private float  _transitProgress = 0f;
    [ObservableProperty] private string _transitLabel    = "";

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pulls the latest component values off <paramref name="entity"/> and
    /// pushes them into the observable properties that the AXAML view binds
    /// to. Called every UI tick by <see cref="MainViewModel"/>.
    /// </summary>
    /// <param name="entity">The ECS entity whose state should drive this view model.</param>
    public void Update(Entity entity)
    {
        EntityId = entity.ShortId;
        Name     = entity.Has<IdentityComponent>()
            ? entity.Get<IdentityComponent>().Name
            : $"Entity {entity.ShortId}";

        // Tags
        var tags = new List<string>();
        if (entity.Has<HungerTag>())          tags.Add("HUNGRY");
        if (entity.Has<ThirstTag>())          tags.Add("THIRSTY");
        if (entity.Has<StarvingTag>())        tags.Add("STARVING");
        if (entity.Has<DehydratedTag>())      tags.Add("DEHYDRATED");
        if (entity.Has<TiredTag>())           tags.Add("TIRED");
        if (entity.Has<ExhaustedTag>())       tags.Add("EXHAUSTED");
        if (entity.Has<SleepingTag>())        tags.Add("SLEEPING");
        if (entity.Has<IrritableTag>())       tags.Add("IRRITABLE");
        if (entity.Has<BowelCriticalTag>())       tags.Add("BOWEL CRITICAL");
        else if (entity.Has<DefecationUrgeTag>()) tags.Add("DEFECATION URGE");
        if (entity.Has<BladderCriticalTag>())     tags.Add("BLADDER CRITICAL");
        else if (entity.Has<UrinationUrgeTag>())  tags.Add("URINATION URGE");
        ActiveTags    = tags.Count > 0 ? string.Join("  ·  ", tags) : "";
        HasActiveTags = tags.Count > 0;

        // Metabolism — resources and derived sensations
        HasMetabolism = entity.Has<MetabolismComponent>();
        if (HasMetabolism)
        {
            var meta = entity.Get<MetabolismComponent>();

            Satiation      = meta.Satiation;
            Hydration      = meta.Hydration;
            SatiationLabel = $"{meta.Satiation:F1}%";
            HydrationLabel = $"{meta.Hydration:F1}%";

            BodyTemp      = meta.BodyTemp;
            BodyTempLabel = $"{meta.BodyTemp:F1}°C";
            BodyTempTier  = meta.BodyTemp < 35f   ? 0   // hypothermia
                          : meta.BodyTemp < 37.6f ? 1   // normal
                          : meta.BodyTemp < 39f   ? 2   // fever
                                                  : 3;  // hyperthermia

            HungerLabel = meta.Hunger < 5f
                ? "Satisfied"
                : $"Hunger  {meta.Hunger:F1}%";

            ThirstLabel = meta.Thirst < 5f
                ? "Hydrated"
                : $"Thirst  {meta.Thirst:F1}%";

            // Body nutrient stores — pooled across the run
            var store       = meta.NutrientStores;
            HasNutrients    = !store.IsEmpty;
            NutrientCalories      = store.Calories;
            NutrientCaloriesLabel = $"{store.Calories:F0} kcal";
            NutrientMacrosLabel   = $"Carbs {store.Carbohydrates:F1}g · Prot {store.Proteins:F1}g · Fat {store.Fats:F1}g · Fiber {store.Fiber:F1}g";
            NutrientWaterLabel    = $"Water  {store.Water:F0} ml";
            NutrientVitaminsLabel = $"A {store.VitaminA:F1}  B {store.VitaminB:F1}  C {store.VitaminC:F1}  D {store.VitaminD:F1}  E {store.VitaminE:F1}  K {store.VitaminK:F1}  (mg)";
            NutrientMineralsLabel = $"Na {store.Sodium:F0}  K {store.Potassium:F0}  Ca {store.Calcium:F0}  Fe {store.Iron:F1}  Mg {store.Magnesium:F0}  (mg)";
        }

        // Stomach
        HasStomach = entity.Has<StomachComponent>();
        if (HasStomach)
        {
            var stomach    = entity.Get<StomachComponent>();
            var queued     = stomach.NutrientsQueued;
            StomachFill    = stomach.Fill * 100f;
            StomachLabel   = $"{stomach.Fill:P0}  ({stomach.CurrentVolumeMl:F0} / {StomachComponent.MaxVolumeMl:F0} ml)";
            DigestionLabel = $"Queued — {queued.Calories:F0} kcal  ·  water {queued.Water:F0}ml  ·  carbs {queued.Carbohydrates:F1}g  ·  prot {queued.Proteins:F1}g  ·  fat {queued.Fats:F1}g";
        }

        // GI Pipeline — SmallIntestine → LargeIntestine → Colon
        HasGiPipeline = entity.Has<SmallIntestineComponent>()
                     && entity.Has<LargeIntestineComponent>()
                     && entity.Has<ColonComponent>();
        if (HasGiPipeline)
        {
            var si    = entity.Get<SmallIntestineComponent>();
            var li    = entity.Get<LargeIntestineComponent>();
            var colon = entity.Get<ColonComponent>();

            SiFill       = si.Fill    * 100f;
            LiFill       = li.Fill    * 100f;
            ColonFill    = colon.Fill * 100f;
            SiFillLabel  = $"{si.Fill:P0}";
            LiFillLabel  = $"{li.Fill:P0}";
            ColonFillLabel = $"{colon.Fill:P0}";

            ColonIsCritical = colon.IsCritical;
            ColonIsUrge     = colon.HasUrge && !colon.IsCritical;
            ColonIsOk       = !colon.HasUrge;
        }

        // Energy / Sleep
        HasEnergy = entity.Has<EnergyComponent>();
        if (HasEnergy)
        {
            var en = entity.Get<EnergyComponent>();

            Energy          = en.Energy;
            Sleepiness      = en.Sleepiness;
            EnergyLabel     = $"{en.Energy:F1}%";
            SleepinessLabel = $"{en.Sleepiness:F1}%";
            IsSleeping      = en.IsSleeping;

            SleepStateLabel = en.IsSleeping ? "Sleeping"
                : en.Energy > 70f            ? "Alert"
                : en.Energy > 40f            ? "Tired"
                                             : "Exhausted";
        }

        // Brain — dominant desire and urgency scores
        HasDrives = entity.Has<DriveComponent>();
        if (HasDrives)
        {
            var d = entity.Get<DriveComponent>();
            DominantDesire = d.Dominant.ToString().ToUpperInvariant();
            DriveScores    = $"eat {d.EatUrgency:F2}  ·  drink {d.DrinkUrgency:F2}  ·  sleep {d.SleepUrgency:F2}  ·  poop {d.DefecateUrgency:F2}  ·  pee {d.PeeUrgency:F2}";
        }

        // Mood / Plutchik emotions
        HasMood = entity.Has<MoodComponent>();
        if (HasMood)
        {
            var mood = entity.Get<MoodComponent>();

            MoodJoy          = mood.Joy;
            MoodTrust        = mood.Trust;
            MoodAnticipation = mood.Anticipation;
            MoodAnger        = mood.Anger;
            MoodSadness      = mood.Sadness;
            MoodDisgust      = mood.Disgust;
            MoodFear         = mood.Fear;
            MoodSurprise     = mood.Surprise;

            float valence = mood.Valence;
            MoodValence = valence >= 0 ? $"+{valence:F1}" : $"{valence:F1}";

            var emotionTags = new List<string>();
            // Joy family
            if (entity.Has<EcstaticTag>())          emotionTags.Add("ECSTATIC");
            else if (entity.Has<JoyfulTag>())        emotionTags.Add("Joyful");
            else if (entity.Has<SereneTag>())        emotionTags.Add("Serene");
            // Disgust/Boredom family
            if (entity.Has<LoathingTag>())           emotionTags.Add("LOATHING");
            else if (entity.Has<DisgustTag>())       emotionTags.Add("Disgusted");
            else if (entity.Has<BoredTag>())         emotionTags.Add("Bored");
            // Anger family
            if (entity.Has<RagingTag>())             emotionTags.Add("RAGING");
            else if (entity.Has<AngryTag>())         emotionTags.Add("Angry");
            else if (entity.Has<AnnoyedTag>())       emotionTags.Add("Annoyed");
            // Sadness family
            if (entity.Has<GriefTag>())              emotionTags.Add("GRIEF");
            else if (entity.Has<SadTag>())           emotionTags.Add("Sad");
            else if (entity.Has<PensiveTag>())       emotionTags.Add("Pensive");
            // Anticipation family
            if (entity.Has<VigilantTag>())           emotionTags.Add("Vigilant");
            else if (entity.Has<AnticipatingTag>())  emotionTags.Add("Anticipating");
            else if (entity.Has<InterestedTag>())    emotionTags.Add("Interested");
            // Fear family
            if (entity.Has<TerrorTag>())             emotionTags.Add("TERRIFIED");
            else if (entity.Has<FearfulTag>())       emotionTags.Add("Fearful");
            else if (entity.Has<ApprehensiveTag>())  emotionTags.Add("Apprehensive");
            // Surprise family
            if (entity.Has<AmazedTag>())             emotionTags.Add("Amazed");
            else if (entity.Has<SurprisedTag>())     emotionTags.Add("Surprised");
            else if (entity.Has<DistractedTag>())    emotionTags.Add("Distracted");
            // Trust family
            if (entity.Has<AdmiringTag>())           emotionTags.Add("Admiring");
            else if (entity.Has<TrustingTag>())      emotionTags.Add("Trusting");
            else if (entity.Has<AcceptingTag>())     emotionTags.Add("Accepting");

            ActiveEmotionTags  = emotionTags.Count > 0 ? string.Join("  ·  ", emotionTags) : "";
            HasActiveEmotions  = emotionTags.Count > 0;
        }

        // Bladder
        HasBladder = entity.Has<BladderComponent>();
        if (HasBladder)
        {
            var bladder      = entity.Get<BladderComponent>();
            BladderFill      = bladder.Fill * 100f;
            BladderFillLabel = $"{bladder.Fill:P0}";
            BladderIsCritical = bladder.IsCritical;
            BladderIsUrge    = bladder.HasUrge && !bladder.IsCritical;
            BladderIsOk      = !bladder.HasUrge;
        }

        // Esophagus transit
        IsInTransit = entity.Has<EsophagusTransitComponent>();
        if (IsInTransit)
        {
            var transit = entity.Get<EsophagusTransitComponent>();
            TransitProgress = transit.Progress * 100f;

            string content = entity.Has<LiquidComponent>()
                ? entity.Get<LiquidComponent>().LiquidType
                : entity.Has<BolusComponent>()
                    ? entity.Get<BolusComponent>().FoodType
                    : "Unknown";

            TransitLabel = $"{content}  —  {transit.Position}%";
        }
    }
}
