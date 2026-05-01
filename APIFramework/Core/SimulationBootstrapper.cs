using System;
using System.Collections.Generic;
using APIFramework.Bootstrap;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Mutation;
using APIFramework.Systems;
using APIFramework.Systems.Coupling;
using APIFramework.Systems.Dialog;
using APIFramework.Systems.Lighting;
using APIFramework.Systems.Movement;
using APIFramework.Systems.Chronicle;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;
using APIFramework.Systems.LifeState;
using System.Reflection;

namespace APIFramework.Core;

/// <summary>
/// Composition root for the entire headless simulation.
/// Loads SimConfig.json, wires every system in execution order, and spawns
/// the initial world entities.
///
/// Any frontend â€” Avalonia GUI, CLI, Unity, test harness â€” creates one instance
/// of this class and drives it by calling Engine.Update(deltaTime) in its own loop.
/// APIFramework never knows a frontend exists.
///
/// HOT-RELOAD
/// â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
/// Call ApplyConfig(newCfg) at any time to push new tuning values into all live
/// systems without restarting the simulation.
///
/// IMPORTANT: hot-reload updates system configs (thresholds, drain rates, brain
/// ceilings) immediately. It does NOT re-spawn entities â€” starting values like
/// EnergyStart only take effect for entities spawned AFTER the reload. To test
/// different starting conditions, restart the simulation.
///
/// SYSTEM PIPELINE (phase â†’ execution order within phase)
/// â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
///  PreUpdate   (0)  InvariantSystem           â€” catch/clamp impossible state values
///  PreUpdate   (0)  StructuralTaggingSystem   â€” one-shot: tag obstacles/walls/doors at boot
///  PreUpdate   (0)  ScheduleSpawnerSystem     â€” attach default routines to scheduleless NPCs
///  PreUpdate   (0)  StressInitializerSystem   â€” attach StressComponent to fresh NPCs
///  PreUpdate   (0)  MaskInitializerSystem     â€” attach SocialMaskComponent (personality baseline)
///  PreUpdate   (0)  WorkloadInitializerSystem â€” attach WorkloadComponent (per-archetype capacity)
///  PreUpdate   (0)  LifeStateInitializerSystemâ€” attach LifeStateComponent = Alive
///  PreUpdate   (0)  TaskGeneratorSystem       â€” spawn day's task batch at configured hour
///  PreUpdate   (0)  LockoutDetectionSystem    â€” Phase 3: end-of-day exit reachability + starvation
///  Spatial     (5)  SpatialIndexSyncSystem    â€” keep spatial index in sync with positions
///  Spatial     (5)  RoomMembershipSystem      â€” derive per-NPC room residency
///  Spatial     (5)  PathfindingCacheInvalidationSystem â€” clear cache on structural change
///  Lighting    (7)  SunSystem                 â€” advance sun phase / day-phase boundary
///  Lighting    (7)  LightSourceStateSystem    â€” flicker/dying state machines
///  Lighting    (7)  ApertureBeamSystem        â€” compute aperture beams from sun + clock
///  Lighting    (7)  IlluminationAccumulationSystem â€” combine source + aperture per room
///  Lighting    (7)  ProximityEventSystem      â€” emit proximity signals against current illumination
///  Coupling    (8)  LightingToDriveCouplingSystem â€” accumulate lighting â†’ drive deltas
///  Physiology  (10) MetabolismSystem          â€” drain satiation / hydration
///  Physiology  (10) EnergySystem              â€” drain/restore energy + sleepiness
///  Physiology  (10) BladderFillSystem         â€” fill bladder at constant rate
///  Condition   (20) BiologicalConditionSystem â€” set hunger/thirst/irritable tags
///  Condition   (20) ScheduleSystem            â€” resolve active block before ActionSelection reads it
///  Cognition   (30) MoodSystem                â€” decay emotions; apply Plutchik intensity tags
///  Cognition   (30) BrainSystem               â€” score drives (incl. circadian, colon, bladder); pick dominant
///  Cognition   (30) PhysiologyGateSystem      â€” write BlockedActionsComponent; inhibitions veto biology
///  Cognition   (30) DriveDynamicsSystem       â€” decay/circadian-modulate social drives
///  Cognition   (30) ActionSelectionSystem     â€” enumerate candidates, pick winner, write IntendedAction
///  Cognition   (30) WillpowerSystem           â€” apply suppression cost / regen
///  Cognition   (30) RelationshipLifecycleSystem â€” relationship intensity / lifecycle
///  Cognition   (30) SocialMaskSystem          â€” drift mask in public, decay in private
///  Behavior    (40) FeedingSystem             â€” act if Eat is dominant (skipped if Eat blocked)
///  Behavior    (40) DrinkingSystem            â€” act if Drink is dominant
///  Behavior    (40) SleepSystem               â€” toggle IsSleeping based on dominant desire
///  Behavior    (40) DefecationSystem          â€” empty colon if Defecate is dominant
///  Behavior    (40) UrinationSystem           â€” empty bladder if Pee is dominant
///  Transit     (50) InteractionSystem         â€” convert held food to esophagus bolus
///  Transit     (50) EsophagusSystem           â€” move transit entities toward stomach
///  Transit     (50) DigestionSystem           â€” release nutrients; deposit chyme to small intestine
///  Elimination (55) SmallIntestineSystem      â€” drain chyme; pass residue to large intestine
///  Elimination (55) LargeIntestineSystem      â€” reabsorb water; form stool into colon
///  Elimination (55) ColonSystem               â€” apply DefecationUrgeTag / BowelCriticalTag
///  Elimination (55) BladderSystem             â€” apply UrinationUrgeTag / BladderCriticalTag
///  World       (60) RotSystem                 â€” age food entities; apply RotTag at threshold
///  World       (60) PathfindingTriggerSystem  â€” kick off A* requests for movement intents
///  World       (60) MovementSpeedModifierSystem â€” derive per-NPC speed multiplier
///  World       (60) StepAsideSystem           â€” perpendicular shift on near-miss
///  World       (60) MovementSystem            â€” advance positions along paths
///  World       (60) FacingSystem              â€” update facing from proximity signals
///  World       (60) IdleMovementSystem        â€” jitter/posture for idle NPCs
///  Narrative   (70) NarrativeEventDetector    â€” emit narrative candidates this tick
///  Narrative   (70) PersistenceThresholdDetector â€” promote candidates to chronicle entries
///  Narrative   (70) MemoryRecordingSystem     â€” route candidates to per-pair / personal memory buffers
///  Dialog      (80) DialogContextDecisionSystem    â€” choose context, queue dialog attempts
///  Dialog      (80) DialogFragmentRetrievalSystem  â€” pick fragments from corpus
///  Dialog      (80) DialogCalcifySystem            â€” promote/decalcify catchphrases
///  Cleanup     (90) StressSystem              â€” accumulate acute/chronic stress, apply tags
///  Cleanup     (90) WorkloadSystem            â€” advance task progress; detect completion / overdue
///  Cleanup     (90) MaskCrackSystem           â€” Phase 3: emit MaskCrack when pressure exceeds threshold
///  Cleanup     (90) ChokingDetectionSystem    â€” Phase 3: bolus + distraction â†’ enqueue Incapacitated
///  Cleanup     (90) LifeStateTransitionSystem â€” Phase 3: Alive â†’ Incapacitated â†’ Deceased
///  Cleanup     (90) ChokingCleanupSystem      â€” Phase 3: clear choke tags after death
///  Cleanup     (90) SlipAndFallSystem         â€” Phase 3: roll fall-risk hazards on settled positions
/// </summary>
public class SimulationBootstrapper
{
    /// <summary>
    /// The phased system runner. Frontends call <c>Engine.Update(deltaTime)</c>
    /// every frame to advance the simulation. Built and fully populated by the constructor;
    /// systems are added in <see cref="RegisterSystems"/>.
    /// </summary>
    public SimulationEngine    Engine              { get; }

    /// <summary>
    /// Authoritative entity store. Created entities are owned here for the lifetime
    /// of the simulation. Pass the same instance to test code that wants to query
    /// or assert on world state.
    /// </summary>
    public EntityManager       EntityManager       { get; }

    /// <summary>
    /// Game clock. <c>Clock.TimeScale</c> is initialized from
    /// <see cref="WorldConfig.DefaultTimeScale"/>; the GUI's time-scale slider
    /// multiplies on top of this.
    /// </summary>
    public SimulationClock     Clock               { get; }

    /// <summary>
    /// Loaded SimConfig instance. Shared by reference with every system, so
    /// <see cref="ApplyConfig"/> can hot-reload values in place without
    /// recreating systems.
    /// </summary>
    public SimConfig           Config              { get; }

    /// <summary>
    /// Invariant enforcement system. Receives the chronicle so it can verify
    /// the chronicle â†” entity-tree agreement check each tick.
    /// </summary>
    public InvariantSystem     Invariants          { get; }

    /// <summary>
    /// Single-writer queue for willpower deltas. Multiple systems push events;
    /// only <see cref="WillpowerSystem"/> drains and applies them.
    /// </summary>
    /// <remarks>Single-writer rule preserves determinism under multi-system suppression events.</remarks>
    public WillpowerEventQueue WillpowerEvents     { get; }

    /// <summary>
    /// Deterministic RNG source for all simulation systems.
    /// Seeded from the <c>seed</c> constructor parameter; default seed is 0.
    /// Systems that need randomness (e.g. <see cref="MovementSystem"/>) receive
    /// this instance so every random call is part of the same seeded sequence,
    /// making the entire simulation deterministic given the same seed.
    /// </summary>
    public SeededRandom Random { get; }

    // â”€â”€ Spatial services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Cell-based spatial index. Singleton; shared by all spatial systems.</summary>
    public ISpatialIndex        SpatialIndex    { get; }

    /// <summary>Proximity event bus. Subscribe here to receive spatial signals.</summary>
    public ProximityEventBus    ProximityBus    { get; }

    /// <summary>Runtime room-membership map. Queried by social and behavior systems.</summary>
    public EntityRoomMembership RoomMembership  { get; }

    // â”€â”€ Lighting services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Singleton sun state. Updated by SunSystem each tick; read by aperture and accumulation systems.</summary>
    public SunStateService SunState { get; }

    // â”€â”€ Coupling services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Lighting-to-drive coupling table loaded from SimConfig.lighting.driveCouplings.</summary>
    public LightingDriveCouplingTable DriveCouplingTable { get; }

    /// <summary>Fractional drive accumulator shared by LightingToDriveCouplingSystem.</summary>
    public SocialDriveAccumulator DriveAccumulator { get; }

    // â”€â”€ Structural change services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Structural topology change bus. Subscribe to receive events when obstacles, doors, or room bounds change.</summary>
    public StructuralChangeBus StructuralBus { get; }

    /// <summary>Pathfinding cache keyed by (query, topologyVersion). Cleared on every structural change.</summary>
    public PathfindingCache PathfindingCache { get; }

    /// <summary>Public mutation API for runtime structural topology changes.</summary>
    public IWorldMutationApi MutationApi { get; }

    // â”€â”€ Narrative services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Narrative event bus. Subscribe to receive candidates emitted each tick.</summary>
    public NarrativeEventBus NarrativeBus { get; }

    // â”€â”€ Chronicle services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Global persistent narrative chronicle. Read by TelemetryProjector each tick.</summary>
    public ChronicleService Chronicle { get; }

    // â”€â”€ Dialog services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Loaded phrase corpus. Null when the corpus file could not be located at boot.
    /// Dialog systems are skipped when null.
    /// </summary>
    public DialogCorpusService? CorpusService { get; private set; }

    /// <summary>Queue shared between DialogContextDecisionSystem and DialogFragmentRetrievalSystem.</summary>
    public PendingDialogQueue PendingDialogQueue { get; }

    /// <summary>Singleton pathfinding service â€” computes A* paths on demand.</summary>
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

    /// <summary>
    /// Primary constructor â€” accepts any <see cref="IConfigProvider"/>.
    /// Use this for tests (InMemoryConfigProvider) and Unity (custom provider).
    /// Builds every service singleton, registers every system, then either loads
    /// the world from <paramref name="worldDefinitionPath"/> or spawns the default
    /// 10Ã—10 apartment via <see cref="SpawnWorld"/>.
    /// </summary>
    /// <param name="configProvider">Config source â€” typically a <see cref="FileConfigProvider"/> in production.</param>
    /// <param name="humanCount">
    /// How many human entities to spawn on startup when no world definition is given.
    /// Default is <see cref="DefaultHumanCount"/> (100 â€” full stress-test world).
    /// Pass 1 for isolated single-entity tests; pass 0 to spawn no humans
    /// (useful for world-object-only unit tests).
    /// </param>
    /// <param name="seed">
    /// RNG seed for deterministic replay. Two runs with the same seed, config, and
    /// command log produce byte-identical telemetry streams. Defaults to 0.
    /// </param>
    /// <param name="worldDefinitionPath">
    /// Optional path to a world-definition.json. When supplied, the world is loaded
    /// via <see cref="WorldDefinitionLoader"/> and the cast is generated through
    /// <see cref="CastGenerator.SpawnAll"/>; <paramref name="humanCount"/> is ignored.
    /// </param>
    public SimulationBootstrapper(IConfigProvider configProvider, int humanCount = DefaultHumanCount, int seed = 0, string? worldDefinitionPath = null)
    {
        Config          = configProvider.GetConfig();
        EntityManager   = new EntityManager();
        Clock           = new SimulationClock { TimeScale = Config.World.DefaultTimeScale };
        Engine          = new SimulationEngine(EntityManager, Clock);
        Random          = new SeededRandom(seed);
        WillpowerEvents = new WillpowerEventQueue();

        // Spatial services â€” instantiated before RegisterSystems so systems can receive them
        SpatialIndex   = new GridSpatialIndex(Config.Spatial);
        ProximityBus   = new ProximityEventBus();
        StructuralBus  = new StructuralChangeBus();
        RoomMembership = new EntityRoomMembership();

        // Structural change services â€” bus and cache before PathfindingService
        PathfindingCache = new PathfindingCache(Config.Movement.Pathfinding.CacheMaxEntries);
        MutationApi      = new WorldMutationApi(EntityManager, StructuralBus);

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
            Config.Movement,
            PathfindingCache,
            StructuralBus);

        // Chronicle services â€” created before Invariants so the check can be injected.
        Chronicle = new ChronicleService(Config.Chronicle.MaxEntries);

        // Invariants â€” receives Chronicle for the chronicle â†” entity-tree agreement check.
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
            { Console.WriteLine($"[Dialog] Corpus load failed: {ex.Message} â€” dialog systems disabled."); }
        }
        else
        {
            Console.WriteLine($"[Dialog] Corpus file '{Config.Dialog.CorpusPath}' not found â€” dialog systems disabled.");
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
    /// Convenience overload â€” loads config from the JSON file at <paramref name="configPath"/>.
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

    // Load-path constructor — same service wiring as the primary constructor but skips SpawnWorld.
    private SimulationBootstrapper(IConfigProvider configProvider, int seed, bool _)
    {
        Config          = configProvider.GetConfig();
        EntityManager   = new EntityManager();
        Clock           = new SimulationClock { TimeScale = Config.World.DefaultTimeScale };
        Engine          = new SimulationEngine(EntityManager, Clock);
        Random          = new SeededRandom(seed);
        WillpowerEvents = new WillpowerEventQueue();

        SpatialIndex   = new GridSpatialIndex(Config.Spatial);
        ProximityBus   = new ProximityEventBus();
        StructuralBus  = new StructuralChangeBus();
        RoomMembership = new EntityRoomMembership();

        PathfindingCache = new PathfindingCache(Config.Movement.Pathfinding.CacheMaxEntries);
        MutationApi      = new WorldMutationApi(EntityManager, StructuralBus);
        SunState         = new SunStateService();
        DriveCouplingTable = new LightingDriveCouplingTable(Config.Lighting.DriveCouplings);
        DriveAccumulator   = new SocialDriveAccumulator();

        Pathfinding = new PathfindingService(
            EntityManager,
            Config.Spatial.WorldSize.Width,
            Config.Spatial.WorldSize.Height,
            Config.Movement,
            PathfindingCache,
            StructuralBus);

        Chronicle  = new ChronicleService(Config.Chronicle.MaxEntries);
        Invariants = new InvariantSystem(Clock, Chronicle);
        NarrativeBus = new NarrativeEventBus();

        PendingDialogQueue = new PendingDialogQueue();
        var corpusPath = DialogCorpusService.FindCorpusFile(Config.Dialog.CorpusPath);
        if (corpusPath != null)
        {
            try   { CorpusService = DialogCorpusService.LoadFromFile(corpusPath); }
            catch (Exception ex)
            { Console.WriteLine($"[Dialog] Corpus load failed: {ex.Message} — dialog systems disabled."); }
        }

        RegisterSystems();
    }

    /// <summary>
    /// Boots a simulation from a v0.5 <see cref="Warden.Contracts.Telemetry.WorldStateDto"/>
    /// save document, restoring all persistent component state.
    /// </summary>
    public static SimulationBootstrapper BootFromWorldStateDto(
        Warden.Contracts.Telemetry.WorldStateDto dto, IConfigProvider configProvider)
    {
        var sim = new SimulationBootstrapper(configProvider, dto.Seed ?? 0, false);

        if (dto.SaveTick.HasValue && dto.SaveTotalTime.HasValue && dto.SaveTimeScale.HasValue)
            sim.Clock.RestoreState(dto.SaveTotalTime.Value, dto.SaveTick.Value, dto.SaveTimeScale.Value);

        if (dto.EntityIdCounter.HasValue)
            sim.EntityManager.RestoreIdCounter(dto.EntityIdCounter.Value);

        if (dto.NpcSaveStates != null)
            foreach (var npc in dto.NpcSaveStates)
                RestoreNpcEntity(npc, sim.EntityManager, sim.Config);

        if (dto.TaskEntities != null)
            foreach (var task in dto.TaskEntities)
                RestoreTaskEntity(task, sim.EntityManager);

        if (dto.StainEntities != null)
            foreach (var stain in dto.StainEntities)
                RestoreStainEntity(stain, sim.EntityManager);

        if (dto.LockedDoors != null)
            foreach (var door in dto.LockedDoors)
                RestoreLockedDoorEntity(door, sim.EntityManager);

        return sim;
    }

    // ── Save/load entity restoration ─────────────────────────────────────────

    private static void RestoreNpcEntity(
        Warden.Contracts.Telemetry.NpcSaveDto npc,
        EntityManager em,
        SimConfig cfg)
    {
        var entity = em.CreateEntity(Guid.Parse(npc.Id));
        entity.Add(new IdentityComponent { Name = npc.Name });
        entity.Add(new PositionComponent { X = npc.PosX, Y = npc.PosY, Z = npc.PosZ });

        var tags      = npc.Tags;
        bool isHuman  = tags != null && tags.Contains("Human");
        bool isCat    = tags != null && tags.Contains("Cat");
        bool isCorpse = tags != null && tags.Contains("Corpse");

        if (!isCorpse)
        {
            var ecfg = isCat ? cfg.Entities.Cat : cfg.Entities.Human;
            entity.Add(new MetabolismComponent
            {
                Satiation                 = npc.Satiation,
                Hydration                 = npc.Hydration,
                BodyTemp                  = npc.BodyTemp,
                SatiationDrainRate        = npc.SatiationDrainRate,
                HydrationDrainRate        = npc.HydrationDrainRate,
                SleepMetabolismMultiplier = ecfg.Metabolism.SleepMetabolismMultiplier,
                NutrientStores            = new NutrientProfile(),
            });
            entity.Add(new EnergyComponent
            {
                Energy              = npc.Energy,
                Sleepiness          = npc.Sleepiness,
                IsSleeping          = npc.IsSleeping,
                EnergyDrainRate     = ecfg.Energy.EnergyDrainRate,
                SleepinessGainRate  = ecfg.Energy.SleepinessGainRate,
                EnergyRestoreRate   = ecfg.Energy.EnergyRestoreRate,
                SleepinessDrainRate = ecfg.Energy.SleepinessDrainRate,
            });
            entity.Add(new StomachComponent
            {
                CurrentVolumeMl = npc.StomachVolumeMl,
                DigestionRate   = ecfg.Stomach.DigestionRate,
                NutrientsQueued = new NutrientProfile(),
            });
            entity.Add(new SmallIntestineComponent
            {
                ChymeVolumeMl          = npc.SiChymeVolumeMl,
                AbsorptionRate         = ecfg.SmallIntestine.AbsorptionRate,
                Chyme                  = new NutrientProfile(),
                ResidueToLargeFraction = ecfg.SmallIntestine.ResidueToLargeFraction,
            });
            entity.Add(new LargeIntestineComponent
            {
                ContentVolumeMl       = npc.LiContentVolumeMl,
                WaterReabsorptionRate = ecfg.LargeIntestine.WaterReabsorptionRate,
                MobilityRate          = ecfg.LargeIntestine.MobilityRate,
                StoolFraction         = ecfg.LargeIntestine.StoolFraction,
            });
            entity.Add(new ColonComponent
            {
                StoolVolumeMl   = npc.ColonStoolVolumeMl,
                UrgeThresholdMl = ecfg.Colon.UrgeThresholdMl,
                CapacityMl      = ecfg.Colon.CapacityMl,
            });
            entity.Add(new BladderComponent
            {
                VolumeML        = npc.BladderVolumeMl,
                FillRate        = ecfg.Bladder.FillRate,
                UrgeThresholdMl = ecfg.Bladder.UrgeThresholdMl,
                CapacityMl      = ecfg.Bladder.CapacityMl,
            });
            entity.Add(new MovementComponent { Speed = isCat ? 0.06f : 0.04f, ArrivalDistance = 0.4f });
        }

        if (npc.LifeState is { } ls)
            entity.Add(new LifeStateComponent
            {
                State                   = (LifeState)ls.State,
                LastTransitionTick      = ls.LastTransitionTick,
                IncapacitatedTickBudget = ls.IncapacitatedTickBudget,
                PendingDeathCause       = (CauseOfDeath)ls.PendingDeathCause,
            });

        if (npc.Choking is { } ch)
            entity.Add(new ChokingComponent
            {
                ChokeStartTick = ch.ChokeStartTick,
                RemainingTicks = ch.RemainingTicks,
                BolusSize      = ch.BolusSize,
                PendingCause   = (CauseOfDeath)ch.PendingCause,
            });

        if (npc.Fainting is { } fa)
            entity.Add(new FaintingComponent { FaintStartTick = fa.FaintStartTick, RecoveryTick = fa.RecoveryTick });

        if (npc.LockedIn is { } loc)
            entity.Add(new LockedInComponent
            {
                FirstDetectedTick    = loc.FirstDetectedTick,
                StarvationTickBudget = loc.StarvationTickBudget,
            });

        if (npc.CauseOfDeath is { } cod)
            entity.Add(new CauseOfDeathComponent
            {
                Cause            = (CauseOfDeath)cod.Cause,
                DeathTick        = cod.DeathTick,
                WitnessedByNpcId = Guid.Parse(cod.WitnessedById),
                LocationRoomId   = Guid.Parse(cod.LocationRoomId),
            });

        if (npc.Corpse is { } cp)
            entity.Add(new CorpseComponent
            {
                DeathTick           = cp.DeathTick,
                OriginalNpcEntityId = Guid.Parse(cp.OriginalNpcEntityId),
                LocationRoomId      = cp.LocationRoomId,
                HasBeenMoved        = cp.HasBeenMoved,
            });

        if (npc.Stress is { } st)
            entity.Add(new StressComponent
            {
                AcuteLevel                = st.AcuteLevel,
                ChronicLevel              = st.ChronicLevel,
                LastDayUpdated            = st.LastDayUpdated,
                SuppressionEventsToday    = st.SuppressionEventsToday,
                DriveSpikeEventsToday     = st.DriveSpikeEventsToday,
                SocialConflictEventsToday = st.SocialConflictEventsToday,
                OverdueTaskEventsToday    = st.OverdueTaskEventsToday,
                WitnessedDeathEventsToday = st.WitnessedDeathEventsToday,
                BereavementEventsToday    = st.BereavementEventsToday,
                BurnoutLastAppliedDay     = st.BurnoutLastAppliedDay,
            });

        if (npc.Mask is { } mk)
            entity.Add(new SocialMaskComponent
            {
                IrritationMask = mk.IrritationMask,
                AffectionMask  = mk.AffectionMask,
                AttractionMask = mk.AttractionMask,
                LonelinessMask = mk.LonelinessMask,
                CurrentLoad    = mk.CurrentLoad,
                Baseline       = mk.Baseline,
                LastSlipTick   = mk.LastSlipTick,
            });

        if (npc.Mood is { } mo)
            entity.Add(new MoodComponent
            {
                Joy          = mo.Joy,
                Trust        = mo.Trust,
                Fear         = mo.Fear,
                Surprise     = mo.Surprise,
                Sadness      = mo.Sadness,
                Disgust      = mo.Disgust,
                Anger        = mo.Anger,
                Anticipation = mo.Anticipation,
                PanicLevel   = mo.PanicLevel,
                GriefLevel   = mo.GriefLevel,
            });

        if (npc.Willpower is { } wp)
            entity.Add(new WillpowerComponent(wp.Current, wp.Baseline));

        if (npc.Workload is { } wl)
            entity.Add(new WorkloadComponent
            {
                ActiveTasks = wl.ActiveTaskIds.Select(Guid.Parse).ToArray(),
                Capacity    = wl.Capacity,
                CurrentLoad = wl.CurrentLoad,
            });

        if (npc.EncounteredCorpseIds is { Count: > 0 } cids)
            entity.Add(new BereavementHistoryComponent
            {
                EncounteredCorpseIds = new HashSet<Guid>(cids.Select(Guid.Parse)),
            });

        if (npc.ScheduleBlocks is { Count: > 0 } sched)
            entity.Add(new ScheduleComponent
            {
                Blocks = sched.Select(b => new ScheduleBlock(
                    b.StartHour, b.EndHour, b.AnchorId,
                    (ScheduleActivityKind)b.Activity)).ToList(),
            });

        if (isHuman)                                     entity.Add(new HumanTag());
        if (isCat)                                       entity.Add(new CatTag());
        if (tags != null && tags.Contains("Npc"))        entity.Add(new NpcTag());
        if (isCorpse)                                    entity.Add(new CorpseTag());
        if (tags != null && tags.Contains("IsChoking"))  entity.Add(new IsChokingTag());
        if (tags != null && tags.Contains("IsFainting")) entity.Add(new IsFaintingTag());
        if (tags != null && tags.Contains("Sleeping"))   entity.Add(new SleepingTag());
    }

    private static void RestoreTaskEntity(
        Warden.Contracts.Telemetry.TaskSaveDto task, EntityManager em)
    {
        var entity = em.CreateEntity(Guid.Parse(task.Id));
        entity.Add(new TaskTag());
        entity.Add(new TaskComponent
        {
            EffortHours   = task.EffortHours,
            DeadlineTick  = task.DeadlineTick,
            Priority      = task.Priority,
            Progress      = task.Progress,
            QualityLevel  = task.QualityLevel,
            AssignedNpcId = Guid.Parse(task.AssignedNpcId),
            CreatedTick   = task.CreatedTick,
        });
        if (task.IsOverdue) entity.Add(new OverdueTag());
    }

    private static void RestoreStainEntity(
        Warden.Contracts.Telemetry.StainEntitySaveDto stain, EntityManager em)
    {
        var entity = em.CreateEntity(Guid.Parse(stain.Id));
        entity.Add(new StainTag());
        entity.Add(new PositionComponent { X = stain.PosX, Y = stain.PosY, Z = stain.PosZ });
        entity.Add(new StainComponent
        {
            Source           = stain.Source,
            Magnitude        = stain.Magnitude,
            CreatedAtTick    = stain.CreatedAtTick,
            ChronicleEntryId = stain.ChronicleEntryId,
        });
        if (stain.FallRiskLevel.HasValue)
            entity.Add(new FallRiskComponent { RiskLevel = stain.FallRiskLevel.Value });
        if (stain.Magnitude >= 50)
            entity.Add(new ObstacleTag());
    }

    private static void RestoreLockedDoorEntity(
        Warden.Contracts.Telemetry.LockedDoorSaveDto door, EntityManager em)
    {
        var entity = em.CreateEntity(Guid.Parse(door.Id));
        entity.Add(new LockedTag());
        entity.Add(new PositionComponent { X = door.PosX, Y = door.PosY, Z = door.PosZ });
        if (door.Name != null) entity.Add(new IdentityComponent { Name = door.Name });
    }

    /// <summary>
    /// Wires every simulation system into <see cref="Engine"/> in execution order.
    /// Phase ordering and per-system rationale are documented inline (do not add
    /// XML comments to the inline blocks below â€” they are implementation detail).
    /// </summary>
    /// <remarks>
    /// Called once from the constructor. Re-running it would double-register systems.
    /// See the class summary for the full pipeline.
    /// </remarks>
    private void RegisterSystems()
    {
        var sys = Config.Systems;

        // Spatial â€” sync index, room membership, cache invalidation (Phase 5).
        // ProximityEvent moves to Lighting (Phase 7) so it fires after illumination is current.
        var syncSys = new SpatialIndexSyncSystem(SpatialIndex, StructuralBus);
        EntityManager.EntityDestroyed += syncSys.OnEntityDestroyed;
        Engine.AddSystem(syncSys,                                                                SystemPhase.Spatial);
        Engine.AddSystem(new RoomMembershipSystem(RoomMembership, ProximityBus, StructuralBus), SystemPhase.Spatial);
        Engine.AddSystem(new PathfindingCacheInvalidationSystem(StructuralBus, PathfindingCache), SystemPhase.Spatial);

        // Lighting â€” sun position, source state machines, aperture beams, room illumination,
        // then proximity events (which now see current illumination).
        var lightSourceStates = new LightSourceStateSystem(Random, Config.Lighting);
        var apertureBeams     = new ApertureBeamSystem(SunState, Clock);
        Engine.AddSystem(new SunSystem(Clock, SunState, Config.Lighting),                           SystemPhase.Lighting);
        Engine.AddSystem(lightSourceStates,                                                          SystemPhase.Lighting);
        Engine.AddSystem(apertureBeams,                                                              SystemPhase.Lighting);
        Engine.AddSystem(new IlluminationAccumulationSystem(lightSourceStates, apertureBeams, Config.Lighting), SystemPhase.Lighting);
        Engine.AddSystem(new ProximityEventSystem(SpatialIndex, ProximityBus, RoomMembership),      SystemPhase.Lighting);

        // Coupling â€” lighting-to-drive coupling; after illumination is fresh, before drive dynamics.
        Engine.AddSystem(new LightingToDriveCouplingSystem(
            DriveCouplingTable, DriveAccumulator, RoomMembership, apertureBeams, SunState),          SystemPhase.Coupling);

        // PreUpdate â€” invariant enforcement; always first
        Engine.AddSystem(Invariants,                                               SystemPhase.PreUpdate);
        // Structural tagging: one-shot system at boot that attaches StructuralTag to obstacles/walls/doors
        Engine.AddSystem(new StructuralTaggingSystem(),                            SystemPhase.PreUpdate);
        // Schedule spawner: attach routines to NPCs that lack one (runs every tick, idempotent)
        Engine.AddSystem(new ScheduleSpawnerSystem(),                              SystemPhase.PreUpdate);

        // Stress initialization â€” attaches StressComponent to newly-spawned NPCs that lack one.
        Engine.AddSystem(
            new StressInitializerSystem(StressInitializerSystem.LoadBaselines()),  SystemPhase.PreUpdate);

        // Mask initialization â€” attaches SocialMaskComponent with personality-derived baseline.
        Engine.AddSystem(new MaskInitializerSystem(),                              SystemPhase.PreUpdate);

        // Workload initialization â€” attaches WorkloadComponent with per-archetype capacity.
        Engine.AddSystem(
            new WorkloadInitializerSystem(WorkloadInitializerSystem.LoadCapacities()), SystemPhase.PreUpdate);

        // Life state initialization â€” attaches LifeStateComponent to newly-spawned NPCs with State == Alive.
        Engine.AddSystem(new LifeStateInitializerSystem(),                          SystemPhase.PreUpdate);

        // Task generation â€” spawns new task entities once per game-day at the configured hour.
        Engine.AddSystem(
            new TaskGeneratorSystem(Config.Workload, Clock, Random),               SystemPhase.PreUpdate);

        // Physiology â€” raw biological resource drain/restore
        Engine.AddSystem(new MetabolismSystem(),                                   SystemPhase.Physiology);
        Engine.AddSystem(new EnergySystem(sys.Energy),                            SystemPhase.Physiology);
        Engine.AddSystem(new BladderFillSystem(),                                  SystemPhase.Physiology);

        // Condition â€” derive sensation tags from physiology values
        Engine.AddSystem(new BiologicalConditionSystem(sys.BiologicalCondition),  SystemPhase.Condition);
        // Schedule: resolve active block before ActionSelectionSystem reads it.
        Engine.AddSystem(new ScheduleSystem(Clock),                                SystemPhase.Condition);

        // Cognition â€” process conditions into emotions and drive scores
        Engine.AddSystem(new MoodSystem(sys.Mood),                                SystemPhase.Cognition);
        Engine.AddSystem(new BrainSystem(sys.Brain, Clock),                       SystemPhase.Cognition);

        // Physiology gate â€” veto set computed after BrainSystem, before Behavior systems act.
        // PhysiologyGateSystem writes BlockedActionsComponent for each NPC with inhibitions.
        Engine.AddSystem(new PhysiologyGateSystem(Config.PhysiologyGate),                  SystemPhase.Cognition);

        // Social cognition â€” drive dynamics, action selection, willpower, relationship lifecycle
        Engine.AddSystem(new DriveDynamicsSystem(Config.Social, Clock, Random, Config.Stress), SystemPhase.Cognition);
        Engine.AddSystem(new ActionSelectionSystem(
            SpatialIndex, RoomMembership, WillpowerEvents, Random, Config.ActionSelection, Config.Schedule, EntityManager, Config.Workload),
                                                                                   SystemPhase.Cognition);
        Engine.AddSystem(new WillpowerSystem(Config.Social, WillpowerEvents),      SystemPhase.Cognition);
        Engine.AddSystem(RelationshipLifecycleSystem.LoadFromFile(Config.Social),  SystemPhase.Cognition);
        Engine.AddSystem(new SocialMaskSystem(RoomMembership, Config.SocialMask),  SystemPhase.Cognition);

        // Behavior â€” act on the dominant drive
        Engine.AddSystem(new FeedingSystem(sys.Feeding),                          SystemPhase.Behavior);
        Engine.AddSystem(new DrinkingSystem(sys.Drinking),                        SystemPhase.Behavior);
        Engine.AddSystem(new SleepSystem(sys.Sleep),                              SystemPhase.Behavior);
        Engine.AddSystem(new DefecationSystem(),                                  SystemPhase.Behavior);
        Engine.AddSystem(new UrinationSystem(),                                    SystemPhase.Behavior);

        // Transit â€” move content through the upper digestive pipeline
        Engine.AddSystem(new InteractionSystem(sys.Interaction),                  SystemPhase.Transit);
        Engine.AddSystem(new EsophagusSystem(),                                   SystemPhase.Transit);
        Engine.AddSystem(new DigestionSystem(sys.Digestion),                      SystemPhase.Transit);

        // Elimination â€” lower digestive pipeline; intestines â†’ colon/bladder â†’ tags
        Engine.AddSystem(new SmallIntestineSystem(),                              SystemPhase.Elimination);
        Engine.AddSystem(new LargeIntestineSystem(),                              SystemPhase.Elimination);
        Engine.AddSystem(new ColonSystem(),                                       SystemPhase.Elimination);
        Engine.AddSystem(new BladderSystem(),                                     SystemPhase.Elimination);

        // World â€” environmental systems independent of entity biology
        Engine.AddSystem(new RotSystem(sys.Rot),                                  SystemPhase.World);

        // Movement quality pipeline (runs in World phase, in registration order)
        Engine.AddSystem(new PathfindingTriggerSystem(Pathfinding),                SystemPhase.World);
        Engine.AddSystem(new MovementSpeedModifierSystem(Config.Movement),         SystemPhase.World);
        Engine.AddSystem(new StepAsideSystem(SpatialIndex, RoomMembership, Config.Movement), SystemPhase.World);
        Engine.AddSystem(new MovementSystem(Random),                               SystemPhase.World);
        Engine.AddSystem(new FacingSystem(ProximityBus),                           SystemPhase.World);
        Engine.AddSystem(new IdleMovementSystem(Random, Config.Movement),          SystemPhase.World);

        // Narrative â€” runs last so all state has settled; emits candidates via NarrativeBus
        Engine.AddSystem(new NarrativeEventDetector(
            NarrativeBus, ProximityBus, RoomMembership, Config.Narrative),        SystemPhase.Narrative);

        // Chronicle â€” evaluates candidates emitted this tick; must run after NarrativeEventDetector.
        Engine.AddSystem(new PersistenceThresholdDetector(
            Chronicle, NarrativeBus, EntityManager, Clock, Random, Config.Chronicle), SystemPhase.Narrative);

        // Memory recording â€” subscribes to the bus and routes candidates to per-pair/personal buffers.
        Engine.AddSystem(new MemoryRecordingSystem(NarrativeBus, EntityManager, Config.Memory), SystemPhase.Narrative);

        // Dialog â€” after Narrative so final drive state is visible
        if (CorpusService != null)
        {
            var decisionSys   = new DialogContextDecisionSystem(PendingDialogQueue, ProximityBus, Config.Dialog, Random);
            var retrievalSys  = new DialogFragmentRetrievalSystem(PendingDialogQueue, CorpusService, ProximityBus, Config.Dialog);
            var calcifySys    = new DialogCalcifySystem(Config.Dialog);
            Engine.AddSystem(decisionSys,  SystemPhase.Dialog);
            Engine.AddSystem(retrievalSys, SystemPhase.Dialog);
            Engine.AddSystem(calcifySys,   SystemPhase.Dialog);
        }

        // Cleanup â€” stress accumulation; runs after WillpowerSystem (Cognition) and
        // NarrativeEventDetector (Narrative) so all tick state has settled.
        Engine.AddSystem(
            new StressSystem(Config.Stress, Config.Workload, Clock, WillpowerEvents, NarrativeBus, EntityManager), SystemPhase.Cleanup);

        // Workload system â€” advances task progress, detects completion and overdue.
        Engine.AddSystem(
            new WorkloadSystem(Config.Workload, Clock, NarrativeBus, EntityManager), SystemPhase.Cleanup);

        // Mask crack detection â€” runs at Cleanup so it fires after ActionSelectionSystem has
        // written its intent; the crack override wins for the following Dialog phase.
        Engine.AddSystem(
            new MaskCrackSystem(RoomMembership, NarrativeBus, Config.SocialMask),  SystemPhase.Cleanup);

        // Create a single LifeStateTransitionSystem instance for both choking and life-state management.
        var lifeStateTransition = new LifeStateTransitionSystem(NarrativeBus, EntityManager, Clock, Config);

        // Choking detection â€” identifies choking conditions (bolus + distraction) and enqueues transition to Incapacitated.
        // Runs after EsophagusSystem (in Transit) so the bolus has had its chance to advance,
        // and before LifeStateTransitionSystem so the request reaches the queue this tick.
        Engine.AddSystem(
            new ChokingDetectionSystem(
                lifeStateTransition,
                NarrativeBus,
                Clock,
                Config.Choking,
                EntityManager),
            SystemPhase.Cleanup);

        // Life state transitions â€” processes queued state changes (Aliveâ†’Incapacitatedâ†’Deceased);
        // runs after WorkloadSystem and MaskCrackSystem so all cognitive ticking is complete.
        Engine.AddSystem(lifeStateTransition, SystemPhase.Cleanup);

        // Choking cleanup â€” removes IsChokingTag and ChokingComponent when NPC transitions to Deceased.
        // Runs at the very end of Cleanup phase (after LifeStateTransitionSystem).
        Engine.AddSystem(
            new ChokingCleanupSystem(), SystemPhase.Cleanup);

        // Slip-and-fall detection â€” rolls hazard checks for NPCs on tiles with FallRiskComponent.
        // Runs in Cleanup phase after MovementSystem (so NPCs have settled position) and
        // before LifeStateTransitionSystem (so transition requests hit the queue this tick).
        Engine.AddSystem(
            new SlipAndFallSystem(
                EntityManager,
                Clock,
                Config,
                lifeStateTransition,
                Random),
            SystemPhase.Cleanup);

        // Lockout detection â€” checks end-of-day reachability to exits and starvation status.
        // Runs in PreUpdate phase, once per game-day (gated internally by hour check).
        Engine.AddSystem(
            new LockoutDetectionSystem(
                EntityManager,
                Clock,
                Config,
                Pathfinding,
                lifeStateTransition,
                Random),
            SystemPhase.PreUpdate);
    }

    // â”€â”€ Human count â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    /// <summary>
    /// Default number of humans spawned when no <c>humanCount</c> argument is
    /// supplied.  100 gives a realistic stress-test world; pass 1 to
    /// <see cref="SimulationBootstrapper(IConfigProvider,int)"/> for isolated
    /// single-entity tests.
    /// </summary>
    public const int DefaultHumanCount = 100;

    /// <summary>
    /// Spawns the default 10Ã—10 apartment â€” a humanCount-sized human grid plus
    /// fixed-position fridge, sink, bed, and toilet world objects. Used when no
    /// world definition file was supplied.
    /// </summary>
    /// <param name="humanCount">How many humans to lay out on the grid.</param>
    private void SpawnWorld(int humanCount)
    {
        // â”€â”€ Living entities â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Spread humanCount humans on a uniform grid inside the 10Ã—10 world.
        // humanCount = 1  â†’ single Billy at centre (5, 5).
        // humanCount = 100 â†’ 10Ã—10 grid from (1,1) to (9,9).
        SpawnHumanGrid(humanCount);

        // â”€â”€ World objects â€” 10Ã—10 unit apartment â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //   Fridge  (2, 0, 2)  NW â€” kitchen
        //   Sink    (7, 0, 2)  NE â€” kitchen
        //   Bed     (2, 0, 8)  SW â€” bedroom
        //   Toilet  (7, 0, 8)  SE â€” bathroom
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
    /// Spreads <paramref name="count"/> humans evenly on a rectangular grid
    /// inside the 10Ã—10 default world. For count = 1 the single entity lands
    /// at centre (5, 5) and is named "Billy".
    /// </summary>
    /// <param name="count">Number of humans to spawn. Values â‰¤ 0 are a no-op.</param>
    private void SpawnHumanGrid(int count)
    {
        if (count <= 0) return;

        if (count == 1)
        {
            EntityTemplates.SpawnHuman(EntityManager, Config.Entities.Human,
                spawnX: 5f, spawnZ: 5f, name: "Billy");
            return;
        }

        // cols Ã— rows â‰¥ count, shaped as square as possible.
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

    /// <summary>
    /// Creates a single static world object â€” Identity, Position, and a default
    /// instance of <typeparamref name="TTag"/> as the marker component (e.g.
    /// <see cref="SinkComponent"/>, <see cref="BedComponent"/>, â€¦).
    /// </summary>
    /// <typeparam name="TTag">Marker component type identifying the object kind.</typeparam>
    /// <param name="name">Human-readable name written to the entity's <see cref="IdentityComponent"/>.</param>
    /// <param name="x">World X coordinate (tiles).</param>
    /// <param name="y">World Y coordinate (tiles, vertical).</param>
    /// <param name="z">World Z coordinate (tiles).</param>
    private void SpawnWorldObject<TTag>(string name, float x, float y, float z)
        where TTag : struct
    {
        var e = EntityManager.CreateEntity();
        e.Add(new IdentityComponent { Name = name });
        e.Add(new PositionComponent { X = x, Y = y, Z = z });
        e.Add(default(TTag));
    }

    // â”€â”€ Snapshot â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Captures the current engine state as an immutable SimulationSnapshot.
    /// Call once per frame from any frontend's render loop; hand the result to
    /// every system that needs to display or log it.
    ///
    /// Usage:
    ///   var snap = sim.Capture();
    ///   Console.WriteLine(snap.Clock.TimeDisplay);
    /// </summary>
    /// <returns>An immutable snapshot of the current simulation state.</returns>
    public SimulationSnapshot Capture() => SimulationSnapshot.Capture(this);

    // â”€â”€ Hot-reload â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Pushes all tuning values from <paramref name="newCfg"/> into the live running
    /// simulation without restarting. Systems immediately use the new values on the
    /// next tick.
    ///
    /// Works by mutating the EXISTING config objects in-place, so all system
    /// constructor references remain valid â€” no system pointer changes hands.
    ///
    /// Thread note: call this on the same thread that drives Engine.Update() to
    /// avoid a tick reading partially-applied config.  Both the CLI (flag check in
    /// the main loop) and the Avalonia GUI (DispatcherTimer on the UI thread) already
    /// satisfy this requirement.
    /// </summary>
    /// <param name="newCfg">Freshly loaded config whose primitive values are merged into the live <see cref="Config"/>.</param>
    /// <remarks>
    /// Only flat (value-type) properties are merged. Nested object identities are
    /// preserved â€” see <see cref="MergeFlat{T}"/>. Entity starting values
    /// (e.g. <c>EnergyStart</c>) take effect for entities spawned AFTER the reload.
    /// </remarks>
    public void ApplyConfig(SimConfig newCfg)
    {
        var changes = new List<string>();

        // â”€â”€ System configs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        // â”€â”€ Entity starting configs (only affect future spawns) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
            Console.WriteLine("[Config] Reloaded â€” no values changed.");
        }
        else
        {
            Console.WriteLine($"[Config] Reloaded â€” {changes.Count} value(s) changed:");
            foreach (var c in changes)
                Console.WriteLine($"         {c}");
        }
    }

    /// <summary>
    /// Copies all primitive (value-type) public properties from <paramref name="src"/>
    /// onto <paramref name="dst"/> in-place, logging any that actually changed.
    /// Reference-type properties (nested objects) are intentionally skipped â€”
    /// they must be merged separately to preserve object identity.
    /// </summary>
    /// <typeparam name="T">Reference type of the config object being merged.</typeparam>
    /// <param name="src">Newly loaded config object whose primitive values are read.</param>
    /// <param name="dst">Live config object whose primitive values are mutated in place.</param>
    /// <param name="changes">Sink for "Type.Prop  old â†’ new" change descriptions, one per modified property.</param>
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
                changes.Add($"{typeof(T).Name}.{prop.Name}  {oldVal} â†’ {newVal}");
            }
        }
    }
}
