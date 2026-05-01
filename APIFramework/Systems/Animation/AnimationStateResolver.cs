using System;
using APIFramework.Components;
using APIFramework.Core;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Systems.Animation;

/// <summary>
/// Pure-C# state machine that maps ECS component data to an <see cref="NpcAnimationState"/>.
/// Extracted from NpcSilhouetteRenderer so xUnit tests can drive it without Unity.
///
/// Priority order (highest wins):
///   Dead > Panic > CoughingFit > Crying > Sleep > SleepingAtDesk > Heimlich
///   > Talk > Eating > Drinking > DefecatingInCubicle > Working > Walk > Idle
/// </summary>
public static class AnimationStateResolver
{
    /// <summary>
    /// Resolves the current animation state for <paramref name="entity"/>.
    /// </summary>
    /// <param name="entity">The ECS entity to evaluate.</param>
    /// <param name="isDtoMoving">IsMoving flag from the WorldStateDto position snapshot.</param>
    /// <param name="isDtoSleeping">IsSleeping flag from the WorldStateDto physiology snapshot.</param>
    /// <param name="em">EntityManager used to look up victim entities for Heimlich detection.</param>
    public static NpcAnimationState Resolve(
        Entity        entity,
        bool          isDtoMoving,
        bool          isDtoSleeping,
        EntityManager em)
    {
        // ── 1. Dead ──────────────────────────────────────────────────────────────
        if (entity.Has<LifeStateComponent>())
        {
            var ls = entity.Get<LifeStateComponent>();
            if (ls.State == LS.Deceased) return NpcAnimationState.Dead;
        }

        // ── 2. Panic: acute choking (ChokingComponent) OR high panic level ───────
        if (entity.Has<ChokingComponent>()) return NpcAnimationState.Panic;

        if (entity.Has<MoodComponent>())
        {
            var mood = entity.Get<MoodComponent>();
            if (mood.PanicLevel >= 0.5f) return NpcAnimationState.Panic;
        }

        // ── 3. CoughingFit: early choking (IsChokingTag only) ────────────────────
        // IsChokingTag without ChokingComponent = early/mild phase. In practice
        // ChokingDetectionSystem adds both simultaneously, so this fires during the
        // single tick between tag application and component attachment, or when the
        // sickness system (future) sets the tag alone.
        if (entity.Has<IsChokingTag>() && !entity.Has<ChokingComponent>())
            return NpcAnimationState.CoughingFit;

        // ── 4. Crying: high grief ─────────────────────────────────────────────────
        if (entity.Has<MoodComponent>())
        {
            var mood = entity.Get<MoodComponent>();
            if (mood.GriefLevel >= 0.7f) return NpcAnimationState.Crying;
        }

        // ── 5. Sleep: incapacitated (fainting) OR physiology IsSleeping ──────────
        if (entity.Has<LifeStateComponent>())
        {
            var ls = entity.Get<LifeStateComponent>();
            if (ls.State == LS.Incapacitated) return NpcAnimationState.Sleep;
        }

        if (isDtoSleeping) return NpcAnimationState.Sleep;

        // ── 6. SleepingAtDesk: low energy while not officially sleeping ───────────
        // Proxy for "at desk" is the absence of active locomotion intent.
        if (entity.Has<EnergyComponent>())
        {
            var energy = entity.Get<EnergyComponent>();
            if (energy.Energy < 25f && !energy.IsSleeping)
            {
                var intentKind = entity.Has<IntendedActionComponent>()
                    ? entity.Get<IntendedActionComponent>().Kind
                    : IntendedActionKind.Idle;
                if (intentKind != IntendedActionKind.Approach)
                    return NpcAnimationState.SleepingAtDesk;
            }
        }

        // ── Intent-based states ───────────────────────────────────────────────────
        if (!entity.Has<IntendedActionComponent>()) return NpcAnimationState.Idle;

        var intent = entity.Get<IntendedActionComponent>();

        // ── 7. Heimlich: rescue intent directed at a choking victim ───────────────
        if (intent.Kind == IntendedActionKind.Rescue)
        {
            var victim = FindEntityByIntId(intent.TargetEntityId, em);
            if (victim != null && victim.Has<IsChokingTag>())
                return NpcAnimationState.Heimlich;
        }

        // ── 8. Talk ───────────────────────────────────────────────────────────────
        if (intent.Kind == IntendedActionKind.Dialog) return NpcAnimationState.Talk;

        // ── 9. Eating ────────────────────────────────────────────────────────────
        if (intent.Kind == IntendedActionKind.Eat) return NpcAnimationState.Eating;

        // ── 10. Drinking ──────────────────────────────────────────────────────────
        if (intent.Kind == IntendedActionKind.Drink) return NpcAnimationState.Drinking;

        // ── 11. DefecatingInCubicle ───────────────────────────────────────────────
        if (intent.Kind == IntendedActionKind.Defecate) return NpcAnimationState.DefecatingInCubicle;

        // ── 12. Working ───────────────────────────────────────────────────────────
        if (intent.Kind == IntendedActionKind.Work) return NpcAnimationState.Working;

        // ── 13. Walk: approaching + actually moving ───────────────────────────────
        if (intent.Kind == IntendedActionKind.Approach && isDtoMoving)
            return NpcAnimationState.Walk;

        // ── 14. Idle: default ─────────────────────────────────────────────────────
        return NpcAnimationState.Idle;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static Entity? FindEntityByIntId(int intId, EntityManager em)
    {
        foreach (var e in em.Query<NpcTag>())
        {
            var b = e.Id.ToByteArray();
            if (BitConverter.ToInt32(b, 0) == intId) return e;
        }
        return null;
    }
}
