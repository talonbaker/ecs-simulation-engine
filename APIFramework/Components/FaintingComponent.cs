namespace APIFramework.Components;

/// <summary>
/// Timing metadata attached to an NPC by
/// <see cref="APIFramework.Systems.LifeState.FaintingDetectionSystem"/> the moment
/// a faint is triggered.
///
/// The NPC is placed into <see cref="LifeState.Incapacitated"/> for exactly
/// <c>RecoveryTick - FaintStartTick</c> ticks, after which
/// <see cref="APIFramework.Systems.LifeState.FaintingRecoverySystem"/> queues a
/// transition back to <see cref="LifeState.Alive"/>.
///
/// Fainting is never fatal. The <see cref="LifeStateComponent.IncapacitatedTickBudget"/>
/// is set to <c>FaintDurationTicks + 1</c> so the budget-expiry death check cannot
/// fire before the recovery system acts.
///
/// Removed by <see cref="APIFramework.Systems.LifeState.FaintingCleanupSystem"/> once
/// the NPC has returned to <see cref="LifeState.Alive"/>.
///
/// WP-3.0.6: Fainting System.
/// </summary>
public struct FaintingComponent
{
    /// <summary>Tick on which the faint was triggered.</summary>
    public long FaintStartTick;

    /// <summary>
    /// Tick at which <see cref="APIFramework.Systems.LifeState.FaintingRecoverySystem"/>
    /// will queue the <see cref="LifeState.Alive"/> recovery request.
    /// Equals <see cref="FaintStartTick"/> + <c>FaintingConfig.FaintDurationTicks</c>.
    /// </summary>
    public long RecoveryTick;
}
