using System.Collections.Generic;
using Newtonsoft.Json;
using APIFramework.Components;
using APIFramework.Systems.Coupling;

namespace APIFramework.Config;

/// <summary>
/// Root configuration object. Load from SimConfig.json at the solution root.
/// All tuning values live here — nothing biologically or mechanically significant
/// should be hardcoded in system or component files.
/// </summary>
public class SimConfig
{
    /// <summary>World/clock-level tuning (time scale, day length).</summary>
    public WorldConfig            World          { get; set; } = new();
    /// <summary>Per-entity-archetype defaults (Human, Cat).</summary>
    public EntitiesConfig         Entities       { get; set; } = new();
    /// <summary>Per-system tuning bundle (biology, brain, feeding, sleep, etc.).</summary>
    public SystemsConfig          Systems        { get; set; } = new();
    /// <summary>Social drive dynamics (decay, circadian amplitudes/phases, willpower regen).</summary>
    public SocialSystemConfig     Social         { get; set; } = new();
    /// <summary>Spatial-index grid sizing and proximity defaults.</summary>
    public SpatialConfig          Spatial        { get; set; } = new();
    /// <summary>Lighting/illumination behaviour (flicker, decay, day phase boundaries).</summary>
    public LightingConfig         Lighting       { get; set; } = new();
    /// <summary>Movement tuning (step-aside, jitter, speed modifiers, pathfinding).</summary>
    public MovementConfig         Movement       { get; set; } = new();
    /// <summary>Narrative-event detection thresholds and windows.</summary>
    public NarrativeConfig        Narrative      { get; set; } = new();
    /// <summary>Cast-generator sampling ranges and seeded relationship counts.</summary>
    public CastGeneratorConfig    CastGenerator  { get; set; } = new();
    /// <summary>Chronicle persistence rules and magnitude ranges.</summary>
    public ChronicleConfig        Chronicle      { get; set; } = new();
    /// <summary>Dialog system tuning (calcification, valence buckets, recency).</summary>
    public DialogConfig           Dialog         { get; set; } = new();
    /// <summary>Action-selection drive/inhibition thresholds.</summary>
    public ActionSelectionConfig  ActionSelection { get; set; } = new();
    /// <summary>Stress system tuning (gain rates, decay, tag thresholds).</summary>
    public StressConfig           Stress          { get; set; } = new();
    /// <summary>Schedule/anchor weights for routine activities.</summary>
    public ScheduleConfig         Schedule        { get; set; } = new();
    /// <summary>Per-NPC memory bounds.</summary>
    public MemoryConfig           Memory          { get; set; } = new();
    /// <summary>Workload/task-generation rates and priorities.</summary>
    public WorkloadConfig         Workload        { get; set; } = new();
    /// <summary>Social-mask gain/decay and crack thresholds.</summary>
    public SocialMaskConfig       SocialMask      { get; set; } = new();
    /// <summary>Physiology-gate veto strength and willpower leakage.</summary>
    public PhysiologyGateConfig   PhysiologyGate  { get; set; } = new();
    /// <summary>Life-state transition timing (incapacitation budgets, death side-effects).</summary>
    public LifeStateConfig        LifeState       { get; set; } = new();
    /// <summary>Choking scenario tuning (bolus toughness, distraction thresholds).</summary>
    public ChokingConfig          Choking         { get; set; } = new();
    /// <summary>Bereavement scenario tuning (witness/colleague stress, grief intensity).</summary>
    public BereavementConfig      Bereavement     { get; set; } = new();
    /// <summary>Corpse-entity behaviour after death (currently a stub).</summary>
    public CorpseConfig           Corpse          { get; set; } = new();
    /// <summary>Fainting scenario tuning (fear threshold, recovery duration).</summary>
    public FaintingConfig         Fainting        { get; set; } = new();

    // ── Loading ───────────────────────────────────────────────────────────────

    // Newtonsoft.Json settings:
    //   MissingMemberHandling.Ignore  — unknown JSON keys are silently skipped
    //   NullValueHandling.Ignore      — missing JSON keys keep their C# defaults
    //
    // NOTE: Newtonsoft.Json serialises public fields on structs (like NutrientProfile)
    // by default, so no extra flag is needed (unlike System.Text.Json which required
    // IncludeFields = true).
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling     = NullValueHandling.Ignore,
    };

    /// <summary>
    /// Loads SimConfig.json, searching upward from the current directory.
    /// Falls back to compiled defaults if no file is found — the simulation
    /// will always run, but you won't be able to tune values without the file.
    /// </summary>
    /// <param name="fileName">Filename to search for (defaults to <c>SimConfig.json</c>).</param>
    /// <returns>The loaded <see cref="SimConfig"/>, or a default-initialised one on miss/parse failure.</returns>
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
            var config = JsonConvert.DeserializeObject<SimConfig>(json, JsonSettings);
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

/// <summary>Per-archetype default <see cref="EntityConfig"/> bundles selected at spawn time.</summary>
public class EntitiesConfig
{
    /// <summary>Default physiological tuning for human entities.</summary>
    public EntityConfig Human { get; set; } = EntityConfig.DefaultHuman;
    /// <summary>Default physiological tuning for cat entities.</summary>
    public EntityConfig Cat   { get; set; } = EntityConfig.DefaultCat;
}

// ── World / clock ─────────────────────────────────────────────────────────────

/// <summary>World-level tuning (clock and time scale).</summary>
public class WorldConfig
{
    /// <summary>
    /// How many game-seconds pass per real second (default 120 = 2 game-minutes/s).
    /// The user-facing time-scale slider in the GUI multiplies on top of this.
    /// </summary>
    public float DefaultTimeScale { get; set; } = 120f;
}

/// <summary>Per-entity-archetype physiology bundle (metabolism, energy, GI tract, bladder, mood starting values).</summary>
public class EntityConfig
{
    /// <summary>Metabolism (satiation/hydration/body-temp drain) configuration.</summary>
    public MetabolismEntityConfig     Metabolism     { get; set; } = new();
    /// <summary>Stomach digestion-rate configuration.</summary>
    public StomachEntityConfig        Stomach        { get; set; } = new();
    /// <summary>Energy/sleepiness drain and restore configuration.</summary>
    public EnergyEntityConfig         Energy         { get; set; } = new();
    /// <summary>Starting mood-component values (Plutchik emotions).</summary>
    public MoodEntityConfig           Mood           { get; set; } = new();
    /// <summary>Small-intestine absorption configuration.</summary>
    public SmallIntestineEntityConfig SmallIntestine { get; set; } = new();
    /// <summary>Large-intestine reabsorption/mobility configuration.</summary>
    public LargeIntestineEntityConfig LargeIntestine { get; set; } = new();
    /// <summary>Colon urge/capacity thresholds.</summary>
    public ColonEntityConfig          Colon          { get; set; } = new();
    /// <summary>Bladder fill-rate and capacity thresholds.</summary>
    public BladderEntityConfig        Bladder        { get; set; } = new();

    /// <summary>Default human physiology bundle (3-meals/day satiation, 7-drinks/day hydration, etc.).</summary>
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
        },
        SmallIntestine = new SmallIntestineEntityConfig
        {
            // 25 ml residue clears in ~52 game-minutes — realistic SI transit time
            AbsorptionRate         = 0.008f,
            ResidueToLargeFraction = 0.4f   // 40% of SI volume becomes LI waste
        },
        LargeIntestine = new LargeIntestineEntityConfig
        {
            WaterReabsorptionRate = 0.001f, // slow, persistent secondary hydration
            MobilityRate          = 0.003f, // ~10.8 ml/game-hour toward the colon
            StoolFraction         = 0.6f    // 60% of LI content forms formed stool
        },
        Colon = new ColonEntityConfig
        {
            UrgeThresholdMl = 100f,   // urge at ~50% fill — comfortable awareness
            CapacityMl      = 200f    // critical at capacity — must act now
        },
        Bladder = new BladderEntityConfig
        {
            // FillRate 0.010 ml/game-second → urge threshold (70ml) in ~2 game-hours.
            // At TimeScale 120 this gives ~6–8 urination events per awake game-day.
            FillRate        = 0.010f,
            UrgeThresholdMl = 70f,
            CapacityMl      = 100f
        }
    };

    /// <summary>Default cat physiology bundle — slower drains, higher body temp, perpetual semi-nap energy state.</summary>
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
        },
        SmallIntestine = new SmallIntestineEntityConfig
        {
            AbsorptionRate         = 0.010f,  // cats have faster SI transit
            ResidueToLargeFraction = 0.35f
        },
        LargeIntestine = new LargeIntestineEntityConfig
        {
            WaterReabsorptionRate = 0.0008f,
            MobilityRate          = 0.004f,
            StoolFraction         = 0.55f
        },
        Colon = new ColonEntityConfig
        {
            UrgeThresholdMl = 80f,
            CapacityMl      = 160f
        },
        Bladder = new BladderEntityConfig
        {
            // Cats urinate less frequently than humans — roughly every 4–6 game-hours.
            FillRate        = 0.004f,
            UrgeThresholdMl = 40f,
            CapacityMl      = 60f
        }
    };
}

/// <summary>Per-entity metabolism tuning — drain rates and starting values for satiation, hydration, and body temp.</summary>
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

/// <summary>Per-entity stomach tuning (digestion rate).</summary>
public class StomachEntityConfig
{
    /// <summary>ml of stomach content digested per game-second.</summary>
    public float DigestionRate { get; set; } = 0.017f;
}

/// <summary>Per-entity small-intestine tuning — absorption rate and fraction passed to the large intestine.</summary>
public class SmallIntestineEntityConfig
{
    /// <summary>ml of chyme processed (absorbed) per game-second in the small intestine.</summary>
    public float AbsorptionRate { get; set; } = 0.008f;

    /// <summary>
    /// Fraction of processed SI volume transferred to the large intestine as residue.
    /// 0.4 → 40% becomes LI waste; 60% is considered absorbed (water + nutrients).
    /// </summary>
    public float ResidueToLargeFraction { get; set; } = 0.4f;
}

/// <summary>Per-entity large-intestine tuning — water reabsorption, mobility, and stool fraction.</summary>
public class LargeIntestineEntityConfig
{
    /// <summary>ml of water recovered from LI content per game-second → Hydration.</summary>
    public float WaterReabsorptionRate { get; set; } = 0.001f;

    /// <summary>ml of LI content advanced toward the colon per game-second.</summary>
    public float MobilityRate { get; set; } = 0.003f;

    /// <summary>
    /// Fraction of processed LI volume that becomes formed stool in ColonComponent.
    /// The remainder (1 - StoolFraction) dissipates as water/gas.
    /// </summary>
    public float StoolFraction { get; set; } = 0.6f;
}

/// <summary>Per-entity colon tuning — defecation urge and capacity thresholds, in ml of stool.</summary>
public class ColonEntityConfig
{
    /// <summary>StoolVolumeMl at which DefecationUrgeTag is applied.</summary>
    public float UrgeThresholdMl { get; set; } = 100f;

    /// <summary>Maximum stool volume before BowelCriticalTag is applied (overrides all drives).</summary>
    public float CapacityMl { get; set; } = 200f;
}

/// <summary>Per-entity bladder tuning — fill rate, urination urge, and capacity thresholds (all in ml).</summary>
public class BladderEntityConfig
{
    /// <summary>ml of urine produced per game-second. At TimeScale 120, 0.010 ml/sec → urge in ~2 game-hours.</summary>
    public float FillRate { get; set; } = 0.010f;

    /// <summary>VolumeML at which UrinationUrgeTag is applied.</summary>
    public float UrgeThresholdMl { get; set; } = 70f;

    /// <summary>Maximum bladder volume before BladderCriticalTag is applied (overrides all drives).</summary>
    public float CapacityMl { get; set; } = 100f;
}

/// <summary>Per-entity energy/sleepiness tuning — starting values and per-second drain/restore rates.</summary>
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

/// <summary>Per-system tuning bundle — one nested config per simulation system.</summary>
public class SystemsConfig
{
    /// <summary>BiologicalConditionSystem thresholds for hunger/thirst/dehydration tags.</summary>
    public BiologicalConditionSystemConfig BiologicalCondition { get; set; } = new();
    /// <summary>EnergySystem thresholds for tired/exhausted tags.</summary>
    public EnergySystemConfig              Energy              { get; set; } = new();
    /// <summary>BrainSystem drive scoring multipliers and thresholds.</summary>
    public BrainSystemConfig               Brain               { get; set; } = new();
    /// <summary>FeedingSystem hunger threshold and food properties.</summary>
    public FeedingSystemConfig             Feeding             { get; set; } = new();
    /// <summary>DrinkingSystem hydration queue caps and water properties.</summary>
    public DrinkingSystemConfig            Drinking            { get; set; } = new();
    /// <summary>DigestionSystem nutrient-to-gameplay conversion factors.</summary>
    public DigestionSystemConfig           Digestion           { get; set; } = new();
    /// <summary>SleepSystem wake threshold.</summary>
    public SleepSystemConfig               Sleep               { get; set; } = new();
    /// <summary>InteractionSystem bite-volume and esophagus speed defaults.</summary>
    public InteractionSystemConfig         Interaction         { get; set; } = new();
    /// <summary>MoodSystem decay/gain rates and Plutchik-tier thresholds.</summary>
    public MoodSystemConfig                Mood                { get; set; } = new();
    /// <summary>RotSystem rot-tag threshold for food entities.</summary>
    public RotSystemConfig                 Rot                 { get; set; } = new();
    // Note: BladderFillSystem has no system-level config — all values live in BladderEntityConfig.
}

// ── BiologicalConditionSystem ─────────────────────────────────────────────────

/// <summary>Thresholds at which BiologicalConditionSystem promotes hunger/thirst values to tags.</summary>
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

/// <summary>BrainSystem drive-scoring multipliers, mood penalty multipliers, and the urgency floor below which all drives are zeroed.</summary>
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

    /// <summary>
    /// Score ceiling for the Defecate drive. Scored as ColonComponent.Fill * DefecateMaxScore.
    /// Slightly below survival drives (eat/drink = 1.0) by default — defecation is urgent
    /// but food and water remain higher priority unless BowelCriticalTag forces it to 1.0.
    /// </summary>
    public float DefecateMaxScore { get; set; } = 0.85f;

    /// <summary>
    /// Score ceiling for the Pee drive. Scored as BladderComponent.Fill * PeeMaxScore.
    /// Slightly below DefecateMaxScore — a full bladder is uncomfortable but less
    /// immediately dangerous than a full colon. BladderCriticalTag overrides to 1.0.
    /// </summary>
    public float PeeMaxScore { get; set; } = 0.80f;
}

// ── FeedingSystem ─────────────────────────────────────────────────────────────

/// <summary>FeedingSystem hunger threshold, queue caps, and the hardcoded banana food properties.</summary>
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

/// <summary>Physical and nutritional properties of a single food item (currently the hardcoded banana).</summary>
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

/// <summary>DrinkingSystem hydration queue caps (normal and dehydrated) and the hardcoded water properties.</summary>
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

/// <summary>Physical and nutritional properties of a single drink (currently the hardcoded gulp of water).</summary>
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

    /// <summary>
    /// Fraction of digested stomach volume transferred to SmallIntestineComponent as chyme.
    /// 0.0 = nothing passes to the intestine (pre-v0.7.3 behaviour).
    /// 0.2 = 20% of digested volume becomes chyme (fiber + unabsorbed matter).
    /// Ignored when the entity has no SmallIntestineComponent.
    /// </summary>
    public float ResidueFraction { get; set; } = 0.2f;
}

// ── EnergySystem ─────────────────────────────────────────────────────────────

/// <summary>EnergySystem thresholds at which the entity is tagged tired or exhausted.</summary>
public class EnergySystemConfig
{
    /// <summary>Energy threshold (0–100) below which TiredTag is applied.</summary>
    public float TiredThreshold { get; set; } = 60f;

    /// <summary>Energy threshold below which ExhaustedTag is applied (severe).</summary>
    public float ExhaustedThreshold { get; set; } = 25f;
}

// ── SleepSystem ───────────────────────────────────────────────────────────────

/// <summary>SleepSystem wake threshold — the sleepiness floor below which an entity may wake.</summary>
public class SleepSystemConfig
{
    /// <summary>
    /// Sleepiness level below which the entity can wake up (if brain also agrees).
    /// Prevents immediately waking back up right after falling asleep.
    /// </summary>
    public float WakeThreshold { get; set; } = 20f;
}

// ── InteractionSystem ─────────────────────────────────────────────────────────

/// <summary>InteractionSystem defaults for bite volume and esophagus speed when an entity bites a food source.</summary>
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
    /// <summary>Starting Joy intensity (0–100).</summary>
    public float JoyStart          { get; set; } = 0f;
    /// <summary>Starting Trust intensity (0–100).</summary>
    public float TrustStart        { get; set; } = 0f;
    /// <summary>Starting Fear intensity (0–100).</summary>
    public float FearStart         { get; set; } = 0f;
    /// <summary>Starting Surprise intensity (0–100).</summary>
    public float SurpriseStart     { get; set; } = 0f;
    /// <summary>Starting Sadness intensity (0–100).</summary>
    public float SadnessStart      { get; set; } = 0f;
    /// <summary>Starting Disgust intensity (0–100).</summary>
    public float DisgustStart      { get; set; } = 0f;
    /// <summary>Starting Anger intensity (0–100).</summary>
    public float AngerStart        { get; set; } = 0f;
    /// <summary>Starting Anticipation intensity (0–100).</summary>
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

/// <summary>RotSystem tuning — the level at which food entities receive RotTag.</summary>
public class RotSystemConfig
{
    /// <summary>
    /// RotLevel (0–100) at which RotTag is applied to a food entity.
    /// FeedingSystem checks RotTag before Billy eats; if set, ConsumedRottenFoodTag is applied.
    /// </summary>
    public float RotTagThreshold { get; set; } = 30f;
}

// ── Dialog system ─────────────────────────────────────────────────────────────

/// <summary>
/// Tuning knobs for the dialog phrase-selection and calcification pipeline.
/// All thresholds are on a 0–100 drive scale unless noted otherwise.
/// </summary>
public class DialogConfig
{
    /// <summary>Number of times a fragment must be selected before calcification is eligible.</summary>
    public int    CalcifyThreshold              { get; set; } = 8;

    /// <summary>Fraction of uses that must share the dominant context before calcification fires (0.0–1.0).</summary>
    public double CalcifyContextDominanceMin    { get; set; } = 0.70;

    /// <summary>Number of times a listener must hear a fragment from one speaker for tic recognition.</summary>
    public int    TicRecognitionThreshold       { get; set; } = 5;

    /// <summary>Game-seconds within which a prior use of the same fragment incurs RecencyPenalty.</summary>
    public int    RecencyWindowSeconds          { get; set; } = 300;

    /// <summary>Score added per drive key whose valence ordinal matches the speaker's current drive level.</summary>
    public int    ValenceMatchScore             { get; set; } = 5;

    /// <summary>Score penalty applied when the fragment was used within RecencyWindowSeconds.</summary>
    public int    RecencyPenalty                { get; set; } = -10;

    /// <summary>Bonus score added when a fragment is calcified for the speaker.</summary>
    public int    CalcifyBiasScore              { get; set; } = 3;

    /// <summary>Score added when the speaker has previously used this fragment with this specific listener.</summary>
    public int    PerListenerBiasScore          { get; set; } = 2;

    /// <summary>Upper bound of the "low" valence ordinal bucket (drive value 0–ValenceLowMaxValue maps to "low").</summary>
    public int    ValenceLowMaxValue            { get; set; } = 33;

    /// <summary>Upper bound of the "mid" valence ordinal bucket (ValenceLowMaxValue+1–ValenceMidMaxValue maps to "mid").</summary>
    public int    ValenceMidMaxValue            { get; set; } = 66;

    /// <summary>Game-days of disuse before a calcified fragment loses its calcified status.</summary>
    public int    DecalcifyTimeoutDays          { get; set; } = 30;

    /// <summary>Relative path to the corpus JSON file, searched from CWD upward.</summary>
    public string CorpusPath                    { get; set; } = "docs/c2-content/dialog/corpus-starter.json";

    /// <summary>Drive level (0–100) at which a drive is considered elevated for context selection.</summary>
    public int    DriveContextThreshold         { get; set; } = 60;

    /// <summary>Per-tick probability (0–1) that an in-range NPC pair attempts dialog.</summary>
    public double DialogAttemptProbability      { get; set; } = 0.05;
}

// ── Social systems ────────────────────────────────────────────────────────────

/// <summary>
/// Tuning for the social drive dynamics: per-tick decay toward baseline, circadian
/// modulation, willpower regeneration during sleep, and relationship intensity decay.
/// </summary>
public class SocialSystemConfig
{
    /// <summary>Points per tick a drive's Current moves toward its Baseline (linear approach).</summary>
    public double DriveDecayPerTick { get; set; } = 0.15;

    /// <summary>
    /// Peak circadian amplitude (points) for each drive.
    /// Keys are lowercase drive names: belonging, status, affection, irritation,
    /// attraction, trust, suspicion, loneliness.
    /// </summary>
    public Dictionary<string, double> DriveCircadianAmplitudes { get; set; } = new()
    {
        ["belonging"]  = 3.0,
        ["status"]     = 2.5,
        ["affection"]  = 3.0,
        ["irritation"] = 4.0,
        ["attraction"] = 2.0,
        ["trust"]      = 1.5,
        ["suspicion"]  = 2.0,
        ["loneliness"] = 5.0,
    };

    /// <summary>
    /// Fraction of the day (0..1) at which each drive peaks.
    /// Keys are lowercase drive names.
    /// </summary>
    public Dictionary<string, double> DriveCircadianPhases { get; set; } = new()
    {
        ["belonging"]  = 0.40,
        ["status"]     = 0.30,
        ["affection"]  = 0.65,
        ["irritation"] = 0.55,
        ["attraction"] = 0.70,
        ["trust"]      = 0.45,
        ["suspicion"]  = 0.80,
        ["loneliness"] = 0.85,
    };

    /// <summary>Global multiplier on the neuroticism-driven per-tick noise.</summary>
    public double DriveVolatilityScale { get; set; } = 1.0;

    /// <summary>Willpower points restored per tick while SleepingTag is present.</summary>
    public int WillpowerSleepRegenPerTick { get; set; } = 1;

    /// <summary>Intensity points lost per tick in the absence of proximity interaction signals.</summary>
    public double RelationshipIntensityDecayPerTick { get; set; } = 0.05;

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Returns the circadian amplitude for the given drive, or 0 if not configured.</summary>
    /// <param name="driveName">Lowercase drive name (e.g. "loneliness").</param>
    /// <returns>The amplitude in drive points, or 0 when no entry exists.</returns>
    public double GetCircadianAmplitude(string driveName)
        => DriveCircadianAmplitudes.TryGetValue(driveName, out var v) ? v : 0.0;

    /// <summary>Returns the circadian phase (fraction of the day at which the drive peaks) or 0 if not configured.</summary>
    /// <param name="driveName">Lowercase drive name.</param>
    /// <returns>The phase in the range 0..1, or 0 when no entry exists.</returns>
    public double GetCircadianPhase(string driveName)
        => DriveCircadianPhases.TryGetValue(driveName, out var v) ? v : 0.0;
}

// ── Movement systems ──────────────────────────────────────────────────────────

/// <summary>Movement-system tuning — step-aside, idle jitter, posture shifts, speed modifiers, and pathfinding.</summary>
public class MovementConfig
{
    /// <summary>Radius (tiles) within which two approaching NPCs trigger step-aside logic.</summary>
    public float StepAsideRadius { get; set; } = 3f;

    /// <summary>Perpendicular position shift (tiles) applied per step-aside event.</summary>
    public float StepAsideShift { get; set; } = 0.4f;

    /// <summary>Maximum random position jitter magnitude (tiles) applied to idle NPCs each tick.</summary>
    public float IdleJitterTiles { get; set; } = 0.05f;

    /// <summary>Per-tick probability [0,1] that an idle NPC performs a posture shift (changes facing).</summary>
    public float IdlePostureShiftProb { get; set; } = 0.005f;

    /// <summary>Per-drive multipliers that scale movement speed as a function of irritation, affection, and low energy.</summary>
    public MovementSpeedModifierConfig SpeedModifier { get; set; } = new();
    /// <summary>A* pathfinding tuning (doorway bias, tie-break noise).</summary>
    public MovementPathfindingConfig   Pathfinding   { get; set; } = new();
}

/// <summary>
/// Multiplicative speed modifier coefficients. Final multiplier is
/// <c>1 + IrritationGain·Irr - AffectionLoss·Aff - LowEnergyLoss·max(0,30-Energy)</c>,
/// clamped to <see cref="MinMultiplier"/>..<see cref="MaxMultiplier"/>.
/// </summary>
public class MovementSpeedModifierConfig
{
    /// <summary>Multiplier gain per point of Irritation drive.</summary>
    public float IrritationGainPerPoint { get; set; } = 0.005f;
    /// <summary>Multiplier loss per point of Affection drive.</summary>
    public float AffectionLossPerPoint  { get; set; } = 0.0033f;
    /// <summary>Multiplier loss per point of Energy below the low-energy threshold.</summary>
    public float LowEnergyLossPerPoint  { get; set; } = 0.005f;
    /// <summary>Lower clamp of the final speed multiplier.</summary>
    public float MinMultiplier          { get; set; } = 0.3f;
    /// <summary>Upper clamp of the final speed multiplier.</summary>
    public float MaxMultiplier          { get; set; } = 2.0f;
}

/// <summary>A* pathfinding tuning — doorway preference and tie-break noise.</summary>
public class MovementPathfindingConfig
{
    /// <summary>F-cost discount applied to tiles that are doorways between rooms.</summary>
    public float DoorwayDiscount    { get; set; } = 1.0f;

    /// <summary>Scale of seeded hash noise used to break A* f-cost ties (produces path variety).</summary>
    public float TieBreakNoiseScale { get; set; } = 0.1f;
}

// ── Spatial systems ───────────────────────────────────────────────────────────

/// <summary>Spatial-index tuning — grid cell size, world dimensions, and proximity range defaults.</summary>
public class SpatialConfig
{
    /// <summary>Side length of each spatial-index grid cell, in tiles. Default 4.</summary>
    public int CellSizeTiles { get; set; } = 4;

    /// <summary>Tile-space dimensions of the indexed world.</summary>
    public SpatialWorldSizeConfig       WorldSize              { get; set; } = new();
    /// <summary>Default conversation/awareness/sight ranges seeded onto NPCs at spawn.</summary>
    public ProximityRangeDefaultsConfig ProximityRangeDefaults { get; set; } = new();
}

/// <summary>Tile-space width and height of the indexed world.</summary>
public class SpatialWorldSizeConfig
{
    /// <summary>Horizontal tile extent of the indexed world. Default 512.</summary>
    public int Width  { get; set; } = 512;

    /// <summary>Vertical tile extent of the indexed world. Default 512.</summary>
    public int Height { get; set; } = 512;
}

/// <summary>Default proximity ranges seeded onto NPCs at spawn (overridden per NPC).</summary>
public class ProximityRangeDefaultsConfig
{
    /// <summary>Seed conversation range in tiles. Overridden per NPC at spawn. Default 2.</summary>
    public int ConversationTiles { get; set; } = 2;

    /// <summary>Seed awareness range in tiles. Default 8.</summary>
    public int AwarenessTiles    { get; set; } = 8;

    /// <summary>Seed sight range in tiles. Default 32.</summary>
    public int SightTiles        { get; set; } = 32;
}

// ── Lighting systems ──────────────────────────────────────────────────────────

/// <summary>LightingSystem tuning — flicker/decay probabilities, range defaults, and drive-coupling rules.</summary>
public class LightingConfig
{
    /// <summary>Boundaries between named day phases (early morning, mid morning, etc.) as fractions of the day.</summary>
    public DayPhaseBoundariesConfig DayPhaseBoundaries { get; set; } = new();

    /// <summary>Probability (0–1) that a Flickering source emits at full intensity on a given tick.</summary>
    public double FlickerOnProb     { get; set; } = 0.70;

    /// <summary>Probability (0–1) per tick that a Dying source loses 1 intensity point.</summary>
    public double DyingDecayProb    { get; set; } = 0.05;

    /// <summary>Tile radius within which a light aperture contributes to a room (for distance falloff).</summary>
    public int    ApertureRangeBase { get; set; } = 5;

    /// <summary>Tile radius within which a light source contributes to its room (for distance falloff).</summary>
    public int    SourceRangeBase   { get; set; } = 3;

    /// <summary>
    /// Ordered list of lighting-to-drive coupling entries. First-match-wins.
    /// Loaded from SimConfig.json "lighting.driveCouplings" array.
    /// </summary>
    public List<LightingCouplingEntry> DriveCouplings { get; set; } = new();
}

/// <summary>
/// Boundaries between named day phases. Each value is a fraction of the day (0..1)
/// at which the named phase begins. Phases are ordered: EarlyMorning &lt; MidMorning &lt;
/// Afternoon &lt; Evening &lt; Dusk &lt; Night.
/// </summary>
public class DayPhaseBoundariesConfig
{
    /// <summary>Fraction of day (0..1) at which Early Morning begins. Default 0.20 (≈ 4:48 AM).</summary>
    public double EarlyMorningStart { get; set; } = 0.20;
    /// <summary>Fraction of day at which Mid Morning begins. Default 0.30 (≈ 7:12 AM).</summary>
    public double MidMorningStart   { get; set; } = 0.30;
    /// <summary>Fraction of day at which Afternoon begins. Default 0.45 (≈ 10:48 AM).</summary>
    public double AfternoonStart    { get; set; } = 0.45;
    /// <summary>Fraction of day at which Evening begins. Default 0.65 (≈ 3:36 PM).</summary>
    public double EveningStart      { get; set; } = 0.65;
    /// <summary>Fraction of day at which Dusk begins. Default 0.80 (≈ 7:12 PM).</summary>
    public double DuskStart         { get; set; } = 0.80;
    /// <summary>Fraction of day at which Night begins. Default 0.85 (≈ 8:24 PM).</summary>
    public double NightStart        { get; set; } = 0.85;
}

// ── Narrative telemetry channel ───────────────────────────────────────────────

/// <summary>
/// Thresholds and window sizes for NarrativeEventDetector.
/// Defaults produce signal for human-authored content; tune lower for testing.
/// </summary>
public class NarrativeConfig
{
    /// <summary>
    /// Minimum absolute drive delta (Current this tick vs last tick) required to emit DriveSpike.
    /// Default 15 — a clearly visible movement in a drive value.
    /// </summary>
    public int DriveSpikeThreshold { get; set; } = 15;

    /// <summary>
    /// Minimum willpower drop in a single tick required to emit WillpowerCollapse.
    /// Default 10 — a sustained suppression releasing all at once.
    /// </summary>
    public int WillpowerDropThreshold { get; set; } = 10;

    /// <summary>
    /// Willpower level below which WillpowerLow is emitted (once per descent).
    /// Default 20 — the gate is visibly weakening.
    /// </summary>
    public int WillpowerLowThreshold { get; set; } = 20;

    /// <summary>
    /// Number of ticks after a DriveSpike during which a RoomMembershipChanged
    /// for the same NPC produces a LeftRoomAbruptly candidate.
    /// Default 3.
    /// </summary>
    public int AbruptDepartureWindowTicks { get; set; } = 3;

    /// <summary>Maximum character length of the Detail field on any candidate.</summary>
    public int CandidateDetailMaxLength { get; set; } = 280;
}

// ── Cast generator ─────────────────────────────────────────────────────────────

/// <summary>
/// CastGenerator tuning — baseline drive ranges (elevated/depressed/neutral), per-drive
/// jitter, and counts of seeded relationships per pattern.
/// </summary>
/// <seealso cref="APIFramework.Bootstrap.CastGenerator"/>
public class CastGeneratorConfig
{
    /// <summary>Baseline range [min,max] for chronically-elevated drives.</summary>
    public int[] ElevatedDriveRange   { get; set; } = new[] { 55, 75 };

    /// <summary>Baseline range [min,max] for chronically-depressed drives.</summary>
    public int[] DepressedDriveRange  { get; set; } = new[] { 25, 45 };

    /// <summary>Baseline range [min,max] for drives with no archetype elevation.</summary>
    public int[] NeutralDriveRange    { get; set; } = new[] { 40, 60 };

    /// <summary>Per-drive jitter applied to Current at spawn (additive, clamped 0–100).</summary>
    public int[] CurrentJitterRange   { get; set; } = new[] { -5, 5 };

    /// <summary>Number of rivalry relationships to seed across the cast.</summary>
    public int RivalryCount            { get; set; } = 2;

    /// <summary>Number of old-flame relationships to seed.</summary>
    public int OldFlameCount           { get; set; } = 1;

    /// <summary>Number of mentor/mentee pairs to seed.</summary>
    public int MentorPairCount         { get; set; } = 1;

    /// <summary>Number of slept-with-their-spouse relationships to seed.</summary>
    public int SleptWithSpouseCount    { get; set; } = 1;

    /// <summary>Number of friend pairs to seed.</summary>
    public int FriendPairCount         { get; set; } = 2;

    /// <summary>Number of "the thing nobody talks about" relationships to seed.</summary>
    public int ThingNobodyTalksAboutCount { get; set; } = 2;

    /// <summary>Intensity range [min,max] for seeded relationships.</summary>
    public int[] RelationshipIntensityRange { get; set; } = new[] { 30, 70 };
}

// ── Chronicle systems ─────────────────────────────────────────────────────────

/// <summary>Chronicle persistence tuning — max-entry caps, magnitude ranges, and stickiness rules.</summary>
public class ChronicleConfig
{
    /// <summary>Maximum number of chronicle entries before oldest are dropped.</summary>
    public int MaxEntries { get; set; } = 4096;

    /// <summary>Threshold rules used to decide whether candidate events stick to the chronicle.</summary>
    public ChronicleThresholdRulesConfig ThresholdRules { get; set; } = new();

    /// <summary>Stain magnitude range [min, max]. Default [10, 80].</summary>
    public int[] StainMagnitudeRange { get; set; } = new[] { 10, 80 };

    /// <summary>Broken-item magnitude range [min, max]. Default [20, 100].</summary>
    public int[] BrokenItemMagnitudeRange { get; set; } = new[] { 20, 100 };
}

/// <summary>Per-rule thresholds that decide whether narrative candidates qualify for chronicle persistence.</summary>
public class ChronicleThresholdRulesConfig
{
    /// <summary>Minimum relationship-intensity delta required to persist a relationship-changing candidate.</summary>
    public int IntensityChangeMinForRelationshipStick { get; set; } = 15;

    /// <summary>Minimum irritation drive value (after spike) required for physical manifest spawning.</summary>
    public int IrritationSpikeMinForPhysicalManifest { get; set; } = 70;

    /// <summary>
    /// Game-seconds window after a DriveSpike candidate during which "drive returned to baseline"
    /// is detected. Candidates still elevated after this window are eligible to persist.
    /// </summary>
    public double DriveReturnToBaselineWindowSeconds { get; set; } = 60;

    /// <summary>Minimum number of different NPCs referencing the same event kind in a tick to trigger the talk-about rule.</summary>
    public int TalkAboutMinReferenceCount { get; set; } = 2;
}

// ── Action-selection system ───────────────────────────────────────────────────

/// <summary>
/// ActionSelectionSystem tuning — drive enumeration thresholds, idle floor,
/// approach-avoidance inversion thresholds, and suppression behaviour.
/// </summary>
public class ActionSelectionConfig
{
    /// <summary>Drive.Current must exceed this to enumerate candidates. Default 60.</summary>
    public int    DriveCandidateThreshold       { get; set; } = 60;

    /// <summary>Idle wins when nothing else exceeds this floor. Default 0.20.</summary>
    public double IdleScoreFloor                { get; set; } = 0.20;

    /// <summary>stake above this enables approach-avoidance flip check. Default 0.55.</summary>
    public double InversionStakeThreshold       { get; set; } = 0.55;

    /// <summary>Matched inhibition above this (combined with stake) completes the flip. Default 0.50.</summary>
    public double InversionInhibitionThreshold  { get; set; } = 0.50;

    /// <summary>How much low-willpower leaks suppressed drive push through the gate. Default 0.30.</summary>
    public double SuppressionGiveUpFactor       { get; set; } = 0.30;

    /// <summary>Closeness (raw push vs winner weight) that marks a candidate as actively suppressed. Default 0.10.</summary>
    public double SuppressionEpsilon            { get; set; } = 0.10;

    /// <summary>Willpower cost scaling for suppression events. Default 5.</summary>
    public int    SuppressionEventMagnitudeScale { get; set; } = 5;

    /// <summary>Per-point personality nudge (Conscientiousness/Openness). Default 0.05.</summary>
    public double PersonalityTieBreakWeight     { get; set; } = 0.05;

    /// <summary>Hard cap on enumerated candidates per NPC per tick. Default 32.</summary>
    public int    MaxCandidatesPerTick          { get; set; } = 32;

    /// <summary>Tiles the avoidance flee target is pushed away from the threat. Default 4.</summary>
    public int    AvoidStandoffDistance         { get; set; } = 4;
}

// ── Stress system ─────────────────────────────────────────────────────────────

/// <summary>
/// StressSystem tuning — per-source acute stress gains, decay, tag promotion thresholds,
/// burnout cooldown, and the neuroticism multiplier on stress accumulation.
/// </summary>
public class StressConfig
{
    /// <summary>AcuteLevel gain per SuppressionTick event (before neuroticism scaling). Default 1.5.</summary>
    public double SuppressionStressGain         { get; set; } = 1.5;

    /// <summary>Drive.Current must exceed Drive.Baseline by this much to count as a spike. Default 25.</summary>
    public int    DriveSpikeStressDelta         { get; set; } = 25;

    /// <summary>AcuteLevel gain per drive spike per tick (before neuroticism scaling). Default 2.0.</summary>
    public double DriveSpikeStressGain          { get; set; } = 2.0;

    /// <summary>AcuteLevel gain per social conflict event (before neuroticism scaling). Default 3.0.</summary>
    public double SocialConflictStressGain      { get; set; } = 3.0;

    /// <summary>AcuteLevel lost per tick to natural decay. Fractional; accumulated internally. Default 0.05.</summary>
    public double AcuteDecayPerTick             { get; set; } = 0.05;

    /// <summary>AcuteLevel threshold above which StressedTag is applied. Default 60.</summary>
    public int    StressedTagThreshold          { get; set; } = 60;

    /// <summary>AcuteLevel threshold above which OverwhelmedTag is applied. Default 85.</summary>
    public int    OverwhelmedTagThreshold       { get; set; } = 85;

    /// <summary>ChronicLevel threshold above which BurningOutTag is applied. Default 70.</summary>
    public int    BurningOutTagThreshold        { get; set; } = 70;

    /// <summary>Days BurningOutTag remains after ChronicLevel drops below threshold. Default 3.</summary>
    public int    BurningOutCooldownDays        { get; set; } = 3;

    /// <summary>Base magnitude of the extra SuppressionTick pushed when AcuteLevel ≥ StressedTagThreshold. Default 1.0.</summary>
    public double StressAmplificationMagnitude  { get; set; } = 1.0;

    /// <summary>
    /// Fraction of AcuteLevel added to the volatility multiplier in DriveDynamicsSystem.
    /// 0.5 means max stress (100) doubles drive volatility. Default 0.5.
    /// </summary>
    public double StressVolatilityScale         { get; set; } = 0.5;

    /// <summary>
    /// Per point of Neuroticism, stress gain is multiplied by (1 + N × factor).
    /// Range: Neuroticism –2..+2 → factor 0.6..1.4 at default 0.2.
    /// </summary>
    public double NeuroticismStressFactor       { get; set; } = 0.2;
}

// ── Schedule system ───────────────────────────────────────────────────────────

/// <summary>ScheduleSystem tuning — anchor weights and the linger/approach distance threshold.</summary>
public class ScheduleConfig
{
    /// <summary>Weight assigned to a schedule-driven Approach/Linger candidate. Default 0.30.</summary>
    public double ScheduleAnchorBaseWeight     { get; set; } = 0.30;

    /// <summary>Distance (tiles) below which AtDesk/Sleeping activity emits Linger instead of Approach. Default 2.0.</summary>
    public float  ScheduleLingerThresholdCells { get; set; } = 2.0f;
}

// ── Workload system ───────────────────────────────────────────────────────────

/// <summary>
/// WorkloadSystem tuning — task generation cadence, effort/deadline/priority ranges,
/// progress rates, quality drift, and the work-action weight.
/// </summary>
public class WorkloadConfig
{
    /// <summary>Game-hour (0–24) at which TaskGeneratorSystem fires each day. Default 8.0 (8 AM).</summary>
    public double TaskGenerationHourOfDay      { get; set; } = 8.0;

    /// <summary>Tasks created per generation event. Default 5.</summary>
    public int    TaskGenerationCountPerDay    { get; set; } = 5;

    /// <summary>Minimum task effort in game-hours. Default 0.5.</summary>
    public float  TaskEffortHoursMin           { get; set; } = 0.5f;

    /// <summary>Maximum task effort in game-hours. Default 6.0.</summary>
    public float  TaskEffortHoursMax           { get; set; } = 6.0f;

    /// <summary>Minimum deadline offset from now in game-hours. Default 4.0.</summary>
    public float  TaskDeadlineHoursMin         { get; set; } = 4.0f;

    /// <summary>Maximum deadline offset from now in game-hours. Default 48.0.</summary>
    public float  TaskDeadlineHoursMax         { get; set; } = 48.0f;

    /// <summary>Minimum randomly assigned task priority. Default 30.</summary>
    public int    TaskPriorityMin              { get; set; } = 30;

    /// <summary>Maximum randomly assigned task priority. Default 80.</summary>
    public int    TaskPriorityMax              { get; set; } = 80;

    /// <summary>Base work-progress gained per game-second (before multipliers). Default 0.0001.</summary>
    public double BaseProgressRatePerSecond    { get; set; } = 0.0001;

    /// <summary>Per Conscientiousness point, progress rate is multiplied by (1 + C × bias). Default 0.10.</summary>
    public double ConscientiousnessProgressBias { get; set; } = 0.10;

    /// <summary>QualityLevel lost per tick when stressed or under poor physiological conditions. Default 0.0002.</summary>
    public double QualityDecayPerStressedTick  { get; set; } = 0.0002;

    /// <summary>QualityLevel recovered per tick under good physiological conditions. Default 0.0001.</summary>
    public double QualityRecoveryPerGoodTick   { get; set; } = 0.0001;

    /// <summary>Base weight of the Work candidate in ActionSelectionSystem. Default 0.40.</summary>
    public double WorkActionBaseWeight         { get; set; } = 0.40;

    /// <summary>Stress gain per overdue task per tick (before neuroticism scaling). Default 1.0.</summary>
    public double OverdueTaskStressGain        { get; set; } = 1.0;
}

// ── Memory system ─────────────────────────────────────────────────────────────

/// <summary>Per-NPC memory bounds — caps for relationship and personal memory ring buffers.</summary>
public class MemoryConfig
{
    /// <summary>Maximum entries in RelationshipMemoryComponent.Recent before oldest is dropped.</summary>
    public int MaxRelationshipMemoryCount { get; set; } = 32;

    /// <summary>Maximum entries in PersonalMemoryComponent.Recent before oldest is dropped.</summary>
    public int MaxPersonalMemoryCount { get; set; } = 16;
}

// ── Physiology gate system ────────────────────────────────────────────────────

/// <summary>
/// Tuning knobs for PhysiologyGateSystem — the layer that lets social inhibitions
/// veto autonomous physiology actions (eating, sleeping, urinating, defecating).
/// </summary>
public class PhysiologyGateConfig
{
    /// <summary>
    /// Effective veto strength at or above which an action class is blocked.
    /// Range 0–1. Default 0.50: a 50% effective inhibition is sufficient to veto.
    /// </summary>
    public double VetoStrengthThreshold { get; set; } = 0.50;

    /// <summary>
    /// Willpower value below which low-willpower leakage begins weakening the veto.
    /// At this value leakage is 0; at willpower 0 leakage is 1 (gate fully open).
    /// Default 30.
    /// </summary>
    public int LowWillpowerLeakageStart { get; set; } = 30;

    /// <summary>
    /// Maximum fraction by which acute stress can relax the veto (0–1).
    /// At default 0.7: an NPC at 100% acute stress has only 30% of normal veto strength.
    /// </summary>
    public double StressMaxRelaxation { get; set; } = 0.7;
}

// ── Life-state system (WP-3.0.0) ─────────────────────────────────────────────

/// <summary>
/// LifeStateSystem tuning — default incapacitation duration, death side-effects, and
/// behaviour switches for biologically ticking but unconscious entities.
/// </summary>
public class LifeStateConfig
{
    /// <summary>
    /// Default number of ticks an NPC remains in <see cref="LifeState.Incapacitated"/>
    /// before the transition system auto-promotes to <see cref="LifeState.Deceased"/>.
    /// At the default time scale (120 game-s/real-s), 180 ticks ≈ 3 game-minutes.
    /// Per-cause overrides (e.g. chokingIncapacitationTicks) live on their respective
    /// scenario configs (ChokingConfig, etc.) and are written into LifeStateComponent
    /// at incapacitation time.
    /// Default: 180.
    /// </summary>
    public int DefaultIncapacitatedTicks { get; set; } = 180;

    /// <summary>
    /// When true, the InvariantSystem applies the deceased-invariant set to Deceased
    /// entities rather than the live-invariant set. Disable only for debugging.
    /// Default: true.
    /// </summary>
    public bool EmitDeathInvariantOnTransition { get; set; } = true;

    /// <summary>
    /// When true, an Incapacitated NPC's bladder may void autonomously
    /// (BladderSystem still runs on IsBiologicallyTicking entities).
    /// Default: true — matches real physiology.
    /// </summary>
    public bool IncapacitatedAllowsBladderVoid { get; set; } = true;

    /// <summary>
    /// When true, a Deceased NPC's position is frozen by MovementSystem skipping them.
    /// The body stays where it fell. WP-3.0.2 introduces corpse-drag for removal.
    /// Default: true.
    /// </summary>
    public bool DeceasedFreezesPosition { get; set; } = true;
}

// ── Choking scenario (WP-3.0.1) ──────────────────────────────────────────────

/// <summary>
/// Tuning knobs for <see cref="APIFramework.Systems.LifeState.ChokingDetectionSystem"/>.
/// Controls which boluses are choking hazards, what physiological conditions
/// make an NPC vulnerable, and how severe the resulting incapacitation is.
/// </summary>
public class ChokingConfig
{
    /// <summary>
    /// BolusComponent.Toughness (0–1) at or above which a bolus is a choking hazard.
    /// Banana default is 0.2 — well below the threshold. Tough foods (jerky ~0.8)
    /// will choke a distracted NPC.
    /// Default: 0.65.
    /// </summary>
    public float BolusSizeThreshold { get; set; } = 0.65f;

    /// <summary>
    /// EnergyComponent.Energy (0–100) strictly below which the NPC is considered
    /// sufficiently distracted to choke. Low energy → fatigue → impaired swallowing.
    /// Default: 40.
    /// </summary>
    public float EnergyThreshold { get; set; } = 40f;

    /// <summary>
    /// StressComponent.AcuteLevel (0–100) at or above which the NPC is distracted
    /// enough to choke. High stress degrades motor coordination.
    /// Default: 70.
    /// </summary>
    public int StressThreshold { get; set; } = 70;

    /// <summary>
    /// SocialDrivesComponent.Irritation.Current (0–100) at or above which the NPC
    /// is distracted enough to choke. Irritation drives inattentive eating.
    /// Default: 65.
    /// </summary>
    public int IrritationThreshold { get; set; } = 65;

    /// <summary>
    /// Ticks the NPC remains in <see cref="LifeState.Incapacitated"/> while choking
    /// before LifeStateTransitionSystem auto-promotes to Deceased.
    /// At the default time scale (120 game-s/real-s), 90 ticks ≈ 1.5 game-minutes
    /// — shorter than the generic DefaultIncapacitatedTicks (180) since choking is acute.
    /// Default: 90.
    /// </summary>
    public int IncapacitationTicks { get; set; } = 90;

    /// <summary>
    /// Acute panic level (0..1, NOT the 0–100 emotion scale) applied to
    /// <see cref="APIFramework.Components.MoodComponent.PanicLevel"/> at the moment of choke.
    /// MoodSystem decays this toward 0 at NegativeDecayRate each game-second.
    /// Default: 0.85.
    /// </summary>
    public float PanicMoodIntensity { get; set; } = 0.85f;

    /// <summary>
    /// When true, ChokingDetectionSystem emits a ChokeStarted narrative candidate at
    /// the instant of choke so witnesses record the onset of the episode.
    /// Default: true.
    /// </summary>
    public bool EmitChokeStartedNarrative { get; set; } = true;
}

// ── Bereavement scenario (WP-3.0.2) ──────────────────────────────────────────

/// <summary>
/// Tuning knobs for the bereavement pipeline:
/// <see cref="APIFramework.Systems.LifeState.BereavementSystem"/> (immediate cascade),
/// <see cref="APIFramework.Systems.LifeState.BereavementByProximitySystem"/> (proximity tier),
/// and <see cref="APIFramework.Systems.StressSystem"/> (bereavement stress branches).
/// </summary>
public class BereavementConfig
{
    /// <summary>
    /// Stress gain applied once to a witness (second participant in a death narrative event).
    /// StressSystem reads StressComponent.WitnessedDeathEventsToday, applies this gain, then
    /// clears the counter. Default: 20.
    /// </summary>
    public double WitnessedDeathStressGain { get; set; } = 20.0;

    /// <summary>
    /// Stress gain applied once to a non-witness colleague per bereavement event.
    /// StressSystem reads StressComponent.BereavementEventsToday, applies this gain, then
    /// clears the counter. Default: 5.
    /// </summary>
    public double BereavementStressGain { get; set; } = 5.0;

    /// <summary>
    /// MoodComponent.GriefLevel (0–100) set on the witness immediately after a death.
    /// Applied as Max(current, WitnessGriefIntensity). Default: 80.
    /// </summary>
    public float WitnessGriefIntensity { get; set; } = 80f;

    /// <summary>
    /// MoodComponent.GriefLevel ceiling for non-witness colleagues before scaling by
    /// relationship intensity fraction. Final value = ColleagueBereavementGriefIntensity × (Intensity / 100).
    /// Default: 40.
    /// </summary>
    public float ColleagueBereavementGriefIntensity { get; set; } = 40f;

    /// <summary>
    /// Minimum RelationshipComponent.Intensity (0–100) required for a non-witness colleague
    /// to receive bereavement impact. Below this threshold: no impact, no narrative.
    /// Default: 20.
    /// </summary>
    public int BereavementMinIntensity { get; set; } = 20;

    /// <summary>
    /// Minimum RelationshipComponent.Intensity (0–100) required for proximity bereavement
    /// to fire when an NPC enters the corpse's room. Default: 30.
    /// </summary>
    public int ProximityBereavementMinIntensity { get; set; } = 30;

    /// <summary>
    /// Direct AcuteLevel gain applied (one-shot, per NPC per corpse) when an NPC physically
    /// enters the room containing the corpse of a NPC they had a meaningful relationship with.
    /// Applied directly to StressComponent.AcuteLevel. Default: 8.
    /// </summary>
    public double ProximityBereavementStressGain { get; set; } = 8.0;
}

// ── Fainting scenario (WP-3.0.6) ─────────────────────────────────────────────

/// <summary>
/// Tuning knobs for the fainting pipeline:
/// <see cref="APIFramework.Systems.LifeState.FaintingDetectionSystem"/> (trigger),
/// <see cref="APIFramework.Systems.LifeState.FaintingRecoverySystem"/> (wakeup), and
/// <see cref="APIFramework.Systems.LifeState.FaintingCleanupSystem"/> (tag removal).
///
/// Fainting is a temporary loss of consciousness triggered by extreme fear. The NPC
/// enters <see cref="LifeState.Incapacitated"/> and automatically recovers to
/// <see cref="LifeState.Alive"/> after <see cref="FaintDurationTicks"/> — it is
/// never fatal by design.
/// </summary>
public class FaintingConfig
{
    /// <summary>
    /// MoodComponent.Fear (0–100) at or above which an Alive NPC faints.
    /// At the default of 85 this corresponds to near-terror on Plutchik's wheel.
    /// Default: 85.
    /// </summary>
    public float FearThreshold { get; set; } = 85f;

    /// <summary>
    /// Number of ticks the NPC remains Incapacitated before FaintingRecoverySystem
    /// queues the recovery to Alive. The IncapacitatedTickBudget is set to
    /// FaintDurationTicks + 1 so the budget-expiry death check cannot fire first.
    /// At the default time scale (120 game-s/real-s), 20 ticks ≈ 20 game-seconds.
    /// Default: 20.
    /// </summary>
    public int FaintDurationTicks { get; set; } = 20;

    /// <summary>
    /// When true, FaintingDetectionSystem emits a <see cref="APIFramework.Systems.Narrative.NarrativeEventKind.Fainted"/>
    /// candidate on the NarrativeEventBus before the incapacitation request is enqueued,
    /// so witnesses record the onset of the faint while all participants are Alive.
    /// Default: true.
    /// </summary>
    public bool EmitFaintedNarrative { get; set; } = true;

    /// <summary>
    /// When true, FaintingRecoverySystem emits a
    /// <see cref="APIFramework.Systems.Narrative.NarrativeEventKind.RegainedConsciousness"/>
    /// candidate on the NarrativeEventBus before the Alive recovery request is enqueued.
    /// Default: true.
    /// </summary>
    public bool EmitRegainedConsciousnessNarrative { get; set; } = true;
}

// ── Corpse scenario (WP-3.0.2) ───────────────────────────────────────────────

/// <summary>
/// Configuration for corpse entity behaviour after death.
/// Reserved for future expansion (decay, smell, ambient drift config).
/// At v0.1 this class is a stub; all corpse behaviour is governed by the life-state
/// and bereavement systems.
/// </summary>
public class CorpseConfig
{
    // Placeholder for future per-cause or per-archetype corpse configuration.
    // WP-3.0.2: no active tuning knobs at v0.1.
}

// ── Social mask system ────────────────────────────────────────────────────────

/// <summary>
/// SocialMaskSystem tuning — per-tick mask gain/decay, personality scaling, and the
/// crack-pressure formula governing public mask slips.
/// </summary>
public class SocialMaskConfig
{
    /// <summary>Maximum mask delta gained per tick when drive is elevated and exposure is full (0–100 scale).</summary>
    public double MaskGainPerTick              { get; set; } = 0.5;

    /// <summary>Mask delta lost per tick in low-exposure context (0–100 scale).</summary>
    public double MaskDecayPerTick             { get; set; } = 0.3;

    /// <summary>Exposure factor below which mask decays rather than grows (0–1).</summary>
    public double LowExposureThreshold         { get; set; } = 0.30;

    /// <summary>Per point of Conscientiousness: mask gain multiplied by (1 + C × scale).</summary>
    public double PersonalityMaskScale         { get; set; } = 0.20;

    /// <summary>Per point of Extraversion: mask gain multiplied by (1 - E × scale).</summary>
    public double PersonalityExtraversionScale { get; set; } = 0.10;

    /// <summary>Crack fires when crackPressure >= this threshold. Default 1.50.</summary>
    public double CrackThreshold               { get; set; } = 1.50;

    /// <summary>Fraction of AcuteLevel added to crack pressure. Default 0.50.</summary>
    public double StressCrackContribution      { get; set; } = 0.50;

    /// <summary>Flat crack pressure bonus when BurningOutTag is present. Default 0.30.</summary>
    public double BurnoutCrackBonus            { get; set; } = 0.30;

    /// <summary>Willpower at or below this contributes to crack pressure. Default 30.</summary>
    public int    LowWillpowerThreshold        { get; set; } = 30;

    /// <summary>Minimum ticks between successive cracks for the same NPC. Default 1800.</summary>
    public int    SlipCooldownTicks            { get; set; } = 1800;
}
