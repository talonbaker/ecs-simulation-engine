using System;
using System.IO;
using UnityEngine;
using APIFramework.Core;
using APIFramework.Config;
using APIFramework.Systems.Audio;
using Warden.Contracts.Telemetry;

/// <summary>
/// MonoBehaviour that owns the entire ECS engine lifetime: boot, tick, snapshot, dispose.
///
/// LIFECYCLE SUMMARY
/// ─────────────────
///   Awake  → nothing (let the scene fully initialise before touching the engine)
///   Start  → boot: construct SimulationBootstrapper, call BootOnce()
///   FixedUpdate → engine tick at Unity's fixed timestep (default 0.02 s = 50 Hz)
///   Update → project world state; renderers read the snapshot each render frame
///   OnDestroy → dispose the bootstrapper; mark _alive = false
///
/// DETERMINISM
/// ───────────
/// The engine ticks in FixedUpdate, not Update. Unity drives FixedUpdate at a
/// constant interval (Time.fixedDeltaTime, set in Project Settings > Time).
/// Passing this constant delta to Tick() means every engine tick sees an identical
/// deltaTime regardless of rendering frame rate — the SRD §4.2 determinism contract
/// is trivially satisfied.
///
/// THREADING
/// ─────────
/// Everything is single-threaded on Unity's main thread at v0.1. The engine is not
/// thread-safe; do not call any engine method from a background thread.
///
/// WARDEN / RETAIL
/// ───────────────
/// WorldStateProjectorAdapter selects between Warden.Telemetry.TelemetryProjector
/// (WARDEN builds) and the in-package InlineProjector (RETAIL builds) at compile time.
/// EngineHost is unaware of which path is active.
/// </summary>
public sealed class EngineHost : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("ScriptableObject containing SimConfig values and Unity host settings. " +
             "Drag Assets/Settings/DefaultSimConfig.asset here in the Inspector.")]
    private SimConfigAsset _configAsset;

    [SerializeField]
    [Tooltip("Path to the world-definition JSON. Relative paths resolve from " +
             "Application.streamingAssetsPath; absolute paths are used directly.")]
    private string _worldDefinitionPath = "office-starter.json";

    [SerializeField]
    [Tooltip("RNG seed for deterministic replay. 0 = use seed embedded in the world file.")]
    private int _seed = 0;

    // ── Engine services (set during Start) ───────────────────────────────────

    private SimulationBootstrapper  _bootstrapper;
    private WorldStateProjectorAdapter _projector;
    private bool                    _alive = false;
    private long                    _tickCount = 0;

    // ── Tick rate diagnostics ─────────────────────────────────────────────────

    private float _diagnosticAccumulator = 0f;
    private long  _ticksAtLastDiagnostic  = 0;

    // ── Snapshot (written each Update; read by renderers) ────────────────────

    /// <summary>
    /// Latest world-state snapshot. Written once per Update(); never null after Start().
    /// All renderer MonoBehaviours read from this; none hold a reference to the engine.
    /// </summary>
    public WorldStateDto WorldState { get; private set; }

    // ── Public engine access (for tests; do not use in renderer code) ────────

    /// <summary>Direct access to the EntityManager. Use only in tests and diagnostics.</summary>
    public EntityManager Engine => _bootstrapper?.EntityManager;

    /// <summary>Direct access to the SimulationClock. Use only in tests and diagnostics.</summary>
    public SimulationClock Clock => _bootstrapper?.Clock;

    /// <summary>Direct access to the SoundTriggerBus. Use only from MonoBehaviours that need to emit sounds.</summary>
    public SoundTriggerBus SoundBus => _bootstrapper?.SoundBus;

    /// <summary>Total engine ticks elapsed since boot.</summary>
    public long TickCount => _tickCount;

    /// <summary>
    /// Returns the latest world-state snapshot, identical to <see cref="WorldState"/>.
    /// Provided as a named method so callers (JsonlStreamEmitter, tests) read clearly.
    /// Must be called from the main thread only.
    /// </summary>
    public WorldStateDto Snapshot() => WorldState;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        // Validate Inspector setup before touching any engine code.
        if (_configAsset == null)
        {
            Debug.LogError("[EngineHost] SimConfigAsset is not assigned. " +
                           "Drag Assets/Settings/DefaultSimConfig.asset into the Inspector field.");
            return;
        }

        try
        {
            string resolvedWorldPath = ResolveWorldPath(_worldDefinitionPath);
            SimConfig config         = _configAsset.Config;

            // Construct the bootstrapper using IConfigProvider to keep the engine
            // decoupled from the filesystem. Unity supplies config via ScriptableObject.
            _bootstrapper = new SimulationBootstrapper(
                configProvider:      new InMemoryConfigProvider(config),
                seed:                _seed,
                worldDefinitionPath: resolvedWorldPath);

            _projector = new WorldStateProjectorAdapter();

            // Produce an initial snapshot so renderers never see a null WorldState.
            WorldState = _projector.Project(_bootstrapper, _tickCount);

            _alive = true;

            Debug.Log($"[EngineHost] Booted. " +
                      $"Entities: {_bootstrapper.EntityManager.Entities.Count} | " +
                      $"World: {resolvedWorldPath ?? "defaults"} | " +
                      $"Seed: {_seed} | " +
                      $"FixedTimestep: {Time.fixedDeltaTime:F4}s");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EngineHost] Boot failed — {ex.GetType().Name}: {ex.Message}");
            // Do not rethrow; Unity swallows exceptions from Start() silently.
            // The _alive flag stays false so FixedUpdate and Update no-op safely.
        }
    }

    private void FixedUpdate()
    {
        // FixedUpdate fires at Unity's fixed physics rate (default 50 Hz).
        // Passing Time.fixedDeltaTime is the determinism key: this value is
        // constant and identical across machines for a given project config.
        if (!_alive) return;

        try
        {
            _bootstrapper.Engine.Update(Time.fixedDeltaTime);
            _tickCount++;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EngineHost] Tick {_tickCount} threw — {ex.GetType().Name}: {ex.Message}");
            _alive = false;   // fail closed per SRD §4.1
        }
    }

    private void Update()
    {
        if (!_alive) return;

        // Project the current engine state into a WorldStateDto once per render frame.
        // Renderers (RoomRectangleRenderer, NpcDotRenderer) read WorldState; they
        // never touch the engine directly.
        WorldState = _projector.Project(_bootstrapper, _tickCount);

        // Periodic tick-rate diagnostic (WARDEN builds only; stripped in RETAIL).
#if WARDEN
        TickDiagnostic();
#endif
    }

    private void OnDestroy()
    {
        _alive = false;
        // SimulationBootstrapper does not implement IDisposable, but mark intent.
        _bootstrapper = null;
        Debug.Log($"[EngineHost] Destroyed after {_tickCount} ticks.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a world-definition path.
    /// Relative paths are joined with Application.streamingAssetsPath.
    /// Returns null when the path is blank (engine will use SpawnWorld defaults).
    /// </summary>
    private static string ResolveWorldPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (Path.IsPathRooted(path))         return path;

        string candidate = Path.Combine(Application.streamingAssetsPath, path);
        if (File.Exists(candidate)) return candidate;

        Debug.LogWarning($"[EngineHost] World file not found at '{candidate}'. Engine will use defaults.");
        return null;
    }

#if WARDEN
    /// <summary>
    /// Logs a tick-rate sample every N real seconds so the dev console can show it.
    /// Compiled out in RETAIL builds.
    /// </summary>
    private void TickDiagnostic()
    {
        _diagnosticAccumulator += Time.deltaTime;
        float interval = _configAsset != null
            ? _configAsset.HostConfig.LogTickRateEverySeconds
            : 10f;

        if (_diagnosticAccumulator >= interval)
        {
            long ticksDelta  = _tickCount - _ticksAtLastDiagnostic;
            float actualRate = ticksDelta / _diagnosticAccumulator;
            Debug.Log($"[EngineHost] Tick rate: {actualRate:F1} ticks/s " +
                      $"(target {1f / Time.fixedDeltaTime:F0}) | " +
                      $"Total ticks: {_tickCount}");
            _diagnosticAccumulator    = 0f;
            _ticksAtLastDiagnostic    = _tickCount;
        }
    }
#endif
}
