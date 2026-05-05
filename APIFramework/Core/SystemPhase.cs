namespace APIFramework.Core;

/// <summary>
/// Execution phase assigned to each system. SimulationEngine groups systems by phase
/// and runs phases in ascending numeric order.
///
/// WHY PHASES INSTEAD OF JUST A LIST ORDER
/// -----------------------------------------
/// A flat ordered list of systems enforces a total ordering: system A always runs
/// before system B. That's fine and correct. But it throws away information: we
/// can't tell *why* A must run before B — is it a hard data dependency (A writes
/// MetabolismComponent, B reads it), or just an incidental registration order?
///
/// Phases make the dependency structure explicit. Systems within the same phase
/// declare that they are logically concurrent — they don't share write targets.
/// Systems in earlier phases declare that they produce data that later phases consume.
///
/// This matters for two reasons right now and one reason in the future:
///
///   NOW:  InvariantSystem must run first. Full stop. A phase (PreUpdate) expresses
///         that constraint more clearly than "it was added first to the list."
///
///   NOW:  When a new system is being designed, the developer asks "which phase does
///         this belong to?" That question forces thinking about data ownership, which
///         is the core discipline of ECS.
///
///   FUTURE: When parallel execution is added, the engine can run all systems within
///           the same phase concurrently (on separate threads), using the phase
///           boundary as a synchronization point. The phase model is the prerequisite
///           for that — without it, the dependency graph is opaque.
///
/// CURRENT EXECUTION MODEL
/// ------------------------
/// As of v0.7.x, systems within a phase still run sequentially in registration order.
/// The phases exist for documentation and future parallelism — not current perf gains.
/// The real-world performance win of v0.7.x is the <see cref="EntityManager.Query{T}"/> index,
/// which is orthogonal to phases.
///
/// PHASE ORDERING (numeric value = execution order)
/// -------------------------------------------------
///  0   PreUpdate   — invariant enforcement; always runs before all simulation logic
/// 10   Physiology  — drain rates, biological state (metabolism, energy)
/// 20   Condition   — read physiology, write tags (hunger/thirst/irritable)
/// 30   Cognition   — read condition tags, update mood and drives
/// 40   Behavior    — read drives, trigger actions (feeding, drinking, sleeping)
/// 50   Transit     — physical movement of content (esophagus → stomach → intestines)
/// 60   World       — environmental systems (rot, weather, time-of-day) that don't
///                    depend on entity biology
/// 100  PostUpdate  — reserved for end-of-frame cleanup; nothing in v0.7.x uses this
/// </summary>
public enum SystemPhase
{
    /// <summary>
    /// Invariant enforcement and one-shot initializers. Always runs first.
    /// Hosts <c>InvariantSystem</c>, the per-component initializers (Stress, Mask, Workload, LifeState),
    /// the schedule spawner, the task generator, and <c>LockoutDetectionSystem</c>.
    /// </summary>
    PreUpdate  = 0,
    /// <summary>
    /// Spatial sync and room membership.
    /// Runs before Lighting so room occupancy is current when illumination is computed.
    /// Phase order: SpatialIndexSyncSystem → RoomMembershipSystem.
    /// </summary>
    Spatial    = 5,
    /// <summary>
    /// Lighting systems, then proximity events.
    /// Lighting reads room membership (Spatial phase) and writes per-room illumination.
    /// ProximityEventSystem runs last in this phase so it sees current illumination.
    /// Phase order: SunSystem → LightSourceStateSystem → ApertureBeamSystem →
    ///              IlluminationAccumulationSystem → ProximityEventSystem.
    /// </summary>
    Lighting   = 7,
    /// <summary>
    /// Lighting-to-drive coupling. Runs after Lighting (illumination is fresh) and before
    /// Cognition/DriveDynamics (so lighting deltas are present before circadian decay).
    /// Phase order: LightingToDriveCouplingSystem.
    /// </summary>
    Coupling   = 8,
    /// <summary>
    /// Raw biological resource drain and restore: <c>MetabolismSystem</c>, <c>EnergySystem</c>,
    /// <c>BladderFillSystem</c>. Reads from physiology components; writes back updated values.
    /// Default phase for legacy single-argument <see cref="SimulationEngine.AddSystem(ISystem)"/>.
    /// </summary>
    Physiology = 10,
    /// <summary>
    /// Derive sensation tags from physiology values: <c>BiologicalConditionSystem</c> writes
    /// hunger / thirst / irritable tags; <c>ScheduleSystem</c> resolves the active schedule block
    /// before action selection reads it.
    /// </summary>
    Condition  = 20,
    /// <summary>
    /// Process conditions into emotions and drive scores: mood, brain, physiology gate,
    /// drive dynamics, action selection, willpower, relationship lifecycle, social mask drift.
    /// Read condition tags; write the dominant drive that <see cref="Behavior"/> systems will act on.
    /// </summary>
    Cognition  = 30,
    /// <summary>
    /// Act on the dominant drive: feeding, drinking, sleep, defecation, urination.
    /// Reads <c>IntendedActionComponent</c>; produces world side-effects (consume food, fill bolus, etc.).
    /// </summary>
    Behavior   = 40,
    /// <summary>
    /// Physical movement of content through the upper digestive pipeline:
    /// <c>InteractionSystem</c> (held food then bolus), <c>EsophagusSystem</c>, <c>DigestionSystem</c>.
    /// </summary>
    Transit     = 50,
    /// <summary>
    /// Lower-digestive pipeline. Intestine systems run after transit and before world: chyme drains
    /// from small to large intestine, water reabsorbs, stool forms, urgency tags fire.
    /// </summary>
    Elimination = 55,
    /// <summary>
    /// Environmental systems independent of entity biology: rot ageing, pathfinding,
    /// movement quality (speed modifiers, step-aside, advance, facing, idle posture).
    /// </summary>
    World       = 60,
    /// <summary>
    /// Narrative detection — runs last so all engine state has settled.
    /// NarrativeEventDetector compares this tick's state to the previous tick
    /// and emits NarrativeEventCandidates via NarrativeEventBus.
    /// </summary>
    Narrative   = 70,
    /// <summary>
    /// Dialog systems — after Narrative so final drive state is visible.
    /// Phase order: DialogContextDecisionSystem → DialogFragmentRetrievalSystem → DialogCalcifySystem.
    /// </summary>
    Dialog      = 75,
    /// <summary>
    /// Post-cognition cleanup. Runs after Dialog so all state (drives, willpower, narrative,
    /// dialog) has settled for the tick. StressSystem runs here: it reads WillpowerSystem's
    /// LastDrainedBatch (populated at Cognition) and NarrativeBus events (populated at Narrative).
    /// </summary>
    Cleanup     = 80,
    /// <summary>
    /// End-of-frame slot reserved for future use. Nothing in v0.7.x registers here.
    /// </summary>
    PostUpdate = 100,
}

/// <summary>
/// Wraps an ISystem with its declared phase so SimulationEngine can group and
/// order execution without requiring systems to know about the phase enum themselves.
///
/// This is a value-object record — it holds no state beyond identity and phase.
/// </summary>
/// <param name="System">The registered system instance.</param>
/// <param name="Phase">Execution phase. Lower phases run first; phases run in ascending numeric order.</param>
public sealed record SystemRegistration(ISystem System, SystemPhase Phase);
