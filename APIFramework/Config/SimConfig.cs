using System.Text.Json;
using System.Text.Json.Serialization;
using APIFramework.Components;

namespace APIFramework.Config;

/// <summary>
/// Root configuration object. Load from SimConfig.json at the solution root.
/// All tuning values live here — nothing biologically or mechanically significant
/// should be hardcoded in system or component files.
/// </summary>
public class SimConfig
{
    public WorldConfig    World    { get; set; } = new();
    public EntitiesConfig Entities { get; set; } = new();
    public SystemsConfig  Systems  { get; set; } = new();

    // ── Loading ───────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas         = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        // REQUIRED: NutrientProfile is a struct of public fields (not properties).
        // System.Text.Json ignores public fields by default — without this flag
        // every NutrientProfile in SimConfig.json deserialises to all-zeros,
        // which caused the stomach-filling / starvation bug found in v0.7.2 testing.
        IncludeFields               = true,
    };

    /// <summary>
    /// Loads SimConfig.json, searching upward from the current directory.
    /// Falls back to compiled defaults if no file is found — the simulation
    /// will always run, but you won't be able to tune values without the file.
    /// </summary>
    public static SimConfig Load(string fileName = "SimConfig.json")
    {
        var path = FindConfig(fileName);

        if (path is null)
        {
            Console.WriteLine($"[SimConfig] '{fileName}' not found — using compiled defaults.");
            return new SimConfig();
        }

        try
        {
            var json   = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<SimConfig>(json, JsonOptions);
            Console.WriteLine($"[SimConfig] Loaded from '{path}'");
            return config ?? new SimConfig();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SimConfig] Failed to parse '{path}': {ex.Message} — using defaults.");
            return new SimConfig();
        }
    }

    /// <summary>Walks up from the CWD until SimConfig.json is found (max 6 levels).</summary>
    private static string? FindConfig(string fileName)
    {
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        return null;
    }
}

// ═════════════════════════════════════════════════════════════════════════════
//  ENTITY CONFIGS
// ═════════════════════════════════════════════════════════════════════════════

public class EntitiesConfig
{
    public EntityConfig Human { get; set; } = EntityConfig.DefaultHuman;
    public EntityConfig Cat   { get; set; } = EntityConfig.DefaultCat;
}

// ── World / clock ─────────────────────────────────────────────────────────────

public class WorldConfig
{
    /// <summary>
    /// How many game-seconds pass per real second (default 120 = 2 game-minutes/s).
    /// The user-facing time-scale slider in the GUI multiplies on top of this.
    /// </summary>
    public float DefaultTimeScale { get; set; } = 120f;
}

public class EntityConfig
{
    public MetabolismEntityConfig Metabolism { get; set; } = new();
    public StomachEntityConfig    Stomach    { get; set; } = new();
    public EnergyEntityConfig     Energy     { get; set; } = new();
    public MoodEntityConfig       Mood       { get; set; } = new();

    public static EntityConfig DefaultHuman => new()
    {
        Metabolism = new MetabolismEntityConfig
        {
            SatiationStart            = 90f,   // just had breakfast — not stuffed
            HydrationStart            = 90f,   // well hydrated — not brimming
            BodyTemp                  = 37.0f,
            SatiationDrainRate        = 0.002f, // hunger in ~5.6 game hours → ~3 meals/day
            HydrationDrainRate        = 0.004f, // thirst in ~2.1 game hours awake → ~7 drinks/day
            SleepMetabolismMultiplier = 0.10f   // 10% drain rate while sleeping
        },
        Stomach = new StomachEntityConfig { DigestionRate = 0.017f }, // ~100 ml/game-hour
        Energy  = new EnergyEntityConfig
        {
            EnergyStart          = 90f,     // well rested — just woke up
            SleepinessStart      = 5f,      // nearly zero after a full night's sleep
            EnergyDrainRate      = 0.001f,  // depletes ~58 pts over 16 game hours awake
            SleepinessGainRate   = 0.0012f, // builds ~69 pts over 16 game hours awake
            EnergyRestoreRate    = 0.003f,  // restores to 100% within 8 game hours asleep
            SleepinessDrainRate  = 0.002f   // 74% → 15% in ~8.3 hours (full night's sleep)
        }
    };

    public static EntityConfig DefaultCat => new()
    {
        Metabolism = new MetabolismEntityConfig
        {
            SatiationStart            = 100f,
            HydrationStart            = 100f,
            BodyTemp                  = 38.5f,
            SatiationDrainRate        = 0.004f,  // cats eat every ~7 game hours
            HydrationDrainRate        = 0.006f,
            SleepMetabolismMultiplier = 0.05f    // cats barely lose resources while sleeping
        },
        Stomach = new StomachEntityConfig { DigestionRate = 0.010f },
        Energy  = new EnergyEntityConfig
        {
            EnergyStart         = 75f,   // cats are perpetually in a semi-nap state
            SleepinessStart     = 30f,
            EnergyDrainRate     = 0.0006f,
            SleepinessGainRate  = 0.0008f,
            EnergyRestoreRate   = 0.003f,   // cats restore energy very quickly
            SleepinessDrainRate = 0.004f
        }
    };
}

public class MetabolismEntityConfig
{
    /// <summary>Starting satiation (0–100). 100 = fully fed.</summary>
    public float SatiationStart { get; set; } = 100f;

    /// <summary>Starting hydration (0–100). 100 = fully hydrated.</summary>
    public float HydrationStart { get; set; } = 100f;

    /// <summary>Resting body temperature in Celsius.</summary>
    public float BodyTemp { get; set; } = 37.0f;

    /// <summary>Satiation lost per second at TimeScale 1.0.</summary>
    public float SatiationDrainRate { get; set; } = 0.3f;

    /// <summary>Hydration lost per second at TimeScale 1.0.</summary>
    public float HydrationDrainRate { get; set; } = 0.5f;

    /// <summary>
    /// Fraction of drain rates applied while the entity is sleeping (SleepingTag present).
    /// 0.10 = 10% of awake rates — an 8-hour sleep costs ~10% hydration/satiation.
    /// </summary>
    public float SleepMetabolismMultiplier { get; set; } = 0.10f;
}

public class StomachEntityConfig
{
    /// <summary>ml of stomach content digested per game-second.</summary>
    public float DigestionRate { get; set; } = 0.017f;
}

public class EnergyEntityConfig
{
    /// <summary>Starting Energy (0–100). 85 = well rested.</summary>
    public float EnergyStart { get; set; } = 85f;

    /// <summary>Starting Sleepiness (0–100). 15 = just woke up.</summary>
    public float SleepinessStart { get; set; } = 15f;

    /// <summary>Energy lost per game-second while awake.</summary>
    public float EnergyDrainRate { get; set; } = 0.001f;

    /// <summary>Sleepiness gained per game-second while awake.</summary>
    public float SleepinessGainRate { get; set; } = 0.0012f;

    /// <summary>Energy restored per game-second while sleeping.</summary>
    public float EnergyRestoreRate { get; set; } = 0.002f;

    /// <summary>Sleepiness lost per game-second while sleeping.</summary>
    public float SleepinessDrainRate { get; set; } = 0.0026f;
}

// ═════════════════════════════════════════════════════════════════════════════
//  SYSTEM CONFIGS
// ═════════════════════════════════════════════════════════════════════════════

public class SystemsConfig
{
    public BiologicalConditionSystemConfig BiologicalCondition { get; set; } = new();
    public EnergySystemConfig              Energy              { get; set; } = new();
    public BrainSystemConfig               Brain               { get; set; } = new();
    public FeedingSystemConfig             Feeding             { get; set; } = new();
    public DrinkingSystemConfig            Drinking            { get; set; } = new();
    public DigestionSystemConfig           Digestion           { get; set; } = new();
    public SleepSystemConfig               Sleep               { get; set; } = new();
    public InteractionSystemConfig         Interaction         { get; set; } = new();
    public MoodSystemConfig                Mood                { get; set; } = new();
    public RotSystemConfig                 Rot                 { get; set; } = new();
}

// ── BiologicalConditionSystem ─────────────────────────────────────────────────

public class BiologicalConditionSystemConfig
{
    /// <summary>Thirst level (0–100) at which ThirstTag is applied.</summary>
    public float ThirstTagThreshold { get; set; } = 30f;

    /// <summary>Thirst level at which DehydratedTag is applied (severe).</summary>
    public float DehydratedTagThreshold { get; set; } = 70f;

    /// <summary>Hunger level at which HungerTag is applied.</summary>
    public float HungerTagThreshold { get; set; } = 30f;

    /// <summary>Hunger level at which StarvingTag is applied (severe).</summary>
    public float StarvingTagThreshold { get; set; } = 80f;

    /// <summary>Hunger OR Thirst level at which IrritableTag is applied.</summary>
    public float IrritableThreshold { get; set; } = 60f;
}

// ── BrainSystem ───────────────────────────────────────────────────────────────

public class BrainSystemConfig
{
    /// <summary>
    /// Score multiplier for the Eat drive. Final score = (Hunger/100) * EatMaxScore.
    /// Set to 1.0 to make eating a potential life-or-death priority.
    /// </summary>
    public float EatMaxScore { get; set; } = 1.0f;

    /// <summary>Score multiplier for the Drink drive.</summary>
    public float DrinkMaxScore { get; set; } = 1.0f;

    /// <summary>
    /// Score multiplier for the Sleep drive. Slightly below survival drives by default
    /// so that hunger/thirst override exhaustion in critical situations.
    /// </summary>
    public float SleepMaxScore { get; set; } = 0.9f;

    /// <summary>
    /// Flat urgency bonus added to ALL drives when the entity has BoredTag.
    /// Small enough not to override genuine drives; big enough to push one drive
    /// over MinUrgencyThreshold so the entity picks something to do.
    /// </summary>
    public float BoredUrgencyBonus { get; set; } = 0.04f;

    /// <summary>
    /// If all drive scores remain below this after mood modifiers, they are all
    /// zeroed — Dominant becomes None (the idle/boredom-accumulation state).
    /// </summary>
    public float MinUrgencyThreshold { get; set; } = 0.05f;

    /// <summary>Multiplier applied to all urgency scores when SadTag is present (mild suppression).</summary>
    public float SadnessUrgencyMult { get; set; } = 0.80f;

    /// <summary>Multiplier applied to all urgency scores when GriefTag is present (strong suppression).</summary>
    public float GriefUrgencyMult { get; set; } = 0.50f;
}

// ── FeedingSystem ─────────────────────────────────────────────────────────────

public class FeedingSystemConfig
{
    /// <summary>Hunger level (0–100) required before the brain considers eating.</summary>
    public float HungerThreshold { get; set; } = 40f;

    /// <summary>
    /// Max Calories already queued in the stomach before FeedingSystem stops spawning.
    /// Prevents over-eating when digestion hasn't caught up. Measured in kcal now —
    /// under the v0.7.0 digestion factors (0.3 sat/kcal), 240 kcal ≈ 72 satiation,
    /// matching the pre-v0.7 cap of 70 "nutrition points".
    /// </summary>
    public float NutritionQueueCap { get; set; } = 240f;

    /// <summary>Properties of the hardcoded banana food source (temporary until world food exists).</summary>
    public FoodItemConfig Banana { get; set; } = new();

    /// <summary>
    /// Game-seconds of freshness before a spawned banana starts decaying.
    /// At TimeScale 120, 3600 game-seconds = 1 game-hour of freshness.
    /// A conjured banana goes straight into transit so this mainly matters
    /// for food entities placed in the world ahead of time.
    /// </summary>
    public float FoodFreshnessSeconds { get; set; } = 86400f; // 1 game-day

    /// <summary>RotLevel gained per game-second once a food entity passes its freshness window.</summary>
    public float FoodRotRate { get; set; } = 0.001f; // 100% rotten after ~27.8 game-hours of decay
}

public class FoodItemConfig
{
    /// <summary>Physical volume that enters the stomach (ml).</summary>
    public float VolumeMl { get; set; } = 50f;

    /// <summary>
    /// Full nutritional breakdown released to the stomach when the bolus arrives.
    /// DigestionSystem uses the Atwater-derived Calories and Water fields to
    /// compute gameplay Satiation/Hydration increments as contents absorb.
    /// </summary>
    public NutrientProfile Nutrients { get; set; } = new()
    {
        // Sensible banana defaults (real-world averages for a medium banana ≈ 118 g).
        Carbohydrates = 27f,
        Proteins      = 1.3f,
        Fats          = 0.4f,
        Fiber         = 3.1f,
        Water         = 89f,    // ml — most of a banana's mass is water
        VitaminB      = 0.4f,   // mg (primarily B6)
        VitaminC      = 10f,
        Potassium     = 422f,
        Magnesium     = 32f,
    };

    /// <summary>Chew resistance 0.0 (soft) → 1.0 (very tough). Affects bite time.</summary>
    public float Toughness { get; set; } = 0.2f;

    /// <summary>Speed at which this bolus travels down the esophagus (progress/second).</summary>
    public float EsophagusSpeed { get; set; } = 0.3f;
}

// ── DrinkingSystem ────────────────────────────────────────────────────────────

public class DrinkingSystemConfig
{
    /// <summary>
    /// Maximum ml of water already queued in the stomach before the system stops
    /// spawning new gulps. Prevents machine-gun gulping under normal thirst.
    /// v0.7.0+: measured in ml (matches NutrientProfile.Water). Default 15 ml =
    /// one gulp in flight at a time, matching the pre-v0.7 cap of 30 hydration pts.
    /// </summary>
    public float HydrationQueueCap { get; set; } = 15f;

    /// <summary>
    /// Raised queue cap when the entity is severely dehydrated (DehydratedTag).
    /// v0.7.0+: measured in ml. 30 ml ≈ two gulps in flight — matches pre-v0.7 60.
    /// </summary>
    public float HydrationQueueCapDehydrated { get; set; } = 30f;

    /// <summary>Properties of the hardcoded water source (temporary until world water exists).</summary>
    public DrinkItemConfig Water { get; set; } = new();
}

public class DrinkItemConfig
{
    /// <summary>Physical volume that enters the stomach per gulp (ml).</summary>
    public float VolumeMl { get; set; } = 15f;

    /// <summary>
    /// Full nutritional breakdown released to the stomach when the gulp arrives.
    /// Pure water by default (15 ml water per gulp, no macros or minerals).
    /// Future drinks (milk, juice, coffee) layer macros/vitamins on top of Water.
    /// </summary>
    public NutrientProfile Nutrients { get; set; } = new()
    {
        Water = 15f,   // ml — a single gulp
    };

    /// <summary>Speed at which liquid travels down the esophagus (progress/second).</summary>
    public float EsophagusSpeed { get; set; } = 0.8f;
}

// ── DigestionSystem ───────────────────────────────────────────────────────────

/// <summary>
/// Conversion factors from real-biology nutrients (NutrientProfile) to gameplay
/// metrics (Satiation / Hydration). Keeps the 0-100 HUD fields tuned to the
/// existing feel while the underlying NutrientStores accumulate real grams/ml.
/// </summary>
public class DigestionSystemConfig
{
    /// <summary>
    /// Satiation points per absorbed kcal. A medium banana (~117 kcal) at 0.3
    /// yields ~35 satiation — matching the pre-v0.7 flat NutritionValue.
    /// </summary>
    public float SatiationPerCalorie { get; set; } = 0.3f;

    /// <summary>
    /// Hydration points per absorbed ml of water. A 15 ml gulp at 2.0 yields
    /// 30 hydration — matching the pre-v0.7 flat HydrationValue.
    /// </summary>
    public float HydrationPerMl { get; set; } = 2.0f;
}

// ── EnergySystem ─────────────────────────────────────────────────────────────

public class EnergySystemConfig
{
    /// <summary>Energy threshold (0–100) below which TiredTag is applied.</summary>
    public float TiredThreshold { get; set; } = 60f;

    /// <summary>Energy threshold below which ExhaustedTag is applied (severe).</summary>
    public float ExhaustedThreshold { get; set; } = 25f;
}

// ── SleepSystem ───────────────────────────────────────────────────────────────

public class SleepSystemConfig
{
    /// <summary>
    /// Sleepiness level below which the entity can wake up (if brain also agrees).
    /// Prevents immediately waking back up right after falling asleep.
    /// </summary>
    public float WakeThreshold { get; set; } = 20f;
}

// ── InteractionSystem ─────────────────────────────────────────────────────────

public class InteractionSystemConfig
{
    /// <summary>Volume of a single bite bolus sent into the esophagus (ml).</summary>
    public float BiteVolumeMl { get; set; } = 50f;

    /// <summary>Speed of a bite bolus travelling down the esophagus (progress/second).</summary>
    public float EsophagusSpeed { get; set; } = 0.3f;
}

// ── MoodSystem ────────────────────────────────────────────────────────────────

/// <summary>
/// Starting values for MoodComponent. All emotions begin near zero for an average
/// rested, fed, non-threatened entity. Adjust per-entity archetype as needed.
/// </summary>
public class MoodEntityConfig
{
    // Starting intensities (0–100). Default: emotionally neutral.
    public float JoyStart          { get; set; } = 0f;
    public float TrustStart        { get; set; } = 0f;
    public float FearStart         { get; set; } = 0f;
    public float SurpriseStart     { get; set; } = 0f;
    public float SadnessStart      { get; set; } = 0f;
    public float DisgustStart      { get; set; } = 0f;
    public float AngerStart        { get; set; } = 0f;
    public float AnticipationStart { get; set; } = 0f;
}

/// <summary>
/// Thresholds at which MoodSystem promotes an emotion to the next intensity tag.
/// Follows Plutchik's three-tier model: low → medium → high.
/// </summary>
public class MoodSystemConfig
{
    /// <summary>Emotion value above which the low-intensity tag is applied (serenity, boredom, etc.).</summary>
    public float LowThreshold  { get; set; } = 10f;

    /// <summary>Emotion value above which the primary tag is applied (joy, disgust, anger, etc.).</summary>
    public float MidThreshold  { get; set; } = 34f;

    /// <summary>Emotion value above which the high-intensity tag is applied (ecstasy, loathing, rage, etc.).</summary>
    public float HighThreshold { get; set; } = 67f;

    // ── Decay rates — emotions fade over time if not sustained by inputs ─────

    /// <summary>Rate at which positive emotions (Joy, Trust, Anticipation) decay per game-second.</summary>
    public float PositiveDecayRate { get; set; } = 0.005f;

    /// <summary>Rate at which negative emotions (Fear, Sadness, Disgust, Anger) decay per game-second.</summary>
    public float NegativeDecayRate { get; set; } = 0.003f;

    /// <summary>Rate at which Surprise decays (fastest — surprise is brief by nature).</summary>
    public float SurpriseDecayRate { get; set; } = 0.05f;

    // ── Gain rates — how fast each emotion accumulates from its inputs ────────

    /// <summary>Joy gained per game-second while needs are comfortably met (all resources above JoyComfortThreshold).</summary>
    public float JoyGainRate { get; set; } = 0.01f;

    /// <summary>Resource level (0–100) all three must exceed (satiation, hydration, energy) for Joy to accumulate.</summary>
    public float JoyComfortThreshold { get; set; } = 60f;

    /// <summary>Anger gained per game-second while IrritableTag is present.</summary>
    public float AngerGainRate { get; set; } = 0.015f;

    /// <summary>Sadness gained per game-second while HungerTag or ThirstTag is present.</summary>
    public float SadnessGainRate { get; set; } = 0.008f;

    /// <summary>Disgust (boredom) gained per game-second while Dominant == None (idle state).</summary>
    public float BoredGainRate { get; set; } = 0.005f;

    /// <summary>Instant Disgust added when the entity consumes rotten food (ConsumedRottenFoodTag).</summary>
    public float RottenFoodDisgustSpike { get; set; } = 40f;

    /// <summary>Anticipation gained per game-second while hunger/thirst is building (between Min and Max).</summary>
    public float AnticipationGainRate { get; set; } = 0.006f;

    /// <summary>Hunger/Thirst value above which anticipation starts accumulating (drive is building).</summary>
    public float AnticipationHungerMin { get; set; } = 15f;

    /// <summary>Hunger/Thirst value below which anticipation stops (drive is dominant — no longer anticipating).</summary>
    public float AnticipationHungerMax { get; set; } = 50f;
}

// ── RotSystem ─────────────────────────────────────────────────────────────────

public class RotSystemConfig
{
    /// <summary>
    /// RotLevel (0–100) at which RotTag is applied to a food entity.
    /// FeedingSystem checks RotTag before Billy eats; if set, ConsumedRottenFoodTag is applied.
    /// </summary>
    public float RotTagThreshold { get; set; } = 30f;
}
