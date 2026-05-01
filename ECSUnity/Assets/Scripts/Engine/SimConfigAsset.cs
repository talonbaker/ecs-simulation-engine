using System;
using UnityEngine;
using APIFramework.Config;
using ECSUnity.Config;

/// <summary>
/// ScriptableObject wrapper around <see cref="SimConfig"/> and <see cref="UnityHostConfig"/>.
///
/// WHY A SCRIPTABLEOBJECT
/// ───────────────────────
/// The Unity Inspector can display and edit ScriptableObject fields at design time.
/// This makes it possible to inspect and override SimConfig values without editing
/// SimConfig.json on disk — useful for tuning runs and build-variant configuration.
///
/// At runtime, <see cref="EngineHost"/> passes <see cref="Config"/> to an
/// <see cref="APIFramework.Core.InMemoryConfigProvider"/>, so the engine never knows
/// it was configured from a Unity asset rather than a file.
///
/// HOW TO CREATE A DEFAULT
/// ────────────────────────
/// Right-click the Project window → Create → ECSUnity → SimConfig Asset
/// A default instance is already committed at Assets/Settings/DefaultSimConfig.asset.
/// </summary>
[CreateAssetMenu(menuName = "ECSUnity/SimConfig Asset", fileName = "DefaultSimConfig")]
public sealed class SimConfigAsset : ScriptableObject
{
    // ── SimConfig source ──────────────────────────────────────────────────────

    [Header("Config Source")]
    [Tooltip("Optional path to SimConfig.json on disk. When set, config is loaded from " +
             "this path and _configOverrides are ignored. Leave blank to use built-in defaults " +
             "augmented by the override fields below.")]
    [SerializeField] private string _configFilePath = "";

    // ── Unity host config (always Inspector-editable) ─────────────────────────

    [Header("Unity Host Settings")]
    [Tooltip("Target engine ticks per second. Must match Unity's FixedUpdate rate in " +
             "Project Settings > Time > Fixed Timestep. Default 50 = 0.02s fixed timestep.")]
    [SerializeField] private int _ticksPerSecond = 50;

    [Tooltip("Informational only — does not enforce a cap, but communicates intent.")]
    [SerializeField] private int _renderFrameRateTarget = 60;

    [Tooltip("Minimum acceptable rolling-average FPS for the performance gate.")]
    [SerializeField] private float _performanceGateMinFps = 55f;

    [Tooltip("Mean FPS the performance gate requires over 60 seconds.")]
    [SerializeField] private float _performanceGateMeanFps = 58f;

    [Tooltip("p99 FPS floor for the performance gate.")]
    [SerializeField] private float _performanceGateP99Fps = 50f;

    [Header("Camera")]
    [Tooltip("World-units. Camera cannot descend below this altitude.")]
    [SerializeField] private float _cameraMinAltitude = 3f;

    [Tooltip("World-units. Camera cannot rise above this altitude.")]
    [SerializeField] private float _cameraMaxAltitude = 5f;

    [Tooltip("Degrees. Pitch of the camera from horizontal (0 = side-on, 90 = straight down).")]
    [SerializeField] private float _cameraPitchAngle = 50f;

    [Tooltip("World-units per second when panning.")]
    [SerializeField] private float _cameraPanSpeed = 5f;

    [Tooltip("Degrees per second when rotating (lazy-susan).")]
    [SerializeField] private float _cameraRotateSpeed = 90f;

    [Tooltip("World-units per scroll tick when zooming.")]
    [SerializeField] private float _cameraZoomSpeed = 2f;

    [Header("Diagnostics")]
    [Tooltip("How often (real seconds) to log the tick rate to the console.")]
    [SerializeField] private float _logTickRateEverySeconds = 10f;

    // ── Public accessors ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the <see cref="SimConfig"/> this asset resolves to.
    /// Loads from <see cref="_configFilePath"/> when non-empty; otherwise returns compiled defaults.
    /// </summary>
    public SimConfig Config
    {
        get
        {
            if (!string.IsNullOrEmpty(_configFilePath))
            {
                try   { return SimConfig.Load(_configFilePath); }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SimConfigAsset] Failed to load '{_configFilePath}': {ex.Message}. Using defaults.");
                }
            }
            return new SimConfig();
        }
    }

    /// <summary>
    /// Unity-host-specific configuration block, assembled from the Inspector fields above.
    /// The engine never reads this; only Unity-side systems do.
    /// </summary>
    public UnityHostConfig HostConfig => new()
    {
        TicksPerSecond           = _ticksPerSecond,
        RenderFrameRateTarget    = _renderFrameRateTarget,
        PerformanceGateMinFps    = _performanceGateMinFps,
        PerformanceGateMeanFps   = _performanceGateMeanFps,
        PerformanceGateP99Fps    = _performanceGateP99Fps,
        CameraMinAltitude        = _cameraMinAltitude,
        CameraMaxAltitude        = _cameraMaxAltitude,
        CameraPitchAngle         = _cameraPitchAngle,
        CameraPanSpeed           = _cameraPanSpeed,
        CameraRotateSpeed        = _cameraRotateSpeed,
        CameraZoomSpeed          = _cameraZoomSpeed,
        LogTickRateEverySeconds  = _logTickRateEverySeconds,
    };
}
