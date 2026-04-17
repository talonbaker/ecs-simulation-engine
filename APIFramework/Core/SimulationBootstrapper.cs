using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Systems;
using System.Reflection;

namespace APIFramework.Core;

/// <summary>
/// Composition root for the entire headless simulation.
/// Loads SimConfig.json, wires every system in execution order, and spawns
/// the initial world entities.
///
/// Any frontend — Avalonia GUI, CLI, Unity, test harness — creates one instance
/// of this class and drives it by calling Engine.Update(deltaTime) in its own loop.
/// APIFramework never knows a frontend exists.
///
/// HOT-RELOAD
/// ──────────
/// Call ApplyConfig(newCfg) at any time to push new tuning values into all live
/// systems without restarting the simulation.
///
/// IMPORTANT: hot-reload updates system configs (thresholds, drain rates, brain
/// ceilings) immediately. It does NOT re-spawn entities — starting values like
/// EnergyStart only take effect for entities spawned AFTER the reload. To test
/// different starting conditions, restart the simulation.
///
/// SYSTEM PIPELINE (execution order)
/// ──────────────────────────────────
///  1. InvariantSystem           — catch/clamp impossible state values (runs FIRST)
///  2. MetabolismSystem          — drain satiation / hydration (sleep multiplier applied)
///  3. EnergySystem              — drain/restore energy + sleepiness; energy-state tags
///  4. BiologicalConditionSystem — set hunger/thirst/irritable tags
///  5. MoodSystem                — decay emotions, apply Plutchik intensity tags
///  6. BrainSystem               — score all drives (incl. circadian sleep); pick dominant
///  7. FeedingSystem             — act if Eat is dominant
///  8. DrinkingSystem            — act if Drink is dominant
///  9. SleepSystem               — toggle IsSleeping based on dominant desire
/// 10. InteractionSystem         — convert held food to esophagus bolus
/// 11. EsophagusSystem           — move transit entities toward stomach
/// 12. DigestionSystem           — release nutrients from stomach → SmallIntestineComponent
/// 13. SmallIntestineSystem      — absorb nutrients into body stores; residue → LargeIntestine
/// 14. LargeIntestineSystem      — recapture water; compact waste into WasteReadyMl
/// 15. RotSystem                 — age food entities; apply RotTag when threshold crossed
/// </summary>
public class SimulationBootstrapper
{
    public SimulationEngine Engine         { get; }
    public EntityManager    EntityManager  { get; }
    public SimulationClock  Clock          { get; }
    public SimConfig        Config         { get; }
    public InvariantSystem  Invariants     { get; }

    private readonly string _configPath;

    public SimulationBootstrapper(string configPath = "SimConfig.json")
    {
        _configPath   = configPath;
        Config        = SimConfig.Load(configPath);
        EntityManager = new EntityManager();
        Clock         = new SimulationClock { TimeScale = Config.World.DefaultTimeScale };
        Engine        = new SimulationEngine(EntityManager, Clock);
        Invariants    = new InvariantSystem(Clock);

        RegisterSystems();
        SpawnWorld();
    }

    private void RegisterSystems()
    {
        var sys = Config.Systems;

        Engine.AddSystem(Invariants);                                              //  1 — catch impossible states
        Engine.AddSystem(new MetabolismSystem());                                  //  2
        Engine.AddSystem(new EnergySystem(sys.Energy));                           //  3
        Engine.AddSystem(new BiologicalConditionSystem(sys.BiologicalCondition)); //  4
        Engine.AddSystem(new MoodSystem(sys.Mood));                               //  5
        Engine.AddSystem(new BrainSystem(sys.Brain, Clock));                      //  6
        Engine.AddSystem(new FeedingSystem(sys.Feeding));                         //  7
        Engine.AddSystem(new DrinkingSystem(sys.Drinking));                       //  8
        Engine.AddSystem(new SleepSystem(sys.Sleep));                             //  9
        Engine.AddSystem(new InteractionSystem(sys.Interaction));                 // 10
        Engine.AddSystem(new EsophagusSystem());                                  // 11
        Engine.AddSystem(new DigestionSystem(sys.Digestion));                     // 12
        Engine.AddSystem(new SmallIntestineSystem(sys.SmallIntestine));           // 13
        Engine.AddSystem(new LargeIntestineSystem(sys.LargeIntestine));           // 14
        Engine.AddSystem(new RotSystem(sys.Rot));                                 // 15
    }

    private void SpawnWorld()
    {
        EntityTemplates.SpawnHuman(EntityManager, Config.Entities.Human);
    }

    // ── Hot-reload ────────────────────────────────────────────────────────────

    /// <summary>
    /// Pushes all tuning values from <paramref name="newCfg"/> into the live running
    /// simulation without restarting. Systems immediately use the new values on the
    /// next tick.
    ///
    /// Works by mutating the EXISTING config objects in-place, so all system
    /// constructor references remain valid — no system pointer changes hands.
    ///
    /// Thread note: call this on the same thread that drives Engine.Update() to
    /// avoid a tick reading partially-applied config.  Both the CLI (flag check in
    /// the main loop) and the Avalonia GUI (DispatcherTimer on the UI thread) already
    /// satisfy this requirement.
    /// </summary>
    public void ApplyConfig(SimConfig newCfg)
    {
        var changes = new List<string>();

        // ── System configs ────────────────────────────────────────────────────
        MergeFlat(newCfg.Systems.BiologicalCondition, Config.Systems.BiologicalCondition, changes);
        MergeFlat(newCfg.Systems.Energy,              Config.Systems.Energy,              changes);
        MergeFlat(newCfg.Systems.Brain,               Config.Systems.Brain,               changes);
        MergeFlat(newCfg.Systems.Feeding,             Config.Systems.Feeding,             changes);
        MergeFlat(newCfg.Systems.Feeding.Banana,      Config.Systems.Feeding.Banana,      changes);
        MergeFlat(newCfg.Systems.Drinking,            Config.Systems.Drinking,            changes);
        MergeFlat(newCfg.Systems.Drinking.Water,      Config.Systems.Drinking.Water,      changes);
        MergeFlat(newCfg.Systems.Digestion,           Config.Systems.Digestion,           changes);
        MergeFlat(newCfg.Systems.SmallIntestine,      Config.Systems.SmallIntestine,      changes);
        MergeFlat(newCfg.Systems.LargeIntestine,      Config.Systems.LargeIntestine,      changes);
        MergeFlat(newCfg.Systems.Sleep,               Config.Systems.Sleep,               changes);
        MergeFlat(newCfg.Systems.Interaction,         Config.Systems.Interaction,         changes);
        MergeFlat(newCfg.Systems.Mood,                Config.Systems.Mood,                changes);
        MergeFlat(newCfg.Systems.Rot,                 Config.Systems.Rot,                 changes);

        // ── Entity starting configs (only affect future spawns) ───────────────
        MergeFlat(newCfg.Entities.Human.Metabolism,   Config.Entities.Human.Metabolism,   changes);
        MergeFlat(newCfg.Entities.Human.Stomach,      Config.Entities.Human.Stomach,      changes);
        MergeFlat(newCfg.Entities.Human.Energy,       Config.Entities.Human.Energy,       changes);
        MergeFlat(newCfg.Entities.Cat.Metabolism,     Config.Entities.Cat.Metabolism,     changes);
        MergeFlat(newCfg.Entities.Cat.Stomach,        Config.Entities.Cat.Stomach,        changes);
        MergeFlat(newCfg.Entities.Cat.Energy,         Config.Entities.Cat.Energy,         changes);

        if (changes.Count == 0)
        {
            Console.WriteLine("[Config] Reloaded — no values changed.");
        }
        else
        {
            Console.WriteLine($"[Config] Reloaded — {changes.Count} value(s) changed:");
            foreach (var c in changes)
                Console.WriteLine($"         {c}");
        }
    }

    /// <summary>
    /// Copies all primitive (value-type) public properties from <paramref name="src"/>
    /// onto <paramref name="dst"/> in-place, logging any that actually changed.
    /// Reference-type properties (nested objects) are intentionally skipped —
    /// they must be merged separately to preserve object identity.
    /// </summary>
    private static void MergeFlat<T>(T src, T dst, List<string> changes) where T : class
    {
        if (src == null || dst == null) return;

        foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                      .Where(p => p.CanRead && p.CanWrite && p.PropertyType.IsValueType))
        {
            var oldVal = prop.GetValue(dst);
            var newVal = prop.GetValue(src);

            if (!Equals(oldVal, newVal))
            {
                prop.SetValue(dst, newVal);
                changes.Add($"{typeof(T).Name}.{prop.Name}  {oldVal} → {newVal}");
            }
        }
    }
}
