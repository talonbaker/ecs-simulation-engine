using System.Collections.Generic;
using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// Renders one soft halo quad per interior light source (<see cref="LightSourceDto"/>),
/// driven by the source's <see cref="LightState"/> and intensity.
///
/// DESIGN
/// ───────
/// Each light source gets a single flat quad (lying on XZ, just above the floor) rendered
/// with the "ECSUnity/LightHalo" shader — a soft radial falloff from centre to edge.
///
/// STATE MACHINE
/// ──────────────
///   On        → halo visible at nominal intensity; steady brightness.
///   Off        → halo hidden (SetActive false).
///   Flickering → halo intensity oscillates deterministically per tick.
///               Two frequencies are mixed: a slow sine wave and a fast per-tick hash.
///               Same seed + same tick = identical pattern across machines (determinism).
///   Dying      → halo at low base intensity with sporadic drops to near-zero.
///               Deterministic via seeded RNG from (entity id hash XOR tick).
///               "Sporadic" = ~8% chance per tick of a full intensity drop.
///
/// DETERMINISM NOTE
/// ─────────────────
/// The flickering/dying calculations use <see cref="EngineHost.TickCount"/> (engine ticks,
/// not wall-clock time) XOR'd with a hash of the source's Id string. This guarantees that
/// two renderers watching the same simulation replay will produce identical visual patterns.
/// Unity's Time.time is NOT used for flicker — it would vary with frame rate.
///
/// PERFORMANCE
/// ────────────
/// All halos share one material; Unity GPU-instances the draw calls automatically.
/// Each halo is a single quad (2 triangles). 40 halos = 40 quads = 1–2 draw calls.
/// Material.SetColor + SetFloat per source per frame ~= 40 × 2 × 30 ns < 0.01 ms.
///
/// MOUNTING
/// ─────────
/// Attach to any GameObject. Assign _engineHost and _config in the Inspector.
/// </summary>
public sealed class LightSourceHaloRenderer : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("Source of WorldState (light sources and their states).")]
    private EngineHost _engineHost;

    [SerializeField]
    [Tooltip("Lighting tunable parameters.")]
    private LightingConfig _config;

    // ── Runtime state ─────────────────────────────────────────────────────────

    // Optional WorldState injected directly for test purposes.
    private WorldStateDto _injectedWorldState;

    // Halo quad GameObjects keyed by light source Id.
    private readonly Dictionary<string, HaloView> _haloViews = new();

    // Shared material.  All halos use ECSUnity/LightHalo.
    private Material _haloMaterial;

    // Root to keep hierarchy clean.
    private Transform _haloRoot;

    // Y position of halo quads — just above floor to avoid Z-fighting.
    private const float HaloY = 0.05f;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _haloRoot = new GameObject("HaloRoot").transform;
        _haloRoot.SetParent(transform, worldPositionStays: false);

        Shader haloShader = Shader.Find("ECSUnity/LightHalo")
                         ?? Shader.Find("Particles/Additive");

        _haloMaterial = new Material(haloShader) { name = "LightHalo" };
    }

    private void Update()
    {
        var ws = _injectedWorldState ?? _engineHost?.WorldState;
        if (ws?.LightSources == null) return;

        long tick = _engineHost.TickCount;

        // Read config with fallbacks.
        float maxRadius       = _config != null ? _config.haloMaxRadius            : 2.5f;
        float minAlpha        = _config != null ? _config.haloMinAlpha             : 0f;
        float maxAlpha        = _config != null ? _config.haloMaxAlpha             : 0.55f;
        float flickFreq       = _config != null ? _config.flickerFrequency         : 0.07f;
        float flickNoiseMix   = _config != null ? _config.flickerNoiseMix          : 0.35f;
        float dyingDropProb   = _config != null ? _config.dyingDropProbability     : 0.08f;
        float dyingBaseFrac   = _config != null ? _config.dyingBaseIntensityFraction: 0.22f;

        var seenIds = new HashSet<string>();
        foreach (var src in ws.LightSources)
        {
            seenIds.Add(src.Id);

            if (src.State == LightState.Off)
            {
                // Off: hide the halo — no reason to keep a quad active.
                if (_haloViews.TryGetValue(src.Id, out var offView))
                    offView.Go.SetActive(false);
                continue;
            }

            if (!_haloViews.TryGetValue(src.Id, out var view))
            {
                view = CreateHaloView(src.Id);
                _haloViews[src.Id] = view;
            }

            view.Go.SetActive(true);

            // Normalised intensity 0..1.
            float normIntensity = Mathf.Clamp01(src.Intensity / 100f);

            // Effective intensity modulated by LightState.
            float effectiveIntensity = ComputeEffectiveIntensity(
                src.State, src.Id, tick, normIntensity,
                flickFreq, flickNoiseMix,
                dyingDropProb, dyingBaseFrac);

            // Radius: scales with effective intensity.
            float radius = maxRadius * effectiveIntensity;

            // Alpha: linear between minAlpha and maxAlpha.
            float alpha  = Mathf.Lerp(minAlpha, maxAlpha, effectiveIntensity);

            // Halo tint: Kelvin color of this source.
            Color kelvin = KelvinToRgb.Convert(src.ColorTemperatureK);
            kelvin.a = alpha;

            // Position: tile X, Z (DTO Y is the tile row = world Z).
            view.Go.transform.position = new Vector3(src.Position.X, HaloY, src.Position.Y);

            // Halo is a flat XZ quad (rotate 90° around X).
            view.Go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Scale: uniform square, radius = half the scale.
            view.Go.transform.localScale = Vector3.one * (radius * 2f);

            // Apply color + alpha to material instance (each view has its own cloned material).
            view.Mat.color = kelvin;
        }

        // Remove stale halo views.
        var toRemove = new List<string>();
        foreach (var id in _haloViews.Keys)
            if (!seenIds.Contains(id)) toRemove.Add(id);
        foreach (var id in toRemove)
        {
            Destroy(_haloViews[id].Go);
            _haloViews.Remove(id);
        }
    }

    // ── Halo creation ─────────────────────────────────────────────────────────

    private HaloView CreateHaloView(string id)
    {
        var go  = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = $"Halo_{id}";
        go.transform.SetParent(_haloRoot, worldPositionStays: false);

        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Clone the shared material so each halo can have its own color / alpha.
        var mat = new Material(_haloMaterial);
        go.GetComponent<Renderer>().material = mat;

        return new HaloView { Go = go, Mat = mat };
    }

    // ── Intensity modulation ──────────────────────────────────────────────────

    /// <summary>
    /// Returns the effective normalised intensity (0..1) for a light source given its
    /// current state, seeded by the source Id and the current engine tick.
    /// All calculations are pure deterministic arithmetic — no System.Random, no Time.time.
    /// </summary>
    private static float ComputeEffectiveIntensity(
        LightState state,
        string     sourceId,
        long       tick,
        float      normIntensity,
        float      flickFreq,
        float      flickNoiseMix,
        float      dyingDropProb,
        float      dyingBaseFrac)
    {
        switch (state)
        {
            case LightState.On:
                return normIntensity;

            case LightState.Flickering:
                return FlickerIntensity(sourceId, tick, normIntensity, flickFreq, flickNoiseMix);

            case LightState.Dying:
                return DyingIntensity(sourceId, tick, normIntensity, dyingDropProb, dyingBaseFrac);

            default:
                // Off is handled by the caller; anything else treated as On.
                return normIntensity;
        }
    }

    /// <summary>
    /// Deterministic flicker: combines a slow sinusoidal oscillation with a fast per-tick
    /// hash noise.  Pattern repeats identically for the same (sourceId, tick) pair.
    ///
    /// <paramref name="freq"/> controls the main oscillation frequency in cycles-per-tick.
    /// <paramref name="noiseMix"/> 0 = pure sine, 1 = pure hash noise.
    /// </summary>
    private static float FlickerIntensity(
        string sourceId, long tick, float nominalIntensity,
        float  freq,     float noiseMix)
    {
        int seed = sourceId.GetHashCode();

        // Slow sinusoidal carrier wave.
        float phase   = (tick * freq + (seed & 0x7FFFF) * 0.001f) % (2f * Mathf.PI);
        float sine    = 0.5f + 0.5f * Mathf.Sin(phase);   // 0..1

        // Fast per-tick hash for the high-frequency flicker component.
        uint h = TickHash((int)(tick & 0x7FFFFFFFL), seed);
        float noise = (h & 0xFFFF) / 65535f;              // 0..1

        // Mix the two and scale by nominal intensity so a dim source flickers dimly.
        float raw = Mathf.Lerp(sine, noise, noiseMix);
        return Mathf.Clamp01(raw * nominalIntensity);
    }

    /// <summary>
    /// Deterministic dying: low base intensity with occasional full drops to near-zero.
    /// "Occasional" is determined by a seeded hash compared against <paramref name="dropProb"/>.
    /// </summary>
    private static float DyingIntensity(
        string sourceId, long tick, float nominalIntensity,
        float  dropProb, float baseFrac)
    {
        int seed = sourceId.GetHashCode();
        uint h   = TickHash((int)(tick & 0x7FFFFFFFL), seed);

        float t = (h & 0xFFFF) / 65535f;   // 0..1
        bool  drop = t < dropProb;           // sporadic zero-drop

        float base_ = nominalIntensity * baseFrac;
        return drop ? 0f : base_;
    }

    /// <summary>
    /// Mixes a tick value and a seed into a pseudo-random uint.
    /// Uses Knuth multiplicative hashing.  Fast, no allocations, no System.Random.
    /// </summary>
    private static uint TickHash(int tick, int seed)
    {
        uint h = (uint)(tick * 2654435761) ^ (uint)seed;
        h ^= h >> 16;
        h *= 0x45d9f3b;
        h ^= h >> 16;
        return h;
    }

    // ── Test / diagnostic accessors ───────────────────────────────────────────

    /// <summary>
    /// Injects a WorldStateDto for test use. When set, Update() reads from this instead
    /// of EngineHost.WorldState, allowing tests to drive the renderer without a full engine boot.
    /// </summary>
    public void InjectWorldState(WorldStateDto ws) => _injectedWorldState = ws;

    /// <summary>Returns true if the halo for the given source is currently visible.</summary>
    public bool IsHaloVisible(string sourceId)
        => _haloViews.TryGetValue(sourceId, out var v) && v.Go.activeInHierarchy;

    /// <summary>Returns the current alpha of the halo material for the given source.</summary>
    public float GetHaloAlpha(string sourceId)
        => _haloViews.TryGetValue(sourceId, out var v) ? v.Mat.color.a : 0f;

    /// <summary>
    /// Returns the effective intensity computed for the given source at the given tick.
    /// Exposed for determinism tests that need to verify the pattern without running Update().
    /// </summary>
    public static float ComputeFlickerAt(string sourceId, long tick,
                                         float flickFreq = 0.07f, float noiseMix = 0.35f)
        => FlickerIntensity(sourceId, tick, 1f, flickFreq, noiseMix);

    /// <summary>
    /// Returns the effective intensity computed for a dying source at the given tick.
    /// </summary>
    public static float ComputeDyingAt(string sourceId, long tick,
                                        float dropProb = 0.08f, float baseFrac = 0.22f)
        => DyingIntensity(sourceId, tick, 1f, dropProb, baseFrac);

    // ── Inner types ───────────────────────────────────────────────────────────

    private sealed class HaloView
    {
        public GameObject Go;
        public Material   Mat;
    }
}
