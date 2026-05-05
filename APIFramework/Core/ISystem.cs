namespace APIFramework.Core;

/// <summary>
/// The contract every simulation system implements.
/// <see cref="SimulationEngine"/> calls <see cref="Update"/> once per tick, in the order
/// declared by the system's <see cref="SystemPhase"/> registration. There is no
/// initialize / dispose lifecycle — systems are instantiated by
/// <see cref="SimulationBootstrapper"/> and live for the duration of the simulation.
/// </summary>
public interface ISystem
{
    /// <summary>
    /// Called once per simulation tick. Implementations read the entities they care
    /// about via <see cref="EntityManager.Query{T}"/>, mutate components through
    /// <see cref="Entity.Add{T}"/>, and treat <paramref name="deltaTime"/> as
    /// game-seconds (the engine has already scaled it by <c>Clock.TimeScale</c>).
    /// </summary>
    /// <param name="em">The entity manager hosting all simulation entities.</param>
    /// <param name="deltaTime">Elapsed game-time (already scaled) since the previous tick, in seconds.</param>
    void Update(EntityManager em, float deltaTime);
}