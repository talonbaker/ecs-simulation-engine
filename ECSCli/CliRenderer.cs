using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Diagnostics;
using System.Diagnostics;

namespace ECSCli;

/// <summary>
/// Renders simulation state and diagnostics to stdout.
/// Two outputs:
///   PrintSnapshot() — live state every N game-minutes during a run
///   PrintReport()   — full metrics + balancing analysis at end of run
/// </summary>
public static class CliRenderer
{
    private const int BarWidth  = 14;
    private const int LineWidth = 66;

    // ═══════════════════════════════════════════════════════════════════════════
    //  LIVE SNAPSHOT
    // ═══════════════════════════════════════════════════════════════════════════

    public static void PrintSnapshot(SimulationBootstrapper sim, long tick, long wallStartTimestamp)
    {
        double wallSeconds = Elapsed(wallStartTimestamp);
        double simSpeed    = wallSeconds > 0.001 ? sim.Clock.TotalTime / wallSeconds : 0;
        string dayNight    = sim.Clock.IsDaytime ? "day" : "night";

        string line = new('─', LineWidth);
        Console.WriteLine();
        Console.WriteLine(line);
        Console.WriteLine($"  {SimVersion.Full}");
        Console.WriteLine(
            $"  TICK {tick,-8:N0}  {sim.Clock.DayTimeDisplay,-22}" +
            $"  [{dayNight}]  WALL {wallSeconds:F1}s  ({simSpeed:F0}x)");
        Console.WriteLine(line);

        var living = sim.EntityManager.Query<MetabolismComponent>().ToList();
        if (living.Count == 0)
        {
            Console.WriteLine("  (no living entities)");
        }
        else
        {
            foreach (var entity in living)
                PrintEntity(entity, sim.Clock);
        }

        var pipeline = sim.EntityManager.Query<EsophagusTransitComponent>().ToList();
        Console.WriteLine();
        Console.WriteLine("  ESOPHAGUS PIPELINE");
        if (pipeline.Count == 0)
        {
            Console.WriteLine("    — nothing in transit —");
        }
        else
        {
            foreach (var e in pipeline)
            {
                var transit = e.Get<EsophagusTransitComponent>();
                string content = e.Has<LiquidComponent>()  ? e.Get<LiquidComponent>().LiquidType
                               : e.Has<BolusComponent>()   ? e.Get<BolusComponent>().FoodType
                               : "Unknown";
                Console.WriteLine($"    {content,-14} {Bar(transit.Progress, 1f)}  {transit.Progress * 100,5:F1}%");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  END-OF-RUN REPORT
    // ═══════════════════════════════════════════════════════════════════════════

    public static void PrintReport(SimulationBootstrapper sim, SimMetrics metrics,
                                   long tick, long wallStartTimestamp)
    {
        double wallSec  = Elapsed(wallStartTimestamp);
        double gameSec  = sim.Clock.TotalTime;
        double gameHours = gameSec / 3600.0;
        int    dayCount  = sim.Clock.DayNumber - 1; // days completed

        string line  = new('═', LineWidth);
        string dash  = new('─', LineWidth);
        var    violations = sim.Invariants.Violations;

        Console.WriteLine();
        Console.WriteLine(line);
        Console.WriteLine($"  BALANCING REPORT  —  {SimVersion.Full}");
        Console.WriteLine($"  Config     : SimConfig.json");
        Console.WriteLine($"  Ran        : {gameHours:F1} game-hours  ({dayCount} full day(s))");
        Console.WriteLine($"  Wall time  : {wallSec:F1}s at {sim.Clock.TimeScale}x");
        Console.WriteLine($"  Ticks      : {tick:N0}");
        Console.WriteLine(line);

        // ── Invariant summary ────────────────────────────────────────────────
        Console.WriteLine();
        if (violations.Count == 0)
        {
            Console.WriteLine("  ✓  INVARIANTS     0 violations — all values stayed in range");
        }
        else
        {
            Console.WriteLine($"  ⚠  INVARIANTS     {violations.Count} violation(s) detected");
            Console.WriteLine();

            // Group by component.property and show the worst (most extreme) examples
            var grouped = violations
                .GroupBy(v => $"{v.Component}.{v.Property}")
                .OrderByDescending(g => g.Count());

            foreach (var group in grouped)
            {
                var worst = group.OrderByDescending(v => Math.Abs(v.ActualValue - v.ClampedTo)).First();
                Console.WriteLine(
                    $"    {group.Key,-40}  ×{group.Count(),4}  " +
                    $"worst: {worst.ActualValue:F4}  (valid [{worst.ValidMin}, {worst.ValidMax}])  " +
                    $"entity: {worst.EntityName}");
            }
        }

        // ── Performance diagnostics (v0.7.2 — O(1) component index) ─────────────
        Console.WriteLine();
        Console.WriteLine(dash);
        Console.WriteLine("  PERFORMANCE  —  v0.7.2 Query Index");
        Console.WriteLine(dash);

        int totalEntities = sim.EntityManager.Entities.Count;
        int livingCount   = sim.EntityManager.Query<MetabolismComponent>().Count();
        double avgTickUs  = tick > 0 ? (wallSec * 1_000_000.0) / tick : 0;

        Console.WriteLine($"    Entities (total / living) : {totalEntities} / {livingCount}");
        Console.WriteLine($"    Ticks completed           : {tick:N0}");
        Console.WriteLine($"    Avg tick wall time        : {avgTickUs:F2} µs  ({1_000_000.0 / avgTickUs:N0} ticks/sec)");
        Console.WriteLine();

        Console.WriteLine("    System phase layout:");
        var phaseGroups = sim.Engine.Registrations
            .GroupBy(r => r.Phase)
            .OrderBy(g => (int)g.Key);
        foreach (var group in phaseGroups)
        {
            string names = string.Join(", ", group.Select(r => r.System.GetType().Name.Replace("System", "")));
            Console.WriteLine($"      {group.Key,-10} ({(int)group.Key,3})  ×{group.Count()}  →  {names}");
        }

        Console.WriteLine();
        long scansAvoided = (long)tick * sim.Engine.Registrations.Count * totalEntities;
        Console.WriteLine($"    Query model   : O(1) component-index bucket lookup (v0.7.2+)");
        Console.WriteLine($"    Index buckets : {sim.EntityManager.ComponentTypeCount} component types indexed");
        Console.WriteLine($"    Scans avoided : ~{scansAvoided:N0} entity checks eliminated");
        Console.WriteLine($"                    ({sim.Engine.Registrations.Count} systems × {tick:N0} ticks × {totalEntities} entities)");

        // ── Per-entity stats ─────────────────────────────────────────────────
        foreach (var em in metrics.EntityStats)
        {
            Console.WriteLine();
            Console.WriteLine(dash);
            Console.WriteLine($"  {em.EntityName.ToUpperInvariant()}  —  LIFECYCLE");
            Console.WriteLine(dash);

            // Lifecycle events in chronological order
            if (em.Events.Count == 0)
            {
                Console.WriteLine("    No notable events recorded.");
            }
            else
            {
                foreach (var ev in em.Events.OrderBy(e => e.GameTime))
                    Console.WriteLine($"    {ev.GameTimeDisplay,-22}  {ev.EventName}");
            }

            Console.WriteLine();
            Console.WriteLine("  ACTIVITY COUNTS");
            Console.WriteLine($"    Feed events   : {em.FeedEvents}");
            Console.WriteLine($"    Drink events  : {em.DrinkEvents}");
            Console.WriteLine($"    Sleep cycles  : {em.SleepCycles}");

            Console.WriteLine();
            Console.WriteLine("  RESOURCE RANGES  (min → mean → max)");
            PrintResourceLine("Satiation  ", em.Satiation);
            PrintResourceLine("Hydration  ", em.Hydration);
            PrintResourceLine("Energy     ", em.Energy);
            PrintResourceLine("Sleepiness ", em.Sleepiness);
            PrintResourceLine("Stomach %  ", em.StomachFill);

            // ── Balancing hints ───────────────────────────────────────────────
            Console.WriteLine();
            Console.WriteLine("  BALANCING HINTS");
            PrintHints(em, gameHours);
        }

        Console.WriteLine();
        Console.WriteLine(line);
        Console.WriteLine("  Adjust values in SimConfig.json and re-run (or hot-reload).");
        Console.WriteLine(line);
    }

    // ── Balancing hint engine ─────────────────────────────────────────────────

    private static void PrintHints(EntityMetrics em, double gameHours)
    {
        bool anyHint = false;

        // Resource stuck near zero
        if (em.Satiation.Min < 2f && em.FeedEvents < 1)
            Hint(ref anyHint, "⚠ Satiation hit near-zero but no feed events detected — FeedingSystem may not be triggering.");

        if (em.Hydration.Min < 2f && em.DrinkEvents < 1)
            Hint(ref anyHint, "⚠ Hydration hit near-zero but no drink events detected — DrinkingSystem may not be triggering.");

        // Resource never drops (threshold too aggressive — entity never experiences the need)
        if (em.Satiation.Min > 60f)
            Hint(ref anyHint, "  Satiation never dropped below 60% — hunger threshold may be set too high, or drain rate too low.");

        if (em.Hydration.Min > 60f)
            Hint(ref anyHint, "  Hydration never dropped below 60% — thirst threshold may be set too high, or drain rate too low.");

        // Machine-gun feeding (too many events relative to game hours)
        double feedsPerHour  = gameHours > 0 ? em.FeedEvents  / gameHours : 0;
        double drinksPerHour = gameHours > 0 ? em.DrinkEvents / gameHours : 0;

        if (feedsPerHour > 4)
            Hint(ref anyHint, $"⚠ {feedsPerHour:F1} feed events/game-hour — possible machine-gun feeding. Check hunger threshold or nutrition queue cap.");

        if (drinksPerHour > 6)
            Hint(ref anyHint, $"⚠ {drinksPerHour:F1} drink events/game-hour — possible rapid-gulping. Check thirst threshold or hydration queue cap.");

        // No sleep at all in a long run
        if (gameHours > 20 && em.SleepCycles == 0)
            Hint(ref anyHint, $"⚠ No sleep cycles in {gameHours:F0} game-hours — sleepiness may never reach the brain threshold. Check sleepinessGainRate or sleepMaxScore.");

        // Sleep energy never recovered
        if (em.Energy.Max < 60f)
            Hint(ref anyHint, "⚠ Energy never exceeded 60% — entity may not be sleeping long enough, or energyRestoreRate is too low.");

        if (!anyHint)
            Console.WriteLine("    All patterns look reasonable for this run length.");
    }

    private static void Hint(ref bool anyHint, string msg)
    {
        anyHint = true;
        Console.WriteLine($"    {msg}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void PrintEntity(Entity entity, SimulationClock clock)
    {
        string name = entity.Has<IdentityComponent>()
            ? entity.Get<IdentityComponent>().Name
            : $"Entity {entity.ShortId}";

        Console.WriteLine();
        Console.WriteLine($"  ◈ {name}  [{entity.ShortId}]");

        var tagList = BuildTagList(entity);
        Console.WriteLine($"    Tags      {(tagList.Count > 0 ? string.Join("  ·  ", tagList) : "— none —")}");

        var meta = entity.Get<MetabolismComponent>();
        Console.WriteLine($"    Satiation  {Bar(meta.Satiation, 100f)}  {meta.Satiation,5:F1}%    hunger {meta.Hunger,5:F1}%");
        Console.WriteLine($"    Hydration  {Bar(meta.Hydration, 100f)}  {meta.Hydration,5:F1}%    thirst {meta.Thirst,5:F1}%");

        if (entity.Has<EnergyComponent>())
        {
            var en = entity.Get<EnergyComponent>();
            string sleepState = en.IsSleeping ? "SLEEPING" : "awake";
            Console.WriteLine($"    Energy     {Bar(en.Energy,     100f)}  {en.Energy,5:F1}%    {sleepState}");
            Console.WriteLine($"    Sleepiness {Bar(en.Sleepiness, 100f)}  {en.Sleepiness,5:F1}%    circadian ×{clock.CircadianFactor:F2}");
        }

        if (entity.Has<DriveComponent>())
        {
            var d = entity.Get<DriveComponent>();
            Console.WriteLine($"    Brain      dominant {d.Dominant.ToString().ToUpperInvariant(),-9}" +
                              $"  eat {d.EatUrgency:F2}  drink {d.DrinkUrgency:F2}  sleep {d.SleepUrgency:F2}" +
                              $"  poop {d.DefecateUrgency:F2}  pee {d.PeeUrgency:F2}");
        }

        if (entity.Has<MoodComponent>())
        {
            var mood = entity.Get<MoodComponent>();
            Console.WriteLine($"    ── Emotions ──────────────────────────────────────");
            Console.WriteLine($"    Joy          {Bar(mood.Joy,          100f)}  {mood.Joy,5:F1}");
            Console.WriteLine($"    Trust        {Bar(mood.Trust,        100f)}  {mood.Trust,5:F1}");
            Console.WriteLine($"    Anticipation {Bar(mood.Anticipation, 100f)}  {mood.Anticipation,5:F1}");
            Console.WriteLine($"    Anger        {Bar(mood.Anger,        100f)}  {mood.Anger,5:F1}");
            Console.WriteLine($"    Sadness      {Bar(mood.Sadness,      100f)}  {mood.Sadness,5:F1}");
            Console.WriteLine($"    Disgust      {Bar(mood.Disgust,      100f)}  {mood.Disgust,5:F1}");
            Console.WriteLine($"    Fear         {Bar(mood.Fear,         100f)}  {mood.Fear,5:F1}");
            Console.WriteLine($"    Surprise     {Bar(mood.Surprise,     100f)}  {mood.Surprise,5:F1}");

            float valence = mood.Valence;
            string valStr = valence >= 0 ? $"+{valence:F1}" : $"{valence:F1}";
            Console.WriteLine($"    Valence {valStr,7}  (+ positive  − negative)");

            var emotionTags = BuildEmotionTagList(entity);
            if (emotionTags.Count > 0)
                Console.WriteLine($"    Mood tags  {string.Join("  ·  ", emotionTags)}");
        }

        // ── GI pipeline (v0.7.3+) ────────────────────────────────────────────
        bool hasGi = entity.Has<SmallIntestineComponent>()
                  && entity.Has<LargeIntestineComponent>()
                  && entity.Has<ColonComponent>();
        if (hasGi)
        {
            var si    = entity.Get<SmallIntestineComponent>();
            var li    = entity.Get<LargeIntestineComponent>();
            var colon = entity.Get<ColonComponent>();
            string colonStatus = colon.IsCritical ? " CRITICAL" : colon.HasUrge ? " URGE" : "";
            Console.WriteLine($"    ── GI Pipeline ───────────────────────────────────");
            Console.WriteLine($"    S.Int      {Bar(si.Fill,    1f)}  {si.Fill    * 100,5:F1}%  ({si.ChymeVolumeMl:F1}/{SmallIntestineComponent.MaxVolumeMl:F0} ml)");
            Console.WriteLine($"    L.Int      {Bar(li.Fill,    1f)}  {li.Fill    * 100,5:F1}%  ({li.ContentVolumeMl:F1}/{LargeIntestineComponent.MaxVolumeMl:F0} ml)");
            Console.WriteLine($"    Colon      {Bar(colon.Fill, 1f)}  {colon.Fill * 100,5:F1}%  ({colon.StoolVolumeMl:F1}/{colon.CapacityMl:F0} ml){colonStatus}");
        }

        // ── Bladder (v0.7.4+) ─────────────────────────────────────────────────
        if (entity.Has<BladderComponent>())
        {
            var bladder = entity.Get<BladderComponent>();
            string bladderStatus = bladder.IsCritical ? " CRITICAL" : bladder.HasUrge ? " URGE" : "";
            Console.WriteLine($"    Bladder    {Bar(bladder.Fill, 1f)}  {bladder.Fill * 100,5:F1}%  ({bladder.VolumeML:F1}/{bladder.CapacityMl:F0} ml){bladderStatus}");
        }

        if (entity.Has<StomachComponent>())
        {
            var s = entity.Get<StomachComponent>();
            var q = s.NutrientsQueued;
            Console.WriteLine($"    Stomach    {Bar(s.Fill, 1f)}  {s.Fill * 100,5:F1}%    ({s.CurrentVolumeMl:F0}/{StomachComponent.MaxVolumeMl:F0} ml)");
            Console.WriteLine($"               queued  {q.Calories,6:F0} kcal  water {q.Water,5:F1}ml  carbs {q.Carbohydrates,4:F1}g  prot {q.Proteins,4:F1}g  fat {q.Fats,4:F1}g");
        }

        // ── Body nutrient stores (v0.7.0+) ────────────────────────────────────
        // Cumulative macros/water/vitamins absorbed by the body. Future organ-
        // systems will subtract from this (daily burn, elimination).
        var store = meta.NutrientStores;
        if (!store.IsEmpty)
        {
            Console.WriteLine($"    ── Nutrient Stores ───────────────────────────────");
            Console.WriteLine($"    Calories     {store.Calories,7:F0} kcal");
            Console.WriteLine($"    Macros       carbs {store.Carbohydrates,5:F1}g  prot {store.Proteins,5:F1}g  fat {store.Fats,5:F1}g  fiber {store.Fiber,5:F1}g");
            Console.WriteLine($"    Water        {store.Water,7:F0} ml");
            if (store.VitaminA + store.VitaminB + store.VitaminC + store.VitaminD + store.VitaminE + store.VitaminK > 0.01f)
                Console.WriteLine($"    Vitamins     A {store.VitaminA:F1}  B {store.VitaminB:F1}  C {store.VitaminC:F1}  D {store.VitaminD:F1}  E {store.VitaminE:F1}  K {store.VitaminK:F1}  (mg)");
            if (store.Sodium + store.Potassium + store.Calcium + store.Iron + store.Magnesium > 0.01f)
                Console.WriteLine($"    Minerals     Na {store.Sodium:F0}  K {store.Potassium:F0}  Ca {store.Calcium:F0}  Fe {store.Iron:F1}  Mg {store.Magnesium:F0}  (mg)");
        }
    }

    private static void PrintResourceLine(string label, ResourceStats s)
    {
        Console.WriteLine($"    {label}   {s.Min,5:F1}% → {s.Mean,5:F1}% → {s.Max,5:F1}%");
    }

    private static List<string> BuildTagList(Entity entity)
    {
        var tags = new List<string>();
        // Vital state
        if (entity.Has<HungerTag>())         tags.Add("HUNGRY");
        if (entity.Has<ThirstTag>())         tags.Add("THIRSTY");
        if (entity.Has<StarvingTag>())       tags.Add("STARVING");
        if (entity.Has<DehydratedTag>())     tags.Add("DEHYDRATED");
        if (entity.Has<TiredTag>())          tags.Add("TIRED");
        if (entity.Has<ExhaustedTag>())      tags.Add("EXHAUSTED");
        if (entity.Has<SleepingTag>())       tags.Add("SLEEPING");
        if (entity.Has<IrritableTag>())      tags.Add("IRRITABLE");
        // Elimination urges (v0.7.3+)
        if (entity.Has<BowelCriticalTag>())  tags.Add("BOWEL CRITICAL");
        else if (entity.Has<DefecationUrgeTag>()) tags.Add("DEFECATION URGE");
        if (entity.Has<BladderCriticalTag>())tags.Add("BLADDER CRITICAL");
        else if (entity.Has<UrinationUrgeTag>())  tags.Add("URINATION URGE");
        return tags;
    }

    private static List<string> BuildEmotionTagList(Entity entity)
    {
        var tags = new List<string>();
        // Joy family
        if (entity.Has<EcstaticTag>())      tags.Add("ECSTATIC");
        else if (entity.Has<JoyfulTag>())   tags.Add("JOYFUL");
        else if (entity.Has<SereneTag>())   tags.Add("serene");
        // Disgust/Boredom family
        if (entity.Has<LoathingTag>())      tags.Add("LOATHING");
        else if (entity.Has<DisgustTag>())  tags.Add("DISGUSTED");
        else if (entity.Has<BoredTag>())    tags.Add("bored");
        // Anger family
        if (entity.Has<RagingTag>())        tags.Add("RAGING");
        else if (entity.Has<AngryTag>())    tags.Add("ANGRY");
        else if (entity.Has<AnnoyedTag>())  tags.Add("annoyed");
        // Sadness family
        if (entity.Has<GriefTag>())         tags.Add("GRIEF");
        else if (entity.Has<SadTag>())      tags.Add("SAD");
        else if (entity.Has<PensiveTag>())  tags.Add("pensive");
        // Anticipation family
        if (entity.Has<VigilantTag>())          tags.Add("VIGILANT");
        else if (entity.Has<AnticipatingTag>()) tags.Add("anticipating");
        else if (entity.Has<InterestedTag>())   tags.Add("interested");
        // Fear family
        if (entity.Has<TerrorTag>())            tags.Add("TERRIFIED");
        else if (entity.Has<FearfulTag>())      tags.Add("FEARFUL");
        else if (entity.Has<ApprehensiveTag>()) tags.Add("apprehensive");
        // Surprise family
        if (entity.Has<AmazedTag>())            tags.Add("AMAZED");
        else if (entity.Has<SurprisedTag>())    tags.Add("surprised");
        else if (entity.Has<DistractedTag>())   tags.Add("distracted");
        // Trust family
        if (entity.Has<AdmiringTag>())          tags.Add("ADMIRING");
        else if (entity.Has<TrustingTag>())     tags.Add("trusting");
        else if (entity.Has<AcceptingTag>())    tags.Add("accepting");
        return tags;
    }

    private static string Bar(float value, float max)
    {
        float ratio  = Math.Clamp(value / max, 0f, 1f);
        int   filled = (int)(ratio * BarWidth);
        int   empty  = BarWidth - filled;
        return "[" + new string('█', filled) + new string('░', empty) + "]";
    }

    private static double Elapsed(long wallStartTimestamp) =>
        (double)(Stopwatch.GetTimestamp() - wallStartTimestamp) / Stopwatch.Frequency;
}
