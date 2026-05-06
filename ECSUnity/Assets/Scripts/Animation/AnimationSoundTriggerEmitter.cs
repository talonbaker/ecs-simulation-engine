using System;
using APIFramework.Systems.Animation;
using APIFramework.Systems.Audio;
using UnityEngine;

/// <summary>
/// Receives Unity animation event callbacks (OnEatingCycle, OnWorkingCycle, etc.)
/// and routes them through <see cref="AnimationCycleSoundEmitter"/> to the
/// <see cref="SoundTriggerBus"/>.
///
/// The bus is located through <see cref="EngineHost"/> — if the host is absent
/// (e.g. editor preview) the callback is silently skipped.
///
/// ANIMATION EVENT WIRING
/// ───────────────────────
/// Each animation clip that should emit sound must have an Animation Event at the
/// appropriate frame calling the corresponding public void method on this component.
///   Eating clip   → OnEatingCycle()
///   Drinking clip → OnDrinkingCycle()
///   Working clip  → OnWorkingCycle()
///   Crying clip   → OnCryingPeriodic()
///   Coughing clip → OnCoughingCycle()
/// </summary>
[RequireComponent(typeof(NpcSilhouetteInstance))]
public sealed class AnimationSoundTriggerEmitter : MonoBehaviour
{
    [SerializeField]
    [Tooltip("EngineHost that provides the SoundTriggerBus and simulation clock.")]
    private EngineHost _host;

    private NpcSilhouetteInstance _instance;
    private SoundTriggerBus       _soundBus;

    private void Awake()
    {
        _instance = GetComponent<NpcSilhouetteInstance>();
    }

    private void Start()
    {
        if (_host != null)
            _soundBus = _host.SoundBus;
    }

    // ── Animation event callbacks ──────────────────────────────────────────────

    public void OnEatingCycle()    => Emit(AnimationCycleSoundEmitter.OnEatingCycle);
    public void OnDrinkingCycle()  => Emit(AnimationCycleSoundEmitter.OnDrinkingCycle);
    public void OnWorkingCycle()   => Emit(AnimationCycleSoundEmitter.OnWorkingCycle);
    public void OnCryingPeriodic() => Emit(AnimationCycleSoundEmitter.OnCryingPeriodic);
    public void OnCoughingCycle()  => Emit(AnimationCycleSoundEmitter.OnCoughingCycle);

    // ── Internal ───────────────────────────────────────────────────────────────

    private void Emit(Action<SoundTriggerBus, Guid, float, float, long> emitFn)
    {
        if (_soundBus == null || _instance == null) return;
        if (!Guid.TryParse(_instance.EntityId, out var entityId)) return;

        var pos  = _instance.transform.position;
        long tick = _host?.Clock?.CurrentTick ?? 0L;
        emitFn(_soundBus, entityId, pos.x, pos.z, tick);
    }
}
