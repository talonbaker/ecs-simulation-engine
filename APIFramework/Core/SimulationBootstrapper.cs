using System;
using System.Collections.Generic;
using APIFramework.Bootstrap;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Systems;
using APIFramework.Systems.Coupling;
using APIFramework.Systems.Dialog;
using APIFramework.Systems.Lighting;
using APIFramework.Systems.Movement;
using APIFramework.Systems.Chronicle;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;
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
///  PreUpdate   (0)  InvariantSystem           — catch/clamp impossible state values
///  Physiology  (10) MetabolismSystem          — drain satiation / hydration
///  Physiology  (10) EnergySystem              — drain/restore energy + sleepiness
///  Physiology  (10) BladderFillSystem         — fill bladder at constant rate
///  Condition   (20) BiologicalConditionSystem — set hunger/thirst/irritable tags
///  Cognition   (30) MoodSystem                — decay emotions; apply Plutchik intensity tags
///  Cognition   (30) BrainSystem               — score drives (incl. circadian, colon, bladder); pick dominant
///  Cognition   (30) PhysiologyGateSystem      — write BlockedActionsComponent; inhibitions veto biology
///  Behavior    (40) FeedingSystem             — act if Eat is dominant (skipped if Eat blocked)
///  Behavior    (40) DrinkingSystem            — act if Drink is dominant
///  Behavior    (40) SleepSystem               — toggle IsSleeping based on dominant desire
///  Behavior    (40) DefecationSystem          — empty colon if Defecate is dominant
///  Behavior    (40) UrinationSystem           — empty bladder if Pee is dominant
///  Transit     (50) InteractionSystem         — convert held food to esophagus bolus
///  Transit     (50) EsophagusSystem           — move transit entities toward stomach
///  Transit     (50) DigestionSystem           — release nutrients; deposit chyme to small intestine
///  Elimination (55) SmallIntestineSystem      — drain chyme; pass residue to large intestine
///  Elimination (55) LargeIntestineSystem      — reabsorb water; form stool into colon
///  Elimination (55) ColonSystem               — apply DefecationUrgeTag / BowelCriticalTag
///  Elimination (55) BladderSystem             — apply UrinationUrgeTag / BladderCriticalTag
///  World       (60) RotSystem                 — age food entities; apply RotTag at threshold
///  Narrative   (70) CorpseSpawnerSystem       — bus subscriber; attaches CorpseTag on death events (WP-3.0.2)
///  Narrative   (70) BereavementSystem         — bus subscriber; witness + colleague grief cascade (WP-3.0.2)
///  Cleanup     (80) StressSystem              — accumulate cortisol-like stress
///  Cleanup     (80) WorkloadSystem            — advance task progress; detect completion/overdue
///  Cleanup     (80) MaskCrackSystem           — detect social mask cracks after intent is locked
///  Cleanup     (80) BereavementByProximitySystem — per-tick; NPC enters corpse's room (WP-3.0.2)
///  Cleanup     (80) FaintingDetectionSystem   — detect Fear>=threshold; enqueue Incapacitated (WP-3.0.6)
///  Cleanup     (80) FaintingRecoverySystem    — queue Alive recovery when RecoveryTick reached (WP-3.0.6)
///  Cleanup     (80) ChokingDetectionSystem    — detect choke events; enqueue incapacitation (WP-3.0.1)
///  Cleanup     (80) LifeStateTransitionSystem — drain transition queue; tick down Incapacitated budgets
///  Cleanup     (80) ChokingCleanupSystem      — remove IsChokingTag from Deceased NPCs (WP-3.0.1)
///  Cleanup     (80) FaintingCleanupSystem     — remove IsFaintingTag from recovered Alive NPCs (WP-3.0.6)
/// </summary>
public class SimulationBootstrapper
{
    public SimulationEngine    Engine              { get; }
    public EntityManager       EntityManager       { get; }
    public SimulationClock     Clock               { get; }
    public SimConfig           Config              { get; }
    public InvariantSystem     Invariants          { get; }
    public WillpowerEventQueue WillpowerEvents     { get; }

    /// <summary>
    /// Deterministic RNG source for all simulation systems.
    /// Seeded from the <c>seed</c> constructor parameter; default seed is 0.
    /// Systems that need randomness (e.g. <see cref="MovementSystem"/>) receive
    /// this instance so every random call is part of the same seeded sequence,
    /// making the entire simulation deterministic given the same seed.
    /// </summary>
    public SeededRandom Random { get; }

    // ── Spatial services ──────────────────────────────────────────────────────

    /// <summary>Cell-based spatial index. Singleton; shared by all spatial systems.</summary>
    public ISpatialIndex        SpatialIndex    { get; }

    /// <summary>Proximity event bus. Subscribe here to receive spatial signals.</summary>
    public ProximityEventBus    ProximityBus    { get; }

    /// <summary>Runtime room-membership map. Queried by social and behavior systems.</summary>
    public EntityRoomMembership RoomMembership  { get; }

    // ── Lighting services ─────────────────────────────────────────────────────

    /// <summary>Singleton sun state. Updated by SunSystem each tick; read by aperture and accumulation systems.</summary>
    public SunStateService SunState { get; }

    // ── Coupling services ─────────────────────────────────────────────────────

    /// <summary>Lighting-to-drive coupling table loaded from SimConfig.lighting.driveCouplings.</summary>
    public LightingDriveCouplingTable DriveCouplingTable { get; }

    /// <summary>Fractional drive accumulator shared by LightingToDriveCouplingSystem.</summary>
    public SocialDriveAccumulator DriveAccumulator { get; }

    // ── Narrative services ────────────────────────────────────────────────────

    /// <summary>Narrative event bus. Subscribe to receive candidates emitted each tick.</summary>
    public NarrativeEventBus NarrativeBus { get; }

    // ── Chronicle services ────────────────────────────────────────────────────

    /// <summary>Global persistent narrative chronicle. Read by TelemetryProjector each tick.</summary>
    public ChronicleService Chronicle { get; }

    // ── Dialog services ───────────────────────────────────────────────────────

    /// <summary>
    /// Loaded phrase corpus. Null when the corpus file could not be located at boot.
    /// Dialog systems are skipped when null.
    /// </summary>
    public DialogCorpusService? CorpusService { get; private set; }

    /// <summary>Queue shared between DialogContextDecisionSystem and DialogFragmentRetrievalSystem.</summary>
    public PendingDialogQueue PendingDialogQueue { get; }

    /// <summary>
    /// Single-writer life-state transition system. Scenario systems (choking, slip-and-fall, etc.)
    /// call <see cref="LifeStateTransitionSystem.RequestTransition"/> to kill or incapacitate an NPC.
    /// Exposed so tests and future scenario systems can push requests without constructing their own instance.
    /// </summary>
    public LifeStateTransitionSystem LifeStateTransitions { get; private set; } = null!;

    /// <summary>
    /// Primary constructor — accepts any IConfigProvider.
    /// Use this for tests (InMemoryConfigProvider) and Unity (custom provider).
    /// </summary>
    /// <param name="configProvider">Config source.</param>
    /// <param name="humanCount">
    /// How many human entities to spawn on startup.
    /// Default is 100 (full stress-test world).
    /// Pass 1 for isolated single-entity tests; pass 0 to spawn no humans
    /// (useful for world-object-only unit tests).
    /// </param>
    /// <param name="seed">
    /// RNG seed for deterministic replay. Two runs with the same seed,
    /// config, and command log produce byte-identical telemetry streams.
    /// Defaults to 0 when not supplied.
    /// </param>
    /// <summary>Singleton pathfinding service — computes A* paths on demand.</summary>
    public PathfindingService Pathfinding { get; }

    /// <summary>
    /// Set when the bootstrapper was given a <c>worldDefinitionPath</c> and the
    /// world was loaded via <see cref="WorldDefinitionLoader"/>. Null when the
    /// default <see cref="SpawnWorld"/> path was used.
    /// </summary>
    public LoadResult? WorldLoadResult { get; private set; }

    /// <summary>
    /// NPC entities produced by <see cref="CastGenerator.SpawnAll"/> after the world
    /// definition is loaded. Null when no world definition path was supplied or when
    /// the archetype catalog could not be located.
    /// </summary>
    public IReadOnlyList<Entity>? SpawnedNpcs { get; private set; }

    /// <summary>
    /// Relationship entities seeded by <see cref="CastGenerator.SeedRelationships"/>.
    /// Null when cast generation did not run.
    /// </summary>
    public IReadOnlyList<Entity>? SeededRelationships { get; private set; }

    public SimulationBootstrapper(IConfigProvider configProvider, int humanCount = DefaultHumanCount, int seed = 0, string? worldDefinitionPath = null)
    {
        Config          = configProvider.GetConfig();
        EntityManager   = new EntityManager();
        Clock           = new SimulationClock { TimeScale = Config.World.DefaultTimeScale };
        Engine          = new SimulationEngine(EntityManager, Clock);
        Random          = new SeededRandom(seed);
        WillpowerEvents = new WillpowerEventQueue();

        // Spatial services — instantiated before RegisterSystems so systems can receive them
        SpatialIndex   = new GridSpatialIndex(Config.Spatial);
        ProximityBus   = new ProximityEventBus();
        RoomMembership = new EntityRoomMembership();

        // Lighting services
        SunState = new SunStateService();

        // Coupling services
        DriveCouplingTable = new LightingDriveCouplingTable(Config.Lighting.DriveCouplings);
        DriveAccumulator   = new SocialDriveAccumulator();

        // Movement services
        Pathfinding = new PathfindingService(
            EntityManager,
            Config.Spatial.WorldSize.Width,
            Config.Spatial.WorldSize.Height,
            Config.Movement);

        // Chronicle services — created before Invariants so the check can be injected.
        Chronicle = new ChronicleService(Config.Chronicle.MaxEntries);

        // Invariants — receives Chronicle for the chronicle ↔ entity-tree agreement check.
        Invariants = new InvariantSystem(Clock, Chronicle);

        // Narrative services
        NarrativeBus = new NarrativeEventBus();

        // Dialog services
        PendingDialogQueue = new PendingDialogQueue();
        var corpusPath = DialogCorpusService.FindCorpusFile(Config.Dialog.CorpusPath);
        if (corpusPath != null)
        {
            try   { CorpusService = DialogCorpusService.LoadFromFile(corpusPath); }
            catch (Exception ex)
            { Console.WriteLine($"[Dialog] Corpus load failed: {ex.Message} — dialog systems disabled."); }
        }
        else
        {
            Console.WriteLine($"[Dialog] Corpus file '{Config.Dialog.CorpusPath}' not found — dialog systems disabled.");
        }

        RegisterSystems();
        if (worldDefinitionPath != null)
        {
            WorldLoadResult = WorldDefinitionLoader.LoadFromFile(worldDefinitionPath, EntityManager, Random);
            if (WorldLoadResult.NpcSlotCount > 0)
            {
                var catalog = ArchetypeCatalog.LoadDefault();
                if (catalog != null)
                {
                    SpawnedNpcs       = CastGenerator.SpawnAll(catalog, EntityManager, Random, Config.CastGenerator);
                    SeededRelationships = CastGenerator.SeedRelationships(SpawnedNpcs, catalog, EntityManager, Random, Config.CastGenerator);
                }
            }
        }
        else
            SpawnWorld(humanCount);
    }

    /// <summary>
    /// Convenience overload — loads config from the JSON file at <paramref name="configPath"/>.
    /// This is the default used by the Avalonia GUI and CLI.
    /// </summary>
    /// <param name="configPath">Path to SimConfig.json.</param>
    /// <param name="humanCount">
    /// How many human entities to spawn.  Defaults to <see cref="DefaultHumanCount"/> (100).
    /// </param>
    /// <param name="seed">
    /// RNG seed. Defaults to 0. Pass a nonzero value for deterministic replay.
    /// </param>
    /// <param name="worldDefinitionPath">
    /// Optional path to a world-definition.json. When supplied, the loader instantiates
    /// all world entities from the file instead of <see cref="SpawnWorld"/>.
    /// </param>
    public SimulationBootstrapper(string configPath = "SimConfig.json", int humanCount = DefaultHumanCount, int seed = 0, string? worldDefinitionPath = null)
        : this(new FileConfigProvider(configPath), humanCount, seed, worldDefinitionPath) { }

    private void RegisterSystems()
    {
        var sys = Config.Systems;

        // Spatial — sync index and room membership (Phase 5).
        // ProximityEvent moves to Lighting (Phase 7) so it fires after illumination is current.
        var syncSys = new SpatialIndexSyncSystem(SpatialIndex);
        EntityManager.EntityDestroyed += syncSys.OnEntityDestroyed;
        Engine.AddSystem(syncSys,                                                SystemPhase.Spatial);
        Engine.AddSystem(new RoomMembershipSystem(RoomMembership, ProximityBus), SystemPhase.Spatial);

        // Lighting — sun position, source state machines, aperture beams, room illumination,
        // then proximity events (which now see current illumination).
        var lightSourceStates = new LightSourceStateSystem(Random, Config.Lighting);
        var apertureBeams     = new ApertureBeamSystem(SunState, Clock);
        Engine.AddSystem(new SunSystem(Clock, SunState, Config.Lighting),                           SystemPhase.Lighting);
        Engine.AddSystem(lightSourceStates,                                                          SystemPhase.Lighting);
        Engine.AddSystem(apertureBeams,                                                              SystemPhase.Lighting);
        Engine.AddSystem(new IlluminationAccumulationSystem(lightSourceStates, apertureBeams, Config.Lighting), SystemPhase.Lighting);
        Engine.AddSystem(new ProximityEventSystem(SpatialIndex, ProximityBus, RoomMembership),      SystemPhase.Lighting);

        // Coupling — lighting-to-drive coupling; after illumination is fresh, before drive dynamics.
        Engine.AddSystem(new LightingToDriveCouplingSystem(
            DriveCouplingTable, DriveAccumulator, RoomMembership, apertureBeams, SunState),          SystemPhase.Coupling);

        // PreUpdate — invariant enforcement; always first
        Engine.AddSystem(Invariants,                                               SystemPhase.PreUpdate);
        // Schedule spawner: attach routines to NPCs that lack one (runs every tick, idempotent).
        Engine.AddSystem(new ScheduleSpawnerSystem(),                              SystemPhase.PreUpdate);

        // Stress initialization — attaches StressComponent to newly-spawned NPCs that lack one.
        Engine.AddSystem(
            new StressInitializerSystem(StressInitializerSystem.LoadBaselines()),  SystemPhase.PreUpdate);

        // Mask initialization — attaches SocialMaskComponent with personality-derived baseline.
        Engine.AddSystem(new MaskInitializerSystem(),                              SystemPhase.PreUpdate);

        // Workload initialization — attaches WorkloadComponent with per-archetype capacity.
        Engine.AddSystem(
            new WorkloadInitializerSystem(WorkloadInitializerSystem.LoadCapacities()), SystemPhase.PreUpdate);

        // Life-state initialization — attaches LifeStateComponent(Alive) to every NPC.
        // Runs last in PreUpdate so all other initializers have already fired.
        Engine.AddSystem(new LifeStateInitializerSystem(),                         SystemPhase.PreUpdate);

        // Task generation — spawns new task entities once per game-day at the configured hour.
        Engine.AddSystem(
            new TaskGeneratorSystem(Config.Workload, Clock, Random),               SystemPhase.PreUpdate);

        // Physiology — raw biological resource drain/restore
        Engine.AddSystem(new MetabolismSystem(),                                   SystemPhase.Physiology);
        Engine.AddSystem(new EnergySystem(sys.Energy),                            SystemPhase.Physiology);
        Engine.AddSystem(new BladderFillSystem(),                                  SystemPhase.Physiology);

        // Condition — derive sensation tags from physiology values
        Engine.AddSystem(new BiologicalConditionSystem(sys.BiologicalCondition),  SystemPhase.Condition);
        // Schedule: resolve active block before ActionSelectionSystem reads it.
        Engine.AddSystem(new ScheduleSystem(Clock),                                SystemPhase.Condition);

        // Cognition — process conditions into emotions and drive scores
        Engine.AddSystem(new MoodSystem(sys.Mood),                                SystemPhase.Cognition);
        Engine.AddSystem(new BrainSystem(sys.Brain, Clock),                       SystemPhase.Cognition);

        // Physiology gate — veto set computed after BrainSystem, before Behavior systems act.
        // PhysiologyGateSystem writes BlockedActionsComponent for each NPC with inhibitions.
        Engine.AddSystem(new PhysiologyGateSystem(Config.PhysiologyGate),                  SystemPhase.Cognition);

        // Social cognition — drive dynamics, action selection, willpower, relationship lifecycle
        Engine.AddSystem(new DriveDynamicsSystem(Config.Social, Clock, Random, Config.Stress), SystemPhase.Cognition);
        Engine.AddSystem(new ActionSelectionSystem(
            SpatialIndex, RoomMembership, WillpowerEvents, Random, Config.ActionSelection, Config.Schedule, EntityManager, Config.Workload),
                                                                                   SystemPhase.Cognition);
        Engine.AddSystem(new WillpowerSystem(Config.Social, WillpowerEvents),      SystemPhase.Cognition);
        Engine.AddSystem(RelationshipLifecycleSystem.LoadFromFile(Config.Social),  SystemPhase.Cognition);
        Engine.AddSystem(new SocialMaskSystem(RoomMembership, Config.SocialMask),  SystemPhase.Cognition);

        // Behavior — act on the dominant drive
        Engine.AddSystem(new FeedingSystem(sys.Feeding),                          SystemPhase.Behavior);
        Engine.AddSystem(new DrinkingSystem(sys.Drinking),                        SystemPhase.Behavior);
        Engine.AddSystem(new SleepSystem(sys.Sleep),                              SystemPhase.Behavior);
        Engine.AddSystem(new DefecationSystem(),                                  SystemPhase.Behavior);
        Engine.AddSystem(new UrinationSystem(),                                    SystemPhase.Behavior);

        // Transit — move content through the upper digestive pipeline
        Engine.AddSystem(new InteractionSystem(sys.Interaction),                  SystemPhase.Transit);
        Engine.AddSystem(new EsophagusSystem(),                                   SystemPhase.Transit);
        Engine.AddSystem(new DigestionSystem(sys.Digestion),                      SystemPhase.Transit);

        // Elimination — lower digestive pipeline; intestines → colon/bladder → tags
        Engine.AddSystem(new SmallIntestineSystem(),                              SystemPhase.Elimination);
        Engine.AddSystem(new LargeIntestineSystem(),                              SystemPhase.Elimination);
        Engine.AddSystem(new ColonSystem(),                                       SystemPhase.Elimination);
        Engine.AddSystem(new BladderSystem(),                                     SystemPhase.Elimination);

        // World — environmental systems independent of entity biology
        Engine.AddSystem(new RotSystem(sys.Rot),                                  SystemPhase.World);

        // Movement quality pipeline (runs in World phase, in registration order)
        Engine.AddSystem(new PathfindingTriggerSystem(Pathfinding),                SystemPhase.World);
        Engine.AddSystem(new MovementSpeedModifierSystem(Config.Movement),         SystemPhase.World);
        Engine.AddSystem(new StepAsideSystem(SpatialIndex, RoomMembership, Config.Movement), SystemPhase.World);
        Engine.AddSystem(new MovementSystem(Random),                               SystemPhase.World);
        Engine.AddSystem(new FacingSystem(ProximityBus),                           SystemPhase.World);
        Engine.AddSystem(new IdleMovementSystem(Random, Config.Movement),          SystemPhase.World);

        // Narrative — runs last so all state has settled; emits candidates via NarrativeBus
        Engine.AddSystem(new NarrativeEventDetector(
            NarrativeBus, ProximityBus, RoomMembership, Config.Narrative),        SystemPhase.Narrative);

        // Chronicle — evaluates candidates emitted this tick; must run after NarrativeEventDetector.
        Engine.AddSystem(new PersistenceThresholdDetector(
            Chronicle, NarrativeBus, EntityManager, Clock, Random, Config.Chronicle), SystemPhase.Narrative);

        // Memory recording — subscribes to the bus and routes candidates to per-pair/personal buffers.
        Engine.AddSystem(new MemoryRecordingSystem(NarrativeBus, EntityManager, Config.Memory), SystemPhase.Narrative);

        // Corpse spawner (WP-3.0.2) — bus subscriber; attaches CorpseTag + CorpseComponent on death events.
        Engine.AddSystem(new CorpseSpawnerSystem(NarrativeBus, EntityManager),                 SystemPhase.Narrative);

        // Bereavement (WP-3.0.2) — bus subscriber; witness + colleague immediate impact on death events.
        Engine.AddSystem(new BereavementSystem(NarrativeBus, EntityManager, Clock, Config.Bereavement), SystemPhase.Narrative);

        // Dialog — after Narrative so final drive state is visible
        if (CorpusService != null)
        {
            var decisionSys   = new DialogContextDecisionSystem(PendingDialogQueue, ProximityBus, Config.Dialog, Random);
            var retrievalSys  = new DialogFragmentRetrievalSystem(PendingDialogQueue, CorpusService, ProximityBus, Config.Dialog);
            var calcifySys    = new DialogCalcifySystem(Config.Dialog);
            Engine.AddSystem(decisionSys,  SystemPhase.Dialog);
            Engine.AddSystem(retrievalSys, SystemPhase.Dialog);
            Engine.AddSystem(calcifySys,   SystemPhase.Dialog);
        }

        // Cleanup — stress accumulation; runs after WillpowerSystem (Cognition) and
        // NarrativeEventDetector (Narrative) so all tick state has settled.
        Engine.AddSystem(
            new StressSystem(Config.Stress, Config.Workload, Clock, WillpowerEvents, NarrativeBus, EntityManager, Config.Bereavement), SystemPhase.Cleanup);

        // Workload system — advances task progress, detects completion and overdue.
        Engine.AddSystem(
            new WorkloadSystem(Config.Workload, Clock, NarrativeBus, EntityManager), SystemPhase.Cleanup);

        // Mask crack detection — runs at Cleanup so it fires after ActionSelectionSystem has
        // written its intent; the crack override wins for the following Dialog phase.
        Engine.AddSystem(
            new MaskCrackSystem(RoomMembership, NarrativeBus, Config.SocialMask),  SystemPhase.Cleanup);

        // Life-state transitions — drains the transition request queue and ticks down
        // IncapacitatedTickBudgets. Must run LAST in Cleanup so all scenario detection
        // systems have had their chance to enqueue requests before the queue is drained.
        // Registered here (before AddSystem) so scenario systems below can receive the reference.
        LifeStateTransitions = new LifeStateTransitionSystem(
            NarrativeBus, EntityManager, Clock, Config.LifeState, RoomMembership);

        // Bereavement by proximity (WP-3.0.2) — Cleanup; per-tick check for NPCs entering a corpse's room.
        // Runs before LifeStateTransitions so new deaths this tick become corpses next tick (CorpseTag
        // is attached by CorpseSpawnerSystem synchronously on the bus event, which fires from
        // LifeStateTransitionSystem, which runs at end of Cleanup — so new corpse entities from this
        // tick are available starting next tick, which is correct for this proximity system).
        Engine.AddSystem(
            new BereavementByProximitySystem(RoomMembership, Config.Bereavement),             SystemPhase.Cleanup);

        // Fainting detection (WP-3.0.6) — Cleanup, BEFORE LifeStateTransitions.
        // Iterates Alive NPCs, detects Fear >= FearThreshold, and enqueues
        // incapacitation requests (with FaintDurationTicks+1 budget) so fainting
        // can never expire into death before FaintingRecoverySystem acts.
        Engine.AddSystem(
            new FaintingDetectionSystem(
                LifeStateTransitions, NarrativeBus, Clock, RoomMembership, Config.Fainting),
            SystemPhase.Cleanup);

        // Fainting recovery (WP-3.0.6) — Cleanup, BEFORE LifeStateTransitions.
        // Watches fainted NPCs for RecoveryTick and queues the Alive recovery.
        // Must run BEFORE LifeStateTransitions so the recovery is drained in the same tick
        // it is queued (before the budget-expiry death check runs on remaining Incapacitated NPCs).
        Engine.AddSystem(
            new FaintingRecoverySystem(
                LifeStateTransitions, NarrativeBus, Clock, Config.Fainting),
            SystemPhase.Cleanup);

        // Choking detection (WP-3.0.1) — Cleanup, BEFORE LifeStateTransitions.
        // Iterates esophageal transit boluses, detects choke conditions, and enqueues
        // incapacitation requests for LifeStateTransitions to drain below.
        Engine.AddSystem(
            new ChokingDetectionSystem(
                LifeStateTransitions, NarrativeBus, EntityManager, Clock, RoomMembership, Config.Choking),
                                                                                   SystemPhase.Cleanup);

        // LifeStateTransitions — drains all scenario requests enqueued above.
        Engine.AddSystem(LifeStateTransitions,                                     SystemPhase.Cleanup);

        // Choking cleanup (WP-3.0.1) — AFTER LifeStateTransitions so the Deceased flip
        // has occurred before we remove IsChokingTag/ChokingComponent from dead NPCs.
        Engine.AddSystem(new ChokingCleanupSystem(),                               SystemPhase.Cleanup);

        // Fainting cleanup (WP-3.0.6) — AFTER LifeStateTransitions so the Alive flip
        // has occurred before we remove IsFaintingTag/FaintingComponent from recovered NPCs.
        Engine.AddSystem(new FaintingCleanupSystem(),                              SystemPhase.Cleanup);
    }

    // ── Human count ───────────────────────────────────────────────────────────
    /// <summary>
    /// Default number of humans spawned when no <c>humanCount</c> argument is
    /// supplied.  100 gives a realistic stress-test world; pass 1 to
    /// <see cref="SimulationBootstrapper(IConfigProvider,int)"/> for isolated
    /// single-entity tests.
    /// </summary>
    public const int DefaultHumanCount = 100;

    private void SpawnWorld(int humanCount)
    {
        // ── Living entities ───────────────────────────────────────────────────
        // Spread humanCount humans on a uniform grid inside the 10×10 world.
        // humanCount = 1  → single Billy at centre (5, 5).
        // humanCount = 100 → 10×10 grid from (1,1) to (9,9).
        SpawnHumanGrid(humanCount);

        // ── World objects — 10×10 unit apartment ─────────────────────────────
        //   Fridge  (2, 0, 2)  NW — kitchen
        //   Sink    (7, 0, 2)  NE — kitchen
        //   Bed     (2, 0, 8)  SW — bedroom
        //   Toilet  (7, 0, 8)  SE — bathroom
        //
        // Fridge food scales with population so every human has a realistic
        // chance of getting fed: 5 items per human, minimum 5.
        var fridge = EntityManager.CreateEntity();
        fridge.Add(new IdentityComponent { Name = "Fridge" });
        fridge.Add(new PositionComponent { X = 2f, Y = 0f, Z = 2f });
        fridge.Add(new FridgeComponent   { FoodCount = Math.Max(5, humanCount * 5) });

        SpawnWorldObject<SinkComponent>   ("Sink",   7f, 0f, 2f);
        SpawnWorldObject<BedComponent>    ("Bed",    2f, 0f, 8f);
        SpawnWorldObject<ToiletComponent> ("Toilet", 7f, 0f, 8f);
    }

    /// <summary>
    /// Spreads <paramref name="count"/> humans evenly on a rectangular grid.
    /// For count = 1 the single entity lands at centre (5, 5).
    /// </summary>
    private void SpawnHumanGrid(int count)
    {
        if (count <= 0) return;

        if (count == 1)
        {
            EntityTemplates.SpawnHuman(EntityManager, Config.Entities.Human,
                spawnX: 5f, spawnZ: 5f, name: "Billy");
            return;
        }

        // cols × rows ≥ count, shaped as square as possible.
        int cols = (int)Math.Ceiling(Math.Sqrt(count));
        int rows = (int)Math.Ceiling((double)count / cols);

        float margin = 1f;         // keep entities away from walls
        float worldSz = 10f;
        float stepX = cols > 1 ? (worldSz - margin * 2f) / (cols - 1) : 0f;
        float stepZ = rows > 1 ? (worldSz - margin * 2f) / (rows - 1) : 0f;

        int spawned = 0;
        for (int r = 0; r < rows && spawned < count; r++)
        {
            for (int c = 0; c < cols && spawned < count; c++)
            {
                float x = margin + c * stepX;
                float z = margin + r * stepZ;
                EntityTemplates.SpawnHuman(EntityManager, Config.Entities.Human,
                    spawnX: x, spawnZ: z, name: $"Human {spawned + 1}");
                spawned++;
            }
        }
    }

    private void SpawnWorldObject<TTag>(string name, float x, float y, float z)
        where TTag : struct
    {
        var e = EntityManager.CreateEntity();
        e.Add(new IdentityComponent { Name = name });
        e.Add(new PositionComponent { X = x, Y = y, Z = z });
        e.Add(default(TTag));
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
        MergeFlat(newCfg.Narrative,                   Config.Narrative,                   changes);
        MergeFlat(newCfg.Chronicle,                   Config.Chronicle,                   changes);
        MergeFlat(newCfg.Chronicle.ThresholdRules,    Config.Chronicle.ThresholdRules,    changes);

        // ── Entity starting configs (only affect future spawns) ───────────────
        MergeFlat(newCfg.Entities.Human.Metabolism,     Config.Entities.Human.Metabolism,     changes);
        MergeFlat(newCfg.Entities.Human.Stomach,       Config.Entities.Human.Stomach,       changes);
        MergeFlat(newCfg.Entities.Human.Energy,        Config.Entities.Human.Energy,        changes);
        MergeFlat(newCfg.Entities.Human.SmallIntestine, Config.Entities.Human.SmallIntestine, changes);
        MergeFlat(newCfg.Entities.Human.LargeIntestine, Config.Entities.Human.LargeIntestine, changes);
        MergeFlat(newCfg.Entities.Human.Colon,          Config.Entities.Human.Colon,          changes);
        MergeFlat(newCfg.Entities.Human.Bladder,        Config.Entities.Human.Bladder,        changes);
        MergeFlat(newCfg.Entities.Cat.Metabolism,       Config.Entities.Cat.Metabolism,       changes);
        MergeFlat(newCfg.Entities.Cat.Stomach,          Config.Entities.Cat.Stomach,          changes);
        MergeFlat(newCfg.Entities.Cat.Energy,           Config.Entities.Cat.Energy,           changes);
        MergeFlat(newCfg.Entities.Cat.SmallIntestine,   Config.Entities.Cat.SmallIntestine,   changes);
        MergeFlat(newCfg.Entities.Cat.LargeIntestine,   Config.Entities.Cat.LargeIntestine,   changes);
        MergeFlat(newCfg.Entities.Cat.Colon,            Config.Entities.Cat.Colon,            changes);
        MergeFlat(newCfg.Entities.Cat.Bladder,          Config.Entities.Cat.Bladder,          changes);

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
