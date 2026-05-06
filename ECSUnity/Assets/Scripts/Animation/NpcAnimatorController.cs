using APIFramework.Components;
using UnityEngine;

/// <summary>
/// Bridges the engine-side <see cref="NpcAnimationState"/> enum to the Unity
/// <see cref="Animator"/> bool parameters that drive the NpcAnimator state machine.
///
/// DESIGN
/// ───────
/// <see cref="NpcSilhouetteInstance"/> computes the animation state once per
/// <c>LateUpdate</c> frame (in <see cref="NpcSilhouetteRenderer"/>). This controller
/// is a pure consumer of that state: it reads <see cref="NpcSilhouetteInstance.CurrentAnimState"/>
/// and translates it to Animator parameters.
///
/// Responsibilities:
///   1. Set Animator bool parameters each frame so the state machine transitions.
///   2. Handle the special case of <see cref="NpcAnimationState.Dead"/>: disable
///      the Animator component entirely so the Dead pose is static (no animation).
///   3. Handle the <see cref="NpcAnimationState.Panic"/> facing lock:
///      store the facing direction at Panic entry and hold it until state changes.
///
/// ANIMATOR PARAMETER CONTRACT
/// ─────────────────────────────
/// The NpcAnimator.controller must define the following bool parameters:
///   "IsMoving"             → Walk state
///   "IsSitting"            → Sit state
///   "IsTalking"            → Talk state
///   "IsPanicking"          → Panic state
///   "IsSleeping"           → Sleep state
///   "IsDead"               → Dead state (Any State → Dead transition)
///   "IsEating"             → Eating state
///   "IsDrinking"           → Drinking state
///   "IsDefecating"         → DefecatingInCubicle state
///   "IsSleepingAtDesk"     → SleepingAtDesk state
///   "IsWorking"            → Working state
///   "IsCrying"             → Crying state
///   "IsCoughing"           → CoughingFit state
///   "IsHeimlicking"        → Heimlich state
///
/// USAGE
/// ──────
/// Added programmatically by <see cref="NpcSilhouetteRenderer.SpawnInstance"/> to
/// the same root GameObject as <see cref="NpcSilhouetteInstance"/>. Call
/// <see cref="Initialise"/> immediately after AddComponent, passing the instance.
/// </summary>
[RequireComponent(typeof(NpcSilhouetteInstance))]
public sealed class NpcAnimatorController : MonoBehaviour
{
    // ── Animator parameter hashes ─────────────────────────────────────────────

    private static readonly int HashIsMoving         = Animator.StringToHash("IsMoving");
    private static readonly int HashIsSitting        = Animator.StringToHash("IsSitting");
    private static readonly int HashIsTalking        = Animator.StringToHash("IsTalking");
    private static readonly int HashIsPanicking      = Animator.StringToHash("IsPanicking");
    private static readonly int HashIsSleeping       = Animator.StringToHash("IsSleeping");
    private static readonly int HashIsDead           = Animator.StringToHash("IsDead");
    private static readonly int HashIsEating         = Animator.StringToHash("IsEating");
    private static readonly int HashIsDrinking       = Animator.StringToHash("IsDrinking");
    private static readonly int HashIsDefecating     = Animator.StringToHash("IsDefecating");
    private static readonly int HashIsSleepingAtDesk = Animator.StringToHash("IsSleepingAtDesk");
    private static readonly int HashIsWorking        = Animator.StringToHash("IsWorking");
    private static readonly int HashIsCrying         = Animator.StringToHash("IsCrying");
    private static readonly int HashIsCoughing       = Animator.StringToHash("IsCoughing");
    private static readonly int HashIsHeimlicking    = Animator.StringToHash("IsHeimlicking");

    private static readonly int[] AllHashes =
    {
        HashIsMoving, HashIsSitting, HashIsTalking, HashIsPanicking, HashIsSleeping, HashIsDead,
        HashIsEating, HashIsDrinking, HashIsDefecating, HashIsSleepingAtDesk,
        HashIsWorking, HashIsCrying, HashIsCoughing, HashIsHeimlicking,
    };

    // ── Component references ───────────────────────────────────────────────────

    private NpcSilhouetteInstance _instance;
    private Animator              _animator;

    // ── State tracking ─────────────────────────────────────────────────────────

    private NpcAnimationState _lastWrittenState = (NpcAnimationState)(-1);

    private float _panicFacingDeg;
    private bool  _facingLockedForPanic;

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Binds this controller to its <see cref="NpcSilhouetteInstance"/>.
    /// Must be called immediately after <c>AddComponent&lt;NpcAnimatorController&gt;()</c>.
    /// </summary>
    public void Initialise(NpcSilhouetteInstance instance)
    {
        _instance = instance;
        _animator = instance.Animator;
    }

    private void LateUpdate()
    {
        if (_instance == null) return;
        if (_animator  == null) return;

        var state = _instance.CurrentAnimState;
        if (state == _lastWrittenState) return;

        WriteAnimatorState(state);
        HandleFacingLock(state);

        _lastWrittenState = state;
    }

    // ── Animator driving ───────────────────────────────────────────────────────

    private void WriteAnimatorState(NpcAnimationState state)
    {
        foreach (var h in AllHashes)
            _animator.SetBool(h, false);

        switch (state)
        {
            case NpcAnimationState.Walk:
                _animator.SetBool(HashIsMoving, true);
                _animator.enabled = true;
                break;

            case NpcAnimationState.Sit:
                _animator.SetBool(HashIsSitting, true);
                _animator.enabled = true;
                break;

            case NpcAnimationState.Talk:
                _animator.SetBool(HashIsTalking, true);
                _animator.enabled = true;
                break;

            case NpcAnimationState.Panic:
                _animator.SetBool(HashIsPanicking, true);
                _animator.enabled = true;
                break;

            case NpcAnimationState.Sleep:
                _animator.SetBool(HashIsSleeping, true);
                _animator.enabled = true;
                break;

            case NpcAnimationState.Dead:
                _animator.SetBool(HashIsDead, true);
                _animator.enabled = false;
                break;

            case NpcAnimationState.Eating:
                _animator.SetBool(HashIsEating, true);
                _animator.enabled = true;
                break;

            case NpcAnimationState.Drinking:
                _animator.SetBool(HashIsDrinking, true);
                _animator.enabled = true;
                break;

            case NpcAnimationState.DefecatingInCubicle:
                _animator.SetBool(HashIsDefecating, true);
                _animator.enabled = true;
                break;

            case NpcAnimationState.SleepingAtDesk:
                _animator.SetBool(HashIsSleepingAtDesk, true);
                _animator.enabled = true;
                break;

            case NpcAnimationState.Working:
                _animator.SetBool(HashIsWorking, true);
                _animator.enabled = true;
                break;

            case NpcAnimationState.Crying:
                _animator.SetBool(HashIsCrying, true);
                _animator.enabled = true;
                break;

            case NpcAnimationState.CoughingFit:
                _animator.SetBool(HashIsCoughing, true);
                _animator.enabled = true;
                break;

            case NpcAnimationState.Heimlich:
                _animator.SetBool(HashIsHeimlicking, true);
                _animator.enabled = true;
                break;

            case NpcAnimationState.Idle:
            default:
                _animator.enabled = true;
                break;
        }
    }

    /// <summary>
    /// Panic-state facing lock: while Panicking, the NPC's facing direction is frozen
    /// at whatever it was when Panic was entered.
    /// </summary>
    private void HandleFacingLock(NpcAnimationState state)
    {
        if (state == NpcAnimationState.Panic)
        {
            if (!_facingLockedForPanic)
            {
                _panicFacingDeg       = _instance.transform.eulerAngles.y;
                _facingLockedForPanic = true;
            }
            _instance.transform.rotation = Quaternion.Euler(0f, _panicFacingDeg, 0f);
        }
        else
        {
            _facingLockedForPanic = false;
        }
    }
}
