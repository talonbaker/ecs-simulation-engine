using System;
using System.Collections.Generic;
using APIFramework.Bootstrap;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Mutation;
using APIFramework.Systems;
using APIFramework.Systems.Chores;
using APIFramework.Systems.Coupling;
using APIFramework.Systems.Dialog;
using APIFramework.Systems.Lighting;
using APIFramework.Systems.Movement;
using APIFramework.Systems.Chronicle;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;
using APIFramework.Systems.LifeState;
using System.Linq;
using System.Reflection;
using Warden.Contracts.Telemetry;

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
///  PreUpdate   (0)  StructuralTaggingSystem   — one-shot: tag obstacles/walls/doors at boot
///  PreUpdate   (0)  ScheduleSpawnerSystem     — attach default routines to scheduleless NPCs
///  PreUpdate   (0)  StressInitializerSystem   — attach StressComponent to fresh NPCs
///  PreUpdate   (0)  MaskInitializerSystem     — attach SocialMaskComponent (personality baseline)
///  PreUpdate   (0)  WorkloadInitializerSystem — attach WorkloadComponent (per-archetype capacity)
///  PreUpdate   (0)  LifeStateInitializerSystem— attach LifeStateComponent = Alive
///  PreUpdate   (0)  TaskGeneratorSystem       — spawn day's task batch at configured hour
///  PreUpdate   (0)  ChoreInitializerSystem    — WP-3.2.3: spawn chore entities; attach ChoreHistoryComponent
///  PreUpdate   (0)  ChoreAssignmentSystem     — WP-3.2.3: daily chore assignment at configured hour
///  PreUpdate   (0)  LockoutDetectionSystem    — Phase 3: end-of-day exit reachability + starvation
///  Spatial     (5)  SpatialIndexSyncSystem    — keep spatial index in sync with positions
///  Spatial     (5)  RoomMembershipSystem      — derive per-NPC room residency
///  Spatial     (5)  PathfindingCacheInvalidationSystem — clear cache on structural change
///  Lighting    (7)  SunSystem                 — advance sun phase / day-phase boundary
///  Lighting    (7)  LightSourceStateSystem    — flicker/dying state machines
///  Lighting    (7)  ApertureBeamSystem        — compute aperture beams from sun + clock
///  Lighting    (7)  IlluminationAccumulationSystem — combine source + aperture per room
///  Lighting    (7)  ProximityEventSystem      — emit proximity signals against current illumination
///  Coupling    (8)  LightingToDriveCouplingSystem — accumulate lighting → drive deltas
///  Physiology  (10) MetabolismSystem          — drain satiation / hydration
///  Physiology  (10) EnergySystem              — drain/restore energy + sleepiness
///  Physiology  (10) BladderFillSystem         — fill bladder at constant rate
///  Condition   (20) BiologicalConditionSystem — set hunger/thirst/irritable tags
///  Condition   (20) ScheduleSystem            — resolve active block before ActionSelection reads it
///  Cognition   (30) MoodSystem                — decay emotions; apply Plutchik intensity tags
///  Cognition   (30) BrainSystem               — score drives (incl. circadian, colon, bladder); pick dominant
///  Cognition   (30) PhysiologyGateSystem      — write BlockedActionsComponent; inhibitions veto biology
///  Cognition   (30) DriveDynamicsSystem       — decay/circadian-modulate social drives
///  Cognition   (30) ActionSelectionSystem     — enumerate candidates, pick winner, write IntendedAction
///  Cognition   (30) WillpowerSystem           — apply suppression cost / regen
///  Cognition   (30) RelationshipLifecycleSystem — relationship intensity / lifecycle
///  Cognition   (30) SocialMaskSystem          — drift mask in public, decay in private
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
///  World       (60) PathfindingTriggerSystem  — kick off A* requests for movement intents
///  World       (60) MovementSpeedModifierSystem — derive per-NPC speed multiplier
///  World       (60) StepAsideSystem           — perpendicular shift on near-miss
///  World       (60) MovementSystem            — advance positions along paths
///  World       (60) FacingSystem              — update facing from proximity signals
///  World       (60) IdleMovementSystem        — jitter/posture for idle NPCs
///  Narrative   (70) NarrativeEventDetector    — emit narrative candidates this tick
///  Narrative   (70) PersistenceThresholdDetector — promote candidates to chronicle entries
///  Narrative   (70) MemoryRecordingSystem     — route candidates to per-pair / personal memory buffers
///  Dialog      (80) DialogContextDecisionSystem    — choose context, queue dialog attempts
///  Dialog      (80) DialogFragmentRetrievalSystem  — pick fragments from corpus
///  Dialog      (80) DialogCalcifySystem            — promote/decalcify catchphrases
///  Cleanup     (90) StressSystem              — accumulate acute/chronic stress, apply tags
///  Cleanup     (90) WorkloadSystem            — advance task progress; detect completion / overdue
///  Cleanup     (90) MaskCrackSystem           — Phase 3: emit MaskCrack when pressure exceeds threshold
///  Cleanup     (90) ChokingDetectionSystem    — Phase 3: bolus + distraction → enqueue Incapacitated
///  Cleanup     (90) LifeStateTransitionSystem — Phase 3: Alive → Incapacitated → Deceased
///  Cleanup     (90) ChokingCleanupSystem      — Phase 3: clear choke tags after death
///  Cleanup     (90) SlipAndFallSystem         — Phase 3: roll fall-risk hazards on settled positions
///  Cleanup     (90) ChoreExecutionSystem      — WP-3.2.3: advance chore progress; detect completion / overrotation
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
    /// the chronicle ↔ entity-tree agreement check each tick.
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

    // ── Structural change services ────────────────────────────────────────────

    /// <summary>Structural topology change bus. Subscribe to receive events when obstacles, doors, or room bounds change.</summary>
    public StructuralChangeBus StructuralBus { get; }

    /// <summary>Pathfinding cache keyed by (query, topologyVersion). Cleared on every structural change.</summary>
    public PathfindingCache PathfindingCache { get; }

    /// <summary>Public mutation API for runtime structural topology changes.</summary>
    public IWorldMutationApi MutationApi { get; }

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

    /// <summary>
    /// Primary constructor — accepts any <see cref="IConfigProvider"/>.
    /// Use this for tests (InMemoryConfigProvider) and Unity (custom provider).
    /// Builds every service singleton, registers every system, then either loads
    /// the world from <paramref name="worldDefinitionPath"/> or spawns the default
    /// 10×10 apartment via <see cref="SpawnWorld"/>.
    /// </summary>
    /// <param name="configProvider">Config source — typically a <see cref="FileConfigProvider"/> in production.</param>
    /// <param name="humanCount">
    /// How many human entities to spawn on startup when no world definition is given.
    /// Default is <see cref="DefaultHumanCount"/> (100 — full stress-test world).
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

        // Spatial services — instantiated before RegisterSystems so systems can receive them
        SpatialIndex   = new GridSpatialIndex(Config.Spatial);
        ProximityBus   = new ProximityEventBus();
        StructuralBus  = new StructuralChangeBus();
        RoomMembership = new EntityRoomMembership();

        // Structural change services — bus and cache before PathfindingService
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


    /// <summary>
    /// Load-path constructor — wires all services and systems but spawns no entities.
    /// Callers must restore entities explicitly after construction.
    /// </summary>
    private SimulationBootstrapper(IConfigProvider configProvider, int seed, bool _loadPath)
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

        SunState           = new SunStateService();
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

        NarrativeBus      = new NarrativeEventBus();
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

    // ── Save / Load ───────────────────────────────────────────────────────────

    /// <summary>
    /// Constructs a fully-restored simulation from a save-game DTO.
    /// Clock, entity ID counter, and all persistent entities are rebuilt from the snapshot.
    /// </summary>
    public static SimulationBootstrapper BootFromWorldStateDto(WorldStateDto dto, IConfigProvider configProvider)
    {
        var sim = new SimulationBootstrapper(configProvider, dto.Seed ?? 0, true);

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

    private static void RestoreNpcEntity(NpcSaveDto dto, EntityManager em, SimConfig cfg)
    {
        var entity = Guid.TryParse(dto.Id, out var id) ? em.CreateEntity(id) : em.CreateEntity();

        entity.Add(new IdentityComponent { Name = dto.Name });
        if (dto.IsHuman) entity.Add(new HumanTag()); else entity.Add(new CatTag());
        entity.Add(new NpcTag());

        entity.Add(new PositionComponent { X = dto.PosX, Y = dto.PosY, Z = dto.PosZ });

        var m = dto.IsHuman ? cfg.Entities.Human.Metabolism : cfg.Entities.Cat.Metabolism;
        entity.Add(new MetabolismComponent
        {
            Satiation                 = dto.Satiation,
            Hydration                 = dto.Hydration,
            BodyTemp                  = dto.BodyTemp,
            SatiationDrainRate        = dto.SatiationDrainRate,
            HydrationDrainRate        = dto.HydrationDrainRate,
            SleepMetabolismMultiplier = m.SleepMetabolismMultiplier
        });

        var s = dto.IsHuman ? cfg.Entities.Human.Stomach : cfg.Entities.Cat.Stomach;
        entity.Add(new StomachComponent
        {
            CurrentVolumeMl = dto.StomachVolumeMl,
            DigestionRate   = s.DigestionRate
        });

        var e = dto.IsHuman ? cfg.Entities.Human.Energy : cfg.Entities.Cat.Energy;
        entity.Add(new EnergyComponent
        {
            Energy              = dto.Energy,
            Sleepiness          = dto.Sleepiness,
            IsSleeping          = dto.IsSleeping,
            EnergyDrainRate     = e.EnergyDrainRate,
            SleepinessGainRate  = e.SleepinessGainRate,
            EnergyRestoreRate   = e.EnergyRestoreRate,
            SleepinessDrainRate = e.SleepinessDrainRate
        });

        var si = dto.IsHuman ? cfg.Entities.Human.SmallIntestine : cfg.Entities.Cat.SmallIntestine;
        entity.Add(new SmallIntestineComponent
        {
            ChymeVolumeMl          = dto.SiChymeVolumeMl,
            AbsorptionRate         = si.AbsorptionRate,
            ResidueToLargeFraction = si.ResidueToLargeFraction
        });

        var li = dto.IsHuman ? cfg.Entities.Human.LargeIntestine : cfg.Entities.Cat.LargeIntestine;
        entity.Add(new LargeIntestineComponent
        {
            ContentVolumeMl       = dto.LiContentVolumeMl,
            WaterReabsorptionRate = li.WaterReabsorptionRate,
            MobilityRate          = li.MobilityRate,
            StoolFraction         = li.StoolFraction
        });

        var co = dto.IsHuman ? cfg.Entities.Human.Colon : cfg.Entities.Cat.Colon;
        entity.Add(new ColonComponent
        {
            StoolVolumeMl   = dto.ColonStoolVolumeMl,
            UrgeThresholdMl = co.UrgeThresholdMl,
            CapacityMl      = co.CapacityMl
        });

        var bl = dto.IsHuman ? cfg.Entities.Human.Bladder : cfg.Entities.Cat.Bladder;
        entity.Add(new BladderComponent
        {
            VolumeML        = dto.BladderVolumeMl,
            FillRate        = bl.FillRate,
            UrgeThresholdMl = bl.UrgeThresholdMl,
            CapacityMl      = bl.CapacityMl
        });

        entity.Add(new MovementComponent { Speed = dto.IsHuman ? 0.04f : 0.06f, ArrivalDistance = 0.4f });

        if (dto.LifeState != null)
            entity.Add(new LifeStateComponent
            {
                State                   = (LifeState)(int)dto.LifeState.State,
                LastTransitionTick      = dto.LifeState.LastTransitionTick,
                IncapacitatedTickBudget = dto.LifeState.IncapacitatedTickBudget,
                PendingDeathCause       = (CauseOfDeath)(int)dto.LifeState.PendingDeathCause
            });

        if (dto.Choking != null)
        {
            entity.Add(new ChokingComponent
            {
                ChokeStartTick = dto.Choking.ChokeStartTick,
                RemainingTicks = dto.Choking.RemainingTicks,
                BolusSize      = dto.Choking.BolusSize,
                PendingCause   = (CauseOfDeath)(int)dto.Choking.PendingCause
            });
            entity.Add(new IsChokingTag());
        }

        if (dto.Fainting != null)
        {
            entity.Add(new FaintingComponent
            {
                FaintStartTick = dto.Fainting.FaintStartTick,
                RecoveryTick   = dto.Fainting.RecoveryTick
            });
            entity.Add(new IsFaintingTag());
        }

        if (dto.LockedIn != null)
            entity.Add(new LockedInComponent
            {
                FirstDetectedTick    = dto.LockedIn.FirstDetectedTick,
                StarvationTickBudget = dto.LockedIn.StarvationTickBudget
            });

        if (dto.CauseOfDeath != null)
            entity.Add(new CauseOfDeathComponent
            {
                Cause            = (CauseOfDeath)(int)dto.CauseOfDeath.Cause,
                DeathTick        = dto.CauseOfDeath.DeathTick,
                WitnessedByNpcId = Guid.TryParse(dto.CauseOfDeath.WitnessedById,  out var wId) ? wId : Guid.Empty,
                LocationRoomId   = Guid.TryParse(dto.CauseOfDeath.LocationRoomId, out var rId) ? rId : Guid.Empty
            });

        if (dto.Corpse != null)
        {
            entity.Add(new CorpseTag());
            entity.Add(new CorpseComponent
            {
                DeathTick           = dto.Corpse.DeathTick,
                OriginalNpcEntityId = Guid.TryParse(dto.Corpse.OriginalNpcEntityId, out var origId) ? origId : Guid.Empty,
                LocationRoomId      = dto.Corpse.LocationRoomId,
                HasBeenMoved        = dto.Corpse.HasBeenMoved
            });
        }

        if (dto.Stress != null)
            entity.Add(new StressComponent
            {
                AcuteLevel                = dto.Stress.AcuteLevel,
                ChronicLevel              = dto.Stress.ChronicLevel,
                LastDayUpdated            = dto.Stress.LastDayUpdated,
                SuppressionEventsToday    = dto.Stress.SuppressionEventsToday,
                DriveSpikeEventsToday     = dto.Stress.DriveSpikeEventsToday,
                SocialConflictEventsToday = dto.Stress.SocialConflictEventsToday,
                OverdueTaskEventsToday    = dto.Stress.OverdueTaskEventsToday,
                BurnoutLastAppliedDay     = dto.Stress.BurnoutLastAppliedDay,
                WitnessedDeathEventsToday = dto.Stress.WitnessedDeathEventsToday,
                BereavementEventsToday    = dto.Stress.BereavementEventsToday
            });

        if (dto.Mask != null)
            entity.Add(new SocialMaskComponent
            {
                IrritationMask = dto.Mask.IrritationMask,
                AffectionMask  = dto.Mask.AffectionMask,
                AttractionMask = dto.Mask.AttractionMask,
                LonelinessMask = dto.Mask.LonelinessMask,
                CurrentLoad    = dto.Mask.CurrentLoad,
                Baseline       = dto.Mask.Baseline,
                LastSlipTick   = dto.Mask.LastSlipTick
            });

        if (dto.Mood != null)
            entity.Add(new MoodComponent
            {
                Joy          = dto.Mood.Joy,
                Trust        = dto.Mood.Trust,
                Fear         = dto.Mood.Fear,
                Surprise     = dto.Mood.Surprise,
                Sadness      = dto.Mood.Sadness,
                Disgust      = dto.Mood.Disgust,
                Anger        = dto.Mood.Anger,
                Anticipation = dto.Mood.Anticipation,
                PanicLevel   = dto.Mood.PanicLevel,
                GriefLevel   = dto.Mood.GriefLevel
            });

        if (dto.Willpower != null)
            entity.Add(new WillpowerComponent { Current = dto.Willpower.Current, Baseline = dto.Willpower.Baseline });

        if (dto.Workload != null)
        {
            var activeGuids = dto.Workload.ActiveTaskIds?
                .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .ToList() ?? new List<Guid>();
            entity.Add(new WorkloadComponent
            {
                Capacity    = dto.Workload.Capacity,
                CurrentLoad = dto.Workload.CurrentLoad,
                ActiveTasks = activeGuids
            });
        }

        if (dto.ScheduleBlocks != null && dto.ScheduleBlocks.Count > 0)
        {
            var blocks = dto.ScheduleBlocks.Select(b => new ScheduleBlock(
                b.StartHour,
                b.EndHour,
                b.AnchorId,
                (ScheduleActivityKind)(int)b.Activity)).ToList();
            entity.Add(new ScheduleComponent { Blocks = blocks });
        }

        if (dto.EncounteredCorpseIds != null && dto.EncounteredCorpseIds.Count > 0)
        {
            var ids = new HashSet<Guid>(dto.EncounteredCorpseIds
                .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty));
            entity.Add(new BereavementHistoryComponent { EncounteredCorpseIds = ids });
        }
    }

    private static void RestoreTaskEntity(TaskSaveDto dto, EntityManager em)
    {
        var entity = Guid.TryParse(dto.Id, out var id) ? em.CreateEntity(id) : em.CreateEntity();
        entity.Add(new TaskTag());
        entity.Add(new TaskComponent
        {
            EffortHours   = dto.EffortHours,
            DeadlineTick  = dto.DeadlineTick,
            Priority      = dto.Priority,
            Progress      = dto.Progress,
            QualityLevel  = dto.QualityLevel,
            AssignedNpcId = Guid.TryParse(dto.AssignedNpcId, out var nId) ? nId : Guid.Empty,
            CreatedTick   = dto.CreatedTick
        });
    }

    private static void RestoreStainEntity(StainEntitySaveDto dto, EntityManager em)
    {
        var entity = Guid.TryParse(dto.Id, out var id) ? em.CreateEntity(id) : em.CreateEntity();
        entity.Add(new StainTag());
        entity.Add(new PositionComponent { X = dto.PosX, Y = 0f, Z = dto.PosZ });
        entity.Add(new StainComponent { Source = dto.Source, Magnitude = dto.Magnitude, CreatedAtTick = dto.CreatedAtTick, ChronicleEntryId = dto.ChronicleEntryId });
        if (dto.FallRisk.HasValue)
            entity.Add(new FallRiskComponent { RiskLevel = dto.FallRisk.Value });
        if (dto.IsObstacle)
            entity.Add(new ObstacleTag());
    }

    private static void RestoreLockedDoorEntity(LockedDoorSaveDto dto, EntityManager em)
    {
        var entity = Guid.TryParse(dto.Id, out var id) ? em.CreateEntity(id) : em.CreateEntity();
        entity.Add(new LockedTag());
        entity.Add(new PositionComponent { X = dto.PosX, Y = dto.PosY, Z = dto.PosZ });
        if (dto.Name != null)
            entity.Add(new IdentityComponent { Name = dto.Name });
    }

    /// <summary>
    /// Wires every simulation system into <see cref="Engine"/> in execution order.
    /// Phase ordering and per-system rationale are documented inline (do not add
    /// XML comments to the inline blocks below — they are implementation detail).
    /// </summary>
    /// <remarks>
    /// Called once from the constructor. Re-running it would double-register systems.
    /// See the class summary for the full pipeline.
    /// </remarks>
    private void RegisterSystems()
    {
        var sys = Config.Systems;
        var choreBiasTable = ChoreAcceptanceBiasTable.LoadDefault((float)Config.Chores.DefaultAcceptanceBias);

        // Spatial — sync index, room membership, cache invalidation (Phase 5).
        // ProximityEvent moves to Lighting (Phase 7) so it fires after illumination is current.
        var syncSys = new SpatialIndexSyncSystem(SpatialIndex, StructuralBus);
        EntityManager.EntityDestroyed += syncSys.OnEntityDestroyed;
        Engine.AddSystem(syncSys,                                                                SystemPhase.Spatial);
        Engine.AddSystem(new RoomMembershipSystem(RoomMembership, ProximityBus, StructuralBus), SystemPhase.Spatial);
        Engine.AddSystem(new PathfindingCacheInvalidationSystem(StructuralBus, PathfindingCache), SystemPhase.Spatial);

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
        // Structural tagging: one-shot system at boot that attaches StructuralTag to obstacles/walls/doors
        Engine.AddSystem(new StructuralTaggingSystem(),                            SystemPhase.PreUpdate);
        // Schedule spawner: attach routines to NPCs that lack one (runs every tick, idempotent)
        Engine.AddSystem(new ScheduleSpawnerSystem(),                              SystemPhase.PreUpdate);

        // Stress initialization — attaches StressComponent to newly-spawned NPCs that lack one.
        Engine.AddSystem(
            new StressInitializerSystem(StressInitializerSystem.LoadBaselines()),  SystemPhase.PreUpdate);

        // Mask initialization — attaches SocialMaskComponent with personality-derived baseline.
        Engine.AddSystem(new MaskInitializerSystem(),                              SystemPhase.PreUpdate);

        // Workload initialization — attaches WorkloadComponent with per-archetype capacity.
        Engine.AddSystem(
            new WorkloadInitializerSystem(WorkloadInitializerSystem.LoadCapacities()), SystemPhase.PreUpdate);

        // Life state initialization — attaches LifeStateComponent to newly-spawned NPCs with State == Alive.
        Engine.AddSystem(new LifeStateInitializerSystem(),                          SystemPhase.PreUpdate);

        // Task generation — spawns new task entities once per game-day at the configured hour.
        Engine.AddSystem(
            new TaskGeneratorSystem(Config.Workload, Clock, Random),               SystemPhase.PreUpdate);

        // Chore initializer — spawns ChoreComponent entities and attaches ChoreHistoryComponent to NPCs.
        Engine.AddSystem(new ChoreInitializerSystem(Config.Chores),                SystemPhase.PreUpdate);

        // Chore assignment — assigns due chores to highest-bias NPCs once per game-day.
        Engine.AddSystem(
            new ChoreAssignmentSystem(Config.Chores, Clock, choreBiasTable, NarrativeBus), SystemPhase.PreUpdate);

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
            SpatialIndex, RoomMembership, WillpowerEvents, Random, Config.ActionSelection, Config.Schedule,
            EntityManager, Config.Workload, Config.Chores, choreBiasTable, NarrativeBus),
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
            new StressSystem(Config.Stress, Config.Workload, Clock, WillpowerEvents, NarrativeBus, EntityManager,
                choreCfg: Config.Chores), SystemPhase.Cleanup);

        // Workload system — advances task progress, detects completion and overdue.
        Engine.AddSystem(
            new WorkloadSystem(Config.Workload, Clock, NarrativeBus, EntityManager), SystemPhase.Cleanup);

        // Mask crack detection — runs at Cleanup so it fires after ActionSelectionSystem has
        // written its intent; the crack override wins for the following Dialog phase.
        Engine.AddSystem(
            new MaskCrackSystem(RoomMembership, NarrativeBus, Config.SocialMask),  SystemPhase.Cleanup);

        // Create a single LifeStateTransitionSystem instance for both choking and life-state management.
        var lifeStateTransition = new LifeStateTransitionSystem(NarrativeBus, EntityManager, Clock, Config);

        // Choking detection — identifies choking conditions (bolus + distraction) and enqueues transition to Incapacitated.
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

        // Life state transitions — processes queued state changes (Alive→Incapacitated→Deceased);
        // runs after WorkloadSystem and MaskCrackSystem so all cognitive ticking is complete.
        Engine.AddSystem(lifeStateTransition, SystemPhase.Cleanup);

        // Choking cleanup — removes IsChokingTag and ChokingComponent when NPC transitions to Deceased.
        // Runs at the very end of Cleanup phase (after LifeStateTransitionSystem).
        Engine.AddSystem(
            new ChokingCleanupSystem(), SystemPhase.Cleanup);

        // Slip-and-fall detection — rolls hazard checks for NPCs on tiles with FallRiskComponent.
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

        // Chore execution — advances assigned chore progress each tick; fires on ChoreWork intent.
        Engine.AddSystem(
            new ChoreExecutionSystem(Config.Chores, Clock, choreBiasTable, NarrativeBus), SystemPhase.Cleanup);

        // Lockout detection — checks end-of-day reachability to exits and starvation status.
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

    // ── Human count ───────────────────────────────────────────────────────────
    /// <summary>
    /// Default number of humans spawned when no <c>humanCount</c> argument is
    /// supplied.  100 gives a realistic stress-test world; pass 1 to
    /// <see cref="SimulationBootstrapper(IConfigProvider,int)"/> for isolated
    /// single-entity tests.
    /// </summary>
    public const int DefaultHumanCount = 100;

    /// <summary>
    /// Spawns the default 10×10 apartment — a humanCount-sized human grid plus
    /// fixed-position fridge, sink, bed, and toilet world objects. Used when no
    /// world definition file was supplied.
    /// </summary>
    /// <param name="humanCount">How many humans to lay out on the grid.</param>
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
    /// Spreads <paramref name="count"/> humans evenly on a rectangular grid
    /// inside the 10×10 default world. For count = 1 the single entity lands
    /// at centre (5, 5) and is named "Billy".
    /// </summary>
    /// <param name="count">Number of humans to spawn. Values ≤ 0 are a no-op.</param>
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

    /// <summary>
    /// Creates a single static world object — Identity, Position, and a default
    /// instance of <typeparamref name="TTag"/> as the marker component (e.g.
    /// <see cref="SinkComponent"/>, <see cref="BedComponent"/>, …).
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
    /// <returns>An immutable snapshot of the current simulation state.</returns>
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
    /// <param name="newCfg">Freshly loaded config whose primitive values are merged into the live <see cref="Config"/>.</param>
    /// <remarks>
    /// Only flat (value-type) properties are merged. Nested object identities are
    /// preserved — see <see cref="MergeFlat{T}"/>. Entity starting values
    /// (e.g. <c>EnergyStart</c>) take effect for entities spawned AFTER the reload.
    /// </remarks>
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
    /// <typeparam name="T">Reference type of the config object being merged.</typeparam>
    /// <param name="src">Newly loaded config object whose primitive values are read.</param>
    /// <param name="dst">Live config object whose primitive values are mutated in place.</param>
    /// <param name="changes">Sink for "Type.Prop  old → new" change descriptions, one per modified property.</param>
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
