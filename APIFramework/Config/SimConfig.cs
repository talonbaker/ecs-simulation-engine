using System.Text.Json;
using System.Text.Json.Serialization;

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
    public SleepSystemConfig               Sleep               { get; set; } = new();
    public InteractionSystemConfig         Interaction         { get; set; } = new();
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
}

// ── FeedingSystem ─────────────────────────────────────────────────────────────

public class FeedingSystemConfig
{
    /// <summary>Hunger level (0–100) required before the brain considers eating.</summary>
    public float HungerThreshold { get; set; } = 40f;

    /// <summary>
    /// Max nutrition already queued in the stomach before FeedingSystem stops spawning.
    /// Prevents over-eating when digestion hasn't caught up.
    /// </summary>
    public float NutritionQueueCap { get; set; } = 70f;

    /// <summary>Properties of the hardcoded banana food source (temporary until world food exists).</summary>
    public FoodItemConfig Banana { get; set; } = new();
}

public class FoodItemConfig
{
    /// <summary>Physical volume that enters the stomach (ml).</summary>
    public float VolumeMl { get; set; } = 50f;

    /// <summary>Nutrition points released to Satiation after digestion.</summary>
    public float NutritionValue { get; set; } = 35f;

    /// <summary>Chew resistance 0.0 (soft) → 1.0 (very tough). Affects bite time.</summary>
    public float Toughness { get; set; } = 0.2f;

    /// <summary>Speed at which this bolus travels down the esophagus (progress/second).</summary>
    public float EsophagusSpeed { get; set; } = 0.3f;
}

// ── DrinkingSystem ────────────────────────────────────────────────────────────

public class DrinkingSystemConfig
{
    /// <summary>
    /// Maximum hydration queued in stomach before the system stops spawning water.
    /// Prevents machine-gun gulping under normal thirst.
    /// </summary>
    public float HydrationQueueCap { get; set; } = 30f;

    /// <summary>
    /// Raised hydration queue cap when the entity is severely dehydrated (DehydratedTag).
    /// Allows faster water intake during emergencies.
    /// </summary>
    public float HydrationQueueCapDehydrated { get; set; } = 60f;

    /// <summary>Properties of the hardcoded water source (temporary until world water exists).</summary>
    public DrinkItemConfig Water { get; set; } = new();
}

public class DrinkItemConfig
{
    /// <summary>Physical volume that enters the stomach per gulp (ml).</summary>
    public float VolumeMl { get; set; } = 15f;

    /// <summary>Hydration points released to Hydration after digestion.</summary>
    public float HydrationValue { get; set; } = 30f;

    /// <summary>Speed at which liquid travels down the esophagus (progress/second).</summary>
    public float EsophagusSpeed { get; set; } = 0.8f;
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
