namespace APIFramework.Core;

/// <summary>
/// Execution phase assigned to each system. SimulationEngine groups systems by phase
/// and runs phases in ascending numeric order.
///
/// WHY PHASES INSTEAD OF JUST A LIST ORDER
/// ─────────────────────────────────────────
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
/// ────────────────────────
/// As of v0.7.x, systems within a phase still run sequentially in registration order.
/// The phases exist for documentation and future parallelism — not current perf gains.
/// The real-world performance win of v0.7.x is the Query<T>() index in EntityManager,
/// which is orthogonal to phases.
///
/// PHASE ORDERING (numeric value = execution order)
/// ─────────────────────────────────────────────────
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
    Physiology = 10,
    Condition  = 20,
    Cognition  = 30,
    Behavior   = 40,
    Transit     = 50,
    Elimination = 55,  // intestine systems — after transit, before world
    World       = 60,
    PostUpdate = 100,
}

/// <summary>
/// Wraps an ISystem with its declared phase so SimulationEngine can group and
/// order execution without requiring systems to know about the phase enum themselves.
///
/// This is a value-object record — it holds no state beyond identity and phase.
/// </summary>
public sealed record SystemRegistration(ISystem System, SystemPhase Phase);
