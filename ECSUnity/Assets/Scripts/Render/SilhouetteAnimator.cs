using APIFramework.Components;
using APIFramework.Systems.Animation;
using UnityEngine;

/// <summary>
/// Scene-level manager that applies per-state frame timing and smooth state
/// transitions to all <see cref="NpcSilhouetteInstance"/> Animators, driven by the
/// <see cref="NpcVisualStateCatalog"/> loaded from visual-state-catalog.json (MAC-013).
///
/// DESIGN
/// ───────
/// One SilhouetteAnimator exists per scene. Each LateUpdate it iterates all active
/// <see cref="NpcSilhouetteInstance"/> objects (cached on Start), reads the current
/// <see cref="NpcAnimationState"/>, and:
///
///   1. Applies catalog per-state <c>frameDurationMs</c> to <c>Animator.speed</c>.
///      Speed = ReferenceFrameMs(200) / catalogFrameMs, so shorter durations play faster.
///
///   2. For visually-significant state transitions (catalogued in <c>transitions</c>),
///      uses <c>Animator.CrossFadeInFixedTime</c> with the catalog's <c>totalDurationMs</c>
///      to produce a smooth interpolation rather than a frame snap.
///
/// ANIMATOR PARAMETER CONTRACT
/// ─────────────────────────────
/// Works alongside <see cref="NpcAnimatorController"/>, which drives the Animator bools.
/// SilhouetteAnimator layered on top: adjusts speed and fires CrossFade when applicable.
///
/// MOUNTING
/// ─────────
/// Attach to any persistent GameObject in the scene. Assign the catalog loader asset
/// via Inspector. Ensure <see cref="NpcSilhouetteRenderer"/> runs before this (script
/// execution order or LateUpdate ordering — both arrive after Update by default).
/// </summary>
public sealed class SilhouetteAnimator : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("Loaded NpcVisualStateCatalogLoader ScriptableObject. Assign in Inspector.")]
    private NpcVisualStateCatalogLoader _catalogLoader;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private NpcVisualStateCatalog  _catalog;
    private NpcSilhouetteInstance[] _instances = System.Array.Empty<NpcSilhouetteInstance>();

    // Per-instance previous state tracking (for detecting transitions).
    private NpcAnimationState[] _previousStates = System.Array.Empty<NpcAnimationState>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        _catalog = _catalogLoader != null
            ? _catalogLoader.Catalog
            : APIFramework.Systems.Animation.NpcVisualStateCatalogLoader.Empty;

        RebuildInstanceCache();
    }

    private void LateUpdate()
    {
        // Re-cache on mismatch (NPCs may spawn/despawn).
        var current = Object.FindObjectsOfType<NpcSilhouetteInstance>();
        if (current.Length != _instances.Length)
            RebuildInstanceCache();

        for (int i = 0; i < _instances.Length; i++)
        {
            var inst = _instances[i];
            if (inst == null || inst.Animator == null) continue;

            var state = inst.CurrentAnimState;
            ApplyFrameTiming(inst.Animator, state);

            if (i < _previousStates.Length && state != _previousStates[i])
            {
                HandleTransition(inst.Animator, _previousStates[i], state);
                _previousStates[i] = state;
            }
        }
    }

    // ── Frame timing ──────────────────────────────────────────────────────────

    private void ApplyFrameTiming(Animator animator, NpcAnimationState state)
    {
        float speed = _catalog.GetAnimatorSpeed(state);
        if (!Mathf.Approximately(animator.speed, speed))
            animator.speed = speed;
    }

    // ── Transition handling ───────────────────────────────────────────────────

    private void HandleTransition(Animator animator, NpcAnimationState from, NpcAnimationState to)
    {
        var transition = _catalog.GetTransition(from, to);
        if (transition == null) return;

        float durationSec = transition.TotalDurationMs / 1000f;
        string stateName  = to.ToString();

        // CrossFadeInFixedTime uses fixed (unscaled) seconds and blends at the
        // animator's default layer (0). This gives a smooth interpolation frame
        // for catalog-specified pairs; all other transitions remain instant via
        // NpcAnimatorController's bool-flip pattern.
        if (animator.isActiveAndEnabled)
            animator.CrossFadeInFixedTime(stateName, durationSec);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RebuildInstanceCache()
    {
        _instances      = Object.FindObjectsOfType<NpcSilhouetteInstance>();
        _previousStates = new NpcAnimationState[_instances.Length];
        for (int i = 0; i < _instances.Length; i++)
            _previousStates[i] = _instances[i].CurrentAnimState;
    }

    // ── Public accessors (for tests) ──────────────────────────────────────────

    /// <summary>The loaded catalog. Non-null after Start() or after first access.</summary>
    public NpcVisualStateCatalog Catalog => _catalog ??
        APIFramework.Systems.Animation.NpcVisualStateCatalogLoader.Empty;

    /// <summary>Inject a catalog directly (for tests, bypassing the ScriptableObject).</summary>
    public void InjectCatalog(NpcVisualStateCatalog catalog)
    {
        _catalog = catalog ?? APIFramework.Systems.Animation.NpcVisualStateCatalogLoader.Empty;
    }
}
