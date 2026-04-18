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
/// SYSTEM PIPELINE (phase → execution order within phase)
/// ────────────────────────────────────────────────────────
///  PreUpdate  (0)  InvariantSystem           — catch/clamp impossible state values
///  Physiology (10) MetabolismSystem          — drain satiation / hydration
///  Physiology (10) EnergySystem              — drain/restore energy + sleepiness
///  Condition  (20) BiologicalConditionSystem — set hunger/thirst/irritable tags
///  Cognition  (30) MoodSystem                — decay emotions; apply Plutchik intensity tags
///  Cognition  (30) BrainSystem               — score drives (incl. circadian); pick dominant
///  Behavior   (40) FeedingSystem             — act if Eat is dominant
///  Behavior   (40) DrinkingSystem            — act if Drink is dominant
///  Behavior   (40) SleepSystem               — toggle IsSleeping based on dominant desire
///  Transit    (50) InteractionSystem         — convert held food to esophagus bolus
///  Transit    (50) EsophagusSystem           — move transit entities toward stomach
///  Transit    (50) DigestionSystem           — release nutrients from stomach to metabolism
///  World      (60) RotSystem                 — age food entities; apply RotTag at threshold
/// </summary>
public class SimulationBootstrapper
{
    public SimulationEngine Engine         { get; }
    public EntityManager    EntityManager  { get; }
    public SimulationClock  Clock          { get; }
    public SimConfig        Config         { get; }
    public InvariantSystem  Invariants     { get; }

    /// <summary>
    /// Primary constructor — accepts any IConfigProvider.
    /// Use this for tests (InMemoryConfigProvider) and Unity (custom provider).
    /// </summary>
    public SimulationBootstrapper(IConfigProvider configProvider)
    {
        Config        = configProvider.GetConfig();
        EntityManager = new EntityManager();
        Clock         = new SimulationClock { TimeScale = Config.World.DefaultTimeScale };
        Engine        = new SimulationEngine(EntityManager, Clock);
        Invariants    = new InvariantSystem(Clock);

        RegisterSystems();
        SpawnWorld();
    }

    /// <summary>
    /// Convenience overload — loads config from the JSON file at <paramref name="configPath"/>.
    /// This is the default used by the Avalonia GUI and CLI.
    /// </summary>
    public SimulationBootstrapper(string configPath = "SimConfig.json")
        : this(new FileConfigProvider(configPath)) { }

    private void RegisterSystems()
    {
        var sys = Config.Systems;

        // PreUpdate — invariant enforcement; always first
        Engine.AddSystem(Invariants,                                               SystemPhase.PreUpdate);

        // Physiology — raw biological resource drain/restore
        Engine.AddSystem(new MetabolismSystem(),                                   SystemPhase.Physiology);
        Engine.AddSystem(new EnergySystem(sys.Energy),                            SystemPhase.Physiology);

        // Condition — derive sensation tags from physiology values
        Engine.AddSystem(new BiologicalConditionSystem(sys.BiologicalCondition),  SystemPhase.Condition);

        // Cognition — process conditions into emotions and drive scores
        Engine.AddSystem(new MoodSystem(sys.Mood),                                SystemPhase.Cognition);
        Engine.AddSystem(new BrainSystem(sys.Brain, Clock),                       SystemPhase.Cognition);

        // Behavior — act on the dominant drive
        Engine.AddSystem(new FeedingSystem(sys.Feeding),                          SystemPhase.Behavior);
        Engine.AddSystem(new DrinkingSystem(sys.Drinking),                        SystemPhase.Behavior);
        Engine.AddSystem(new SleepSystem(sys.Sleep),                              SystemPhase.Behavior);

        // Transit — move content through the digestive pipeline
        Engine.AddSystem(new InteractionSystem(sys.Interaction),                  SystemPhase.Transit);
        Engine.AddSystem(new EsophagusSystem(),                                   SystemPhase.Transit);
        Engine.AddSystem(new DigestionSystem(sys.Digestion),                      SystemPhase.Transit);

        // World — environmental systems independent of entity biology
        Engine.AddSystem(new RotSystem(sys.Rot),                                  SystemPhase.World);
    }

    private void SpawnWorld()
    {
        EntityTemplates.SpawnHuman(EntityManager, Config.Entities.Human);
    }

    // ── Snapshot ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Captures the current engine state as an immutable SimulationSnapshot.
    /// Call once per frame from any frontend's render loop; hand the result to
    /// every system that needs to display or log it.
    ///
    /// Usage:
    ///   var snap = sim.Capture();
    ///   Console.WriteLine(snap.Clock.TimeDisplay);
    /// </summary>
    public SimulationSnapshot Capture() => SimulationSnapshot.Capture(this);

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
