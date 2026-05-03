using UnityEngine;
using UnityEngine.VFX;
using APIFramework.Systems.Visual;

/// <summary>
/// MonoBehaviour that subscribes to the engine's <see cref="ParticleTriggerBus"/> and
/// spawns/destroys VFX Graph instances at the trigger location.
///
/// MOUNTING
/// ─────────
/// Attach to any persistent GameObject in the scene (e.g. EngineHost's GameObject).
/// Assign <see cref="_catalog"/> and <see cref="_engineHost"/> in the Inspector.
///
/// LIFETIME
/// ─────────
/// Each VFX instance lives for <c>entry.LifetimeSeconds</c> seconds, then is destroyed.
/// Uses standard Unity Instantiate/Destroy; no pooling beyond VFX Graph's internal reuse.
///
/// WIRING
/// ───────
/// Subscribe in Start(); unsubscribe in OnDestroy() to avoid dangling delegates.
/// </summary>
public sealed class ParticleTriggerSpawner : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Catalog mapping ParticleTriggerKind to VFX Graph assets.")]
    private ParticleTriggerCatalog _catalog;

    [SerializeField]
    [Tooltip("EngineHost that owns the ParticleTriggerBus.")]
    private EngineHost _engineHost;

    private ParticleTriggerBus _bus;

    private void Start()
    {
        if (_engineHost == null)
        {
            Debug.LogError("[ParticleTriggerSpawner] EngineHost not assigned.");
            return;
        }

        _bus = _engineHost.ParticleBus;
        if (_bus == null)
        {
            Debug.LogError("[ParticleTriggerSpawner] ParticleTriggerBus not available on EngineHost.");
            return;
        }

        _bus.Subscribe(HandleTrigger);
    }

    private void OnDestroy()
    {
        _bus?.Unsubscribe(HandleTrigger);
    }

    private void HandleTrigger(ParticleTriggerEvent evt)
    {
        if (_catalog == null) return;

        var entry = _catalog.GetByKind(evt.Kind);
        if (entry == null || entry.VfxAsset == null) return;

        var worldPos  = new Vector3(evt.SourceX, 0f, evt.SourceZ);
        var go        = new GameObject($"VFX_{evt.Kind}");
        go.transform.position = worldPos;

        var vfx = go.AddComponent<VisualEffect>();
        vfx.visualEffectAsset = entry.VfxAsset;
        vfx.SetFloat("Intensity", evt.IntensityMult);
        vfx.Play();

        Destroy(go, entry.LifetimeSeconds);
    }

    // ── Test / diagnostic accessors ───────────────────────────────────────────

    /// <summary>Injects a bus directly for unit testing without EngineHost.</summary>
    public void InjectBus(ParticleTriggerBus bus)
    {
        _bus?.Unsubscribe(HandleTrigger);
        _bus = bus;
        _bus.Subscribe(HandleTrigger);
    }
}
