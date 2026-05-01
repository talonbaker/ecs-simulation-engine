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
///   "IsMoving"    → Walk state
///   "IsSitting"   → Sit state
///   "IsTalking"   → Talk state
///   "IsPanicking" → Panic state
///   "IsSleeping"  → Sleep state
///   "IsDead"      → Dead state (Any State → Dead transition)
///
/// Parameter names match by string. Hashing them in Awake avoids per-frame
/// string lookups (Animator.StringToHash is free after first call).
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
    // ── Animator parameter hashes (set in Awake) ──────────────────────────────

    private static readonly int HashIsMoving    = Animator.StringToHash("IsMoving");
    private static readonly int HashIsSitting   = Animator.StringToHash("IsSitting");
    private static readonly int HashIsTalking   = Animator.StringToHash("IsTalking");
    private static readonly int HashIsPanicking = Animator.StringToHash("IsPanicking");
    private static readonly int HashIsSleeping  = Animator.StringToHash("IsSleeping");
    private static readonly int HashIsDead      = Animator.StringToHash("IsDead");

    // ── Component references ───────────────────────────────────────────────────

    private NpcSilhouetteInstance _instance;
    private Animator              _animator;

    // ── State tracking ─────────────────────────────────────────────────────────

    // Last state written to the Animator, to avoid redundant SetBool calls.
    private NpcAnimationState _lastWrittenState = (NpcAnimationState)(-1);

    // Facing direction captured when Panic state was entered.
    // While Panicking, the NPC's facing is locked to this value.
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
        // Ensure references are valid (robustness: instance may be destroyed in tests).
        if (_instance == null) return;
        if (_animator  == null) return;

        var state = _instance.CurrentAnimState;

        // Skip update if state hasn't changed — avoids redundant SetBool calls
        // which can trigger Animator state machine evaluations each frame.
        if (state == _lastWrittenState) return;

        WriteAnimatorState(state);
        HandleFacingLock(state);

        _lastWrittenState = state;
    }

    // ── Animator driving ───────────────────────────────────────────────────────

    /// <summary>
    /// Translates the current <see cref="NpcAnimationState"/> to Animator bool
    /// parameters.
    ///
    /// Only the active state's bool is true; all others are false. This matches
    /// the state machine's Any State → Dead and per-state exclusive transitions.
    ///
    /// Special case — Dead: the Animator component is disabled after setting IsDead=true
    /// so the Dead pose is truly static (no blend tree evaluation per frame).
    /// </summary>
    private void WriteAnimatorState(NpcAnimationState state)
    {
        // Guard: if the Animator doesn't have these parameters (e.g. stub controller
        // with no parameters yet), SetBool will log an error but not throw.
        // In play-mode tests with a null or minimal controller this is acceptable.

        // Reset all bools first so only the active one is true.
        _animator.SetBool(HashIsMoving,    false);
        _animator.SetBool(HashIsSitting,   false);
        _animator.SetBool(HashIsTalking,   false);
        _animator.SetBool(HashIsPanicking, false);
        _animator.SetBool(HashIsSleeping,  false);
        _animator.SetBool(HashIsDead,      false);

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
                // Set IsDead briefly so the Any State → Dead transition fires.
                _animator.SetBool(HashIsDead, true);
                // Then disable the Animator so the pose is static from this frame onward.
                // This also prevents the Animator from overriding the Dead pose each frame.
                _animator.enabled = false;
                break;

            case NpcAnimationState.Idle:
            default:
                // All bools are already false. Animator plays Idle state by default.
                _animator.enabled = true;
                break;
        }
    }

    /// <summary>
    /// Handles the Panic-state facing lock (AT-05): while Panicking, the NPC's
    /// facing direction is frozen at whatever it was when Panic was entered.
    ///
    /// We capture the root's current Y rotation on Panic entry and hold it
    /// for as long as state == Panic. When state leaves Panic, the lock is released
    /// and NpcSilhouetteRenderer resumes writing facing from FacingComponent.
    /// </summary>
    private void HandleFacingLock(NpcAnimationState state)
    {
        if (state == NpcAnimationState.Panic)
        {
            if (!_facingLockedForPanic)
            {
                // Capture current facing on first entry into Panic.
                _panicFacingDeg        = _instance.transform.eulerAngles.y;
                _facingLockedForPanic  = true;
            }
            // Re-apply the locked facing every frame while Panicking so
            // NpcSilhouetteRenderer's own facing writes don't override it.
            // (NpcSilhouetteRenderer calls UpdatePosition which writes facing;
            //  we override it here in LateUpdate, which runs after it.)
            _instance.transform.rotation = Quaternion.Euler(0f, _panicFacingDeg, 0f);
        }
        else
        {
            _facingLockedForPanic = false;
        }
    }
}
