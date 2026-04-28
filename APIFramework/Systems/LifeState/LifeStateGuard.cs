using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// Static guard helpers used by every system that iterates NPC entities.
/// Two tiers:
/// <list type="bullet">
///   <item><description>
///     <see cref="IsAlive"/> — cognitive/volitional/social guard.
///     Skip both <see cref="LifeState.Incapacitated"/> and <see cref="LifeState.Deceased"/>.
///     Use for drive dynamics, willpower, mood, action selection, schedule, workload, mask,
///     dialog, movement, and relationship systems.
///   </description></item>
///   <item><description>
///     <see cref="IsBiologicallyTicking"/> — physiology guard.
///     Skip only <see cref="LifeState.Deceased"/>. Incapacitated NPCs still digest,
///     lose energy, and fill their bladder. Use for metabolism, energy, bladder fill,
///     esophagus, digestion, intestine, and colon systems.
///   </description></item>
/// </list>
/// <para>
/// Canonical usage (Group A — cognitive systems):
/// <code>
///   foreach (var npc in em.Query&lt;NpcTag&gt;().ToList())
///   {
///       if (!LifeStateGuard.IsAlive(npc)) continue;
///       // ...
///   }
/// </code>
/// </para>
/// <para>
/// Canonical usage (Group B — physiology systems):
/// <code>
///   foreach (var npc in em.Query&lt;NpcTag&gt;().ToList())
///   {
///       if (!LifeStateGuard.IsBiologicallyTicking(npc)) continue;
///       // ...
///   }
/// </code>
/// </para>
/// <para>
/// Non-NPC entities (tasks, stains, rooms, lights) do not have
/// <see cref="LifeStateComponent"/> and are not guarded. Both helpers return
/// <c>true</c> for any entity that lacks the component so that iterating a
/// mixed-entity query remains safe.
/// </para>
/// </summary>
public static class LifeStateGuard
{
    /// <summary>
    /// Returns <c>true</c> only if the entity is <see cref="LifeState.Alive"/>.
    /// Use for cognitive, social, and volitional systems that should skip both
    /// Incapacitated and Deceased entities.
    /// Entities without <see cref="LifeStateComponent"/> pass through (return <c>true</c>).
    /// </summary>
    public static bool IsAlive(Entity entity)
    {
        if (!entity.Has<LifeStateComponent>()) return true;
        return entity.Get<LifeStateComponent>().State == LifeState.Alive;
    }

    /// <summary>
    /// Returns <c>true</c> for <see cref="LifeState.Alive"/> and
    /// <see cref="LifeState.Incapacitated"/>. Returns <c>false</c> only for
    /// <see cref="LifeState.Deceased"/>.
    /// Use for physiology systems whose biology legitimately continues while an
    /// NPC is incapacitated (e.g. a choking NPC's esophagus still advances).
    /// Entities without <see cref="LifeStateComponent"/> pass through (return <c>true</c>).
    /// </summary>
    public static bool IsBiologicallyTicking(Entity entity)
    {
        if (!entity.Has<LifeStateComponent>()) return true;
        return entity.Get<LifeStateComponent>().State != LifeState.Deceased;
    }
}
