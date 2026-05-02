using System;
using APIFramework.Components;
using APIFramework.Systems.Audio;

namespace APIFramework.Systems.Animation;

/// <summary>
/// Emits diegetic sound triggers for animation-cycle events.
/// Each method corresponds to one animation cycle callback: call it when the Unity
/// animation event fires (or when the engine-side cycle timer fires).
/// The SoundTriggerBus handles de-duplication if both Unity and engine emit.
/// </summary>
public static class AnimationCycleSoundEmitter
{
    public static void OnEatingCycle(SoundTriggerBus bus, Guid entityId, float x, float z, long tick)
        => bus.Emit(SoundTriggerKind.Chew, entityId, x, z, 0.6f, tick);

    public static void OnDrinkingCycle(SoundTriggerBus bus, Guid entityId, float x, float z, long tick)
        => bus.Emit(SoundTriggerKind.Slurp, entityId, x, z, 0.6f, tick);

    public static void OnWorkingCycle(SoundTriggerBus bus, Guid entityId, float x, float z, long tick)
        => bus.Emit(SoundTriggerKind.KeyboardClack, entityId, x, z, 0.4f, tick);

    public static void OnCryingPeriodic(SoundTriggerBus bus, Guid entityId, float x, float z, long tick)
        => bus.Emit(SoundTriggerKind.Sigh, entityId, x, z, 0.5f, tick);

    public static void OnCoughingCycle(SoundTriggerBus bus, Guid entityId, float x, float z, long tick)
        => bus.Emit(SoundTriggerKind.Cough, entityId, x, z, 0.7f, tick);

    /// <summary>
    /// Returns the SoundTriggerKind emitted for a given animation state cycle, or null if the
    /// state emits no sound. Used by tests to verify the correct kind without hard-coding it twice.
    /// </summary>
    public static SoundTriggerKind? CycleSoundFor(NpcAnimationState state) => state switch
    {
        NpcAnimationState.Eating            => SoundTriggerKind.Chew,
        NpcAnimationState.Drinking          => SoundTriggerKind.Slurp,
        NpcAnimationState.Working           => SoundTriggerKind.KeyboardClack,
        NpcAnimationState.Crying            => SoundTriggerKind.Sigh,
        NpcAnimationState.CoughingFit       => SoundTriggerKind.Cough,
        _                                   => null,
    };
}
