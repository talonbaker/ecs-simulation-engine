using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// The single canonical early-return check for "is this NPC eligible to be ticked."
/// Used throughout the engine to skip deceased and sometimes incapacitated NPCs.
///
/// Two-tier semantics:
/// - IsAlive: true only for State == Alive. Skips both Incapacitated and Deceased.
///   Used by cognitive and volitional systems (ActionSelection, WillpowerSystem, etc.)
/// - IsBiologicallyTicking: true for State == Alive or Incapacitated. Skips only Deceased.
///   Used by physiology systems that legitimately continue after incapacitation
///   (EnergySystem, MetabolismSystem, digestion pipeline, etc.)
///
/// Non-NPC entities lacking LifeStateComponent pass through both checks (return true).
/// This allows non-NPC entities to be iterated by systems that also iterate NPCs.
/// </summary>
public static class LifeStateGuard
{
    /// <summary>
    /// The single canonical "is this NPC eligible to be ticked" check.
    /// Equivalent to State == Alive.
    /// Returns true for non-NPC entities lacking the component (pass-through semantics).
    /// </summary>
    /// <param name="npc">The entity to check; may be a non-NPC entity (pass-through).</param>
    /// <returns>True if the entity has no <see cref="LifeStateComponent"/> or its state is <see cref="Components.LifeState.Alive"/>; false for Incapacitated or Deceased.</returns>
    /// <seealso cref="IsBiologicallyTicking"/>
    public static bool IsAlive(Entity npc)
    {
        if (!npc.Has<LifeStateComponent>()) return true; // non-NPC entities pass through
        return npc.Get<LifeStateComponent>().State == Components.LifeState.Alive;
    }

    /// <summary>
    /// True when biology continues but cognition stops.
    /// Used by physiology systems that should keep ticking after Incapacitated.
    /// Returns true for State == Alive or State == Incapacitated.
    /// Returns true for non-NPC entities lacking the component (pass-through semantics).
    /// </summary>
    /// <param name="npc">The entity to check; may be a non-NPC entity (pass-through).</param>
    /// <returns>True if the entity has no <see cref="LifeStateComponent"/> or its state is <see cref="Components.LifeState.Alive"/> or <see cref="Components.LifeState.Incapacitated"/>; false only for Deceased.</returns>
    /// <seealso cref="IsAlive"/>
    public static bool IsBiologicallyTicking(Entity npc)
    {
        if (!npc.Has<LifeStateComponent>()) return true; // non-NPC entities pass through
        var s = npc.Get<LifeStateComponent>().State;
        return s == Components.LifeState.Alive || s == Components.LifeState.Incapacitated;
    }
}
