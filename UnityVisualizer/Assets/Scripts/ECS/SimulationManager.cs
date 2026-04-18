using System;
using System.IO;
using UnityEngine;
using APIFramework.Core;
using APIFramework.Config;

/// <summary>
/// Singleton MonoBehaviour that owns and ticks the ECS simulation engine.
/// Add this component to an empty GameObject named "SimulationManager" in the scene.
///
/// ROLE
/// ────
/// Unity is a pure renderer in this architecture — it never writes to the simulation.
/// Each frame:
///   1. Engine.Update(deltaTime) — advances the simulation by one real-second step
///   2. Capture()               — produces an immutable SimulationSnapshot
///   3. All views read from Snapshot — no other coupling to the engine exists
///
/// CONFIG
/// ──────
/// The system walks up from the Unity project folder looking for SimConfig.json —
/// it will find the one at the repo root automatically. Override configPath in
/// the Inspector to point at a different file.
/// </summary>
public class SimulationManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static SimulationManager Instance  { get; private set; }
    public static SimulationSnapshot Snapshot { get; private set; }

    // ── Inspector fields ──────────────────────────────────────────────────────
    [Header("Config")]
    [Tooltip("Absolute or relative path to SimConfig.json. Leave blank to auto-locate.")]
    public string configPath = "";

    [Header("Playback Speed")]
    [Tooltip("Multiplier on top of SimConfig defaultTimeScale (120). " +
             "1 = normal game speed. Drag to 0 to pause, >1 to fast-forward.")]
    [Range(0f, 5f)]
    public float speedMultiplier = 1f;

    // ── Private ───────────────────────────────────────────────────────────────
    private SimulationBootstrapper _sim;
    private bool _ready = false;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            string resolved = ResolveConfigPath();
            _sim = resolved != null
                ? new SimulationBootstrapper(resolved)
                : new SimulationBootstrapper(new InMemoryConfigProvider(new SimConfig()));

            _ready = true;
            Debug.Log($"[ECS] Simulation started. " +
                      $"Config: {resolved ?? "defaults"} | " +
                      $"Entities: {_sim.EntityManager.Entities.Count}");
        }
        catch (Exception ex)
        {
            // NOTE: Do NOT use ex.StackTrace — Unity's Mono cannot resolve
            // NullableContextAttribute from System.Runtime 8.0.0.0 (a net8.0 artefact),
            // causing StackTrace.ToString() to throw and hide the real error.
            Debug.LogError("[ECS] Failed to start simulation: "
                + ex.GetType().Name + ": " + ex.Message);
        }
    }

    void Update()
    {
        if (!_ready) return;

        _sim.Clock.TimeScale = _sim.Config.World.DefaultTimeScale * speedMultiplier;
        _sim.Engine.Update(Time.deltaTime);
        Snapshot = _sim.Capture();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Config resolution ─────────────────────────────────────────────────────

    /// <summary>
    /// Locates SimConfig.json. Checks (in order):
    ///   1. Inspector configPath field
    ///   2. StreamingAssets/SimConfig.json
    ///   3. Walk up from Application.dataPath (finds repo-root SimConfig.json)
    /// Returns null if not found — caller uses compiled defaults.
    /// </summary>
    private string ResolveConfigPath()
    {
        // 1. Explicit override
        if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
            return configPath;

        // 2. StreamingAssets (for deployed builds)
        string streaming = Path.Combine(Application.streamingAssetsPath, "SimConfig.json");
        if (File.Exists(streaming)) return streaming;

        // 3. Walk up from the Unity Assets folder — finds repo-root SimConfig.json
        //    Assets/ is one level inside UnityVisualizer/, which is one level inside repo root.
        string dir = Application.dataPath; // e.g. …/_ecs-simulation-engine/UnityVisualizer/Assets
        for (int i = 0; i < 5; i++)
        {
            dir = Directory.GetParent(dir)?.FullName;
            if (dir == null) break;
            string candidate = Path.Combine(dir, "SimConfig.json");
            if (File.Exists(candidate))
            {
                Debug.Log($"[ECS] Found SimConfig.json at: {candidate}");
                return candidate;
            }
        }

        Debug.LogWarning("[ECS] SimConfig.json not found — using compiled defaults.");
        return null;
    }
}
