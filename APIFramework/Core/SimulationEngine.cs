namespace APIFramework.Core;

/// <summary>
/// Drives the simulation: advances the clock, then runs all registered systems
/// grouped by SystemPhase in ascending numeric order.
///
/// PHASE EXECUTION MODEL (v0.7.2)
/// --------------------------------
/// Systems within the same phase are still executed sequentially in registration
/// order. The phase boundary is the synchronization primitive — it makes the
/// dependency structure explicit and is the prerequisite for future parallelism.
///
/// When parallel execution is introduced (v0.8+), each phase becomes a Task.WhenAll
/// over its member systems, with a barrier between phases. The AddSystem API and
/// SystemRegistration record are already compatible with that model.
///
/// BACKWARD COMPATIBILITY
/// -----------------------
/// AddSystem(ISystem) still works — it assigns SystemPhase.Physiology by default
/// so old registrations continue to run. Prefer the explicit overload:
///   AddSystem(system, SystemPhase.Transit)
/// </summary>
public class SimulationEngine
{
    // Internal ordered list of registrations — kept in insertion order.
    // The phase-ordered execution list is built lazily and invalidated on AddSystem.
    private readonly List<SystemRegistration> _registrations = new();
    private List<SystemRegistration>?         _orderedCache;

    /// <summary>The <see cref="Core.EntityManager"/> this engine ticks systems against.</summary>
    public EntityManager    EntityManager { get; private set; }

    /// <summary>The <see cref="SimulationClock"/> advanced once per <see cref="Update"/> call.</summary>
    public SimulationClock  Clock         { get; private set; }

    /// <summary>Read-only view of all registrations (for diagnostics/tooling).</summary>
    public IReadOnlyList<SystemRegistration> Registrations => _registrations;

    /// <summary>
    /// Creates a new simulation engine bound to an existing entity manager and clock.
    /// </summary>
    /// <param name="entityManager">The entity manager that owns the simulation's entities.</param>
    /// <param name="clock">The simulation clock to advance on every <see cref="Update"/>.</param>
    public SimulationEngine(EntityManager entityManager, SimulationClock clock)
    {
        EntityManager = entityManager;
        Clock         = clock;
    }

    // -- Registration ----------------------------------------------------------

    /// <summary>Registers a system at an explicit phase.</summary>
    public void AddSystem(ISystem system, SystemPhase phase)
    {
        _registrations.Add(new SystemRegistration(system, phase));
        _orderedCache = null; // invalidate
    }

    /// <summary>
    /// Registers a system without an explicit phase.
    /// Defaults to Physiology so legacy single-argument calls continue to work.
    /// Prefer the two-argument overload for clarity.
    /// </summary>
    public void AddSystem(ISystem system)
        => AddSystem(system, SystemPhase.Physiology);

    // -- Tick -----------------------------------------------------------------

    /// <summary>
    /// Advances the simulation by one tick: ticks <see cref="Clock"/> with the given
    /// real-time delta, then runs every registered system in ascending
    /// <see cref="SystemPhase"/> order, passing the scaled (game-time) delta.
    /// The phase-sorted execution list is cached and rebuilt only when
    /// <see cref="AddSystem(ISystem, SystemPhase)"/> is called.
    /// </summary>
    /// <param name="realDeltaTime">Elapsed real (wall-clock) seconds since the previous tick.</param>
    public void Update(float realDeltaTime)
    {
        // Advance the clock; all systems receive game-time so their math is
        // independent of TimeScale.
        float scaledDelta = Clock.Tick(realDeltaTime);

        // Build (or reuse) the phase-sorted execution order.
        _orderedCache ??= [.. _registrations.OrderBy(r => (int)r.Phase)];

        foreach (var registration in _orderedCache)
        {
            registration.System.Update(EntityManager, scaledDelta);
        }
    }
}