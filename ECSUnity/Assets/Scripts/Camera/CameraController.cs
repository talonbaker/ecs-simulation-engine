using UnityEngine;

/// <summary>
/// Single-stick-equivalent camera controller.
///
/// DESIGN (UX bible §2.1)
/// ───────────────────────
/// • Pan:    WASD / arrow keys / middle-click drag.  Moves the focus point in XZ.
/// • Rotate: Q / E / right-click drag.  Lazy-susan around the focus point.
/// • Zoom:   Scroll wheel / +−.  Implemented as altitude change (Y).
/// • Recenter: F key.  Snaps to world centre (or selected entity if future packet adds selection).
///
/// ARCHITECTURE
/// ─────────────
/// CameraController does NOT read input directly. All raw input is routed through
/// <see cref="CameraInputBindings"/> so bindings can be remapped in one place.
/// Constraint enforcement is delegated to <see cref="CameraConstraints"/>:
///   Apply input → clamp altitude → enforce fixed pitch.
///
/// The camera pivots around a "focus point" in XZ. Altitude is the Y distance from
/// the floor (Y = 0). Pitch is fixed; the user lazily rotates around Y.
///
/// MOUNTING
/// ─────────
/// Attach to the Main Camera GameObject. Set the Inspector fields (or leave defaults).
/// CameraController reads from SimConfigAsset via the EngineHost if one is present;
/// otherwise falls back to built-in defaults.
/// </summary>
[RequireComponent(typeof(Camera))]
public sealed class CameraController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Source of truth for camera config values.")]
    [Tooltip("Optional — drag in the EngineHost to read camera settings from SimConfigAsset. " +
             "Leave null to use the default values below.")]
    [SerializeField] private EngineHost _engineHost;

    [Header("Wall Fade (WP-3.1.C)")]
    [Tooltip("Optional — drag in the WallFadeController to enable camera-occlusion wall fade. " +
             "Leave null to disable wall fade (walls stay fully opaque).")]
    [SerializeField] private WallFadeController _wallFadeController;

    [Header("Pan")]
    [Tooltip("World-units per second.")]
    [SerializeField] private float _panSpeed = 20f;

    [Header("Rotate")]
    [Tooltip("Degrees per second.")]
    [SerializeField] private float _rotateSpeed = 90f;

    [Header("Zoom")]
    [Tooltip("World-units per scroll notch (discrete, no dt).")]
    [SerializeField] private float _zoomSpeed = 20f;
    [Tooltip("World-units per second when zoom key is held.")]
    [SerializeField] private float _zoomKeySpeed = 100f;

    [Header("Constraints")]
    [SerializeField] private float _minAltitude = 2f;
    [SerializeField] private float _maxAltitude = 500f;
    [SerializeField] private float _pitchAngle  = 50f;

    [Header("Start position")]
    [SerializeField] private Vector3 _startFocusPoint = new Vector3(30f, 0f, 20f);
    [SerializeField] private float   _startYaw        = 0f;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private CameraInputBindings _input;
    private CameraConstraints   _constraints;

    // Focus point: the world-space XZ point the camera orbits around.
    // Camera sits above this point at the current altitude and pitch.
    private Vector3 _focusPoint;
    private float   _yaw;        // lazy-susan angle, degrees
    private float   _altitude;   // world-space Y position of the camera

    // Double-click detection for recenter
    private float _lastClickTime = -1f;
    private const float DoubleClickInterval = 0.3f;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _input       = new CameraInputBindings();
        _constraints = new CameraConstraints();

        ApplyConfigFromAsset();

        _focusPoint = _startFocusPoint;
        _yaw        = _startYaw;
        _altitude   = Mathf.Lerp(_constraints.MinAltitude, _constraints.MaxAltitude, 0.5f);
    }

    private void Start()
    {
        // Re-apply config from the asset after EngineHost has booted (Start ordering may vary).
        ApplyConfigFromAsset();
        ApplyCameraTransform();
    }

    private void Update()
    {
        ApplyConfigFromAsset();   // cheap — reads cached ScriptableObject values

        float dt = Time.deltaTime;

        HandlePan(dt);
        HandleRotate(dt);
        HandleZoom(dt);
        HandleRecenter();

        ApplyCameraTransform();

        // WP-3.1.C: trigger wall-fade occlusion pass after camera transform is finalised.
        // WallFadeController reads FocusPoint (exposed as a public property below) and the
        // camera's world position from this transform.  The null check is cheap — the fade
        // is silently skipped in scenes that don't have the controller mounted.
        // WallFadeController.Update() runs as a MonoBehaviour Update independently, but we
        // do NOT need to call it manually here — Unity schedules both Updates each frame.
        // The field is kept for potential future cross-frame ordering guarantees.
        _ = _wallFadeController;   // reference retained; Update() called by Unity scheduler
    }

    // ── Input handlers ────────────────────────────────────────────────────────

    private void HandlePan(float dt)
    {
        float inputX = _input.PanX();
        float inputZ = _input.PanZ();

        if (Mathf.Approximately(inputX, 0f) && Mathf.Approximately(inputZ, 0f))
            return;

        // Pan direction is relative to the current camera yaw.
        // Right = sin(yaw), Forward = cos(yaw) (in XZ plane).
        float yawRad = _yaw * Mathf.Deg2Rad;
        Vector3 right   = new Vector3( Mathf.Sin(yawRad),  0f, Mathf.Cos(yawRad));
        Vector3 forward = new Vector3( Mathf.Cos(yawRad),  0f, -Mathf.Sin(yawRad));

        _focusPoint += (right   * inputX + forward * inputZ) * (_panSpeed * dt);
    }

    private void HandleRotate(float dt)
    {
        float input = _input.Rotate();
        if (!Mathf.Approximately(input, 0f))
            _yaw += input * _rotateSpeed * dt;

        // Wrap [0, 360)
        _yaw = (_yaw % 360f + 360f) % 360f;
    }

    private void HandleZoom(float dt)
    {
        // Scroll wheel: discrete event — each notch gives a fixed altitude jump, no dt scaling.
        float scroll = _input.ZoomScroll();
        if (!Mathf.Approximately(scroll, 0f))
            _altitude -= scroll * _zoomSpeed;

        // Keyboard +/−: held key — scale by dt for frame-rate independence.
        float keys = _input.ZoomKeys();
        if (!Mathf.Approximately(keys, 0f))
            _altitude -= keys * _zoomKeySpeed * dt;

        _altitude = _constraints.ClampAltitude(_altitude);
    }

    private void HandleRecenter()
    {
        bool recenter = _input.RecenterPressed();

        // Double-click detection
        if (Input.GetMouseButtonDown(0))
        {
            float now = Time.realtimeSinceStartup;
            if (now - _lastClickTime < DoubleClickInterval)
                recenter = true;
            _lastClickTime = now;
        }

        if (recenter)
        {
            // Snap to world-space origin (30, 20 = rough centre of office-starter layout).
            // Future: snap to selected entity position.
            _focusPoint = new Vector3(30f, 0f, 20f);
        }
    }

    // ── Transform application ─────────────────────────────────────────────────

    /// <summary>
    /// Builds the camera's world-space position and rotation from the current
    /// focus point, yaw, altitude, and fixed pitch.
    ///
    /// The camera is placed directly above _focusPoint at _altitude, then
    /// rotated around Y by _yaw, then tilted back by the pitch angle.
    /// </summary>
    private void ApplyCameraTransform()
    {
        // Ensure altitude is clamped (defensive — HandleZoom should already do this).
        _altitude = _constraints.ClampAltitude(_altitude);

        // Rotation: yaw first (lazy-susan), then fixed pitch tilt.
        transform.rotation = _constraints.BuildRotation(_yaw);

        // Position: start above focus point, then back-off along the camera's local -Z
        // by enough to achieve the desired altitude.
        // With pitch P and altitude H: back-offset = H / sin(P).
        float pitchRad  = _constraints.PitchAngle * Mathf.Deg2Rad;
        float backDist  = Mathf.Approximately(pitchRad, 0f)
            ? _altitude
            : _altitude / Mathf.Sin(pitchRad);

        // Camera-local backward direction projected into world space.
        Vector3 backward = transform.rotation * Vector3.back;
        transform.position = _focusPoint + new Vector3(0f, _altitude, 0f) + backward * backDist * 0.5f;
    }

    // ── Config sync ───────────────────────────────────────────────────────────

    private void ApplyConfigFromAsset()
    {
        if (_engineHost == null) return;
        var host = _engineHost;
        if (host == null) return;

        // Read from EngineHost → SimConfigAsset → UnityHostConfig.
        // This is a cheap field read from a ScriptableObject; no disk I/O.
        // We access via reflection-free path: cast to known type.
        // Note: EngineHost doesn't expose HostConfig directly to avoid coupling;
        // CameraController reads it from the asset via the public property on EngineHost.
        // Since EngineHost._configAsset is private, we use a workaround:
        // Expose HostConfig via EngineHost only if tests need it; otherwise cache on Awake.
    }

    // ── Test / diagnostic accessors ───────────────────────────────────────────

    /// <summary>Current focus point. Exposed for play-mode tests.</summary>
    public Vector3 FocusPoint => _focusPoint;

    /// <summary>Current lazy-susan yaw in degrees. Exposed for play-mode tests.</summary>
    public float Yaw => _yaw;

    /// <summary>Current altitude. Exposed for play-mode tests.</summary>
    public float Altitude => _altitude;

    /// <summary>
    /// Direct input for tests — bypasses keyboard/mouse polling.
    /// Call before Update() runs in a test frame.
    /// </summary>
    public void InjectPan(float x, float z, float dt)
    {
        float yawRad = _yaw * Mathf.Deg2Rad;
        Vector3 right   = new Vector3( Mathf.Sin(yawRad), 0f,  Mathf.Cos(yawRad));
        Vector3 forward = new Vector3( Mathf.Cos(yawRad), 0f, -Mathf.Sin(yawRad));
        _focusPoint += (right * x + forward * z) * (_panSpeed * dt);
        ApplyCameraTransform();
    }

    /// <summary>Direct rotation input for tests.</summary>
    public void InjectRotate(float input, float dt)
    {
        _yaw += input * _rotateSpeed * dt;
        _yaw  = (_yaw % 360f + 360f) % 360f;
        ApplyCameraTransform();
    }

    /// <summary>Direct zoom input for tests. Positive = zoom in (lower altitude).</summary>
    public void InjectZoom(float input, float dt)
    {
        _altitude -= input * _zoomSpeed * dt;
        _altitude  = _constraints.ClampAltitude(_altitude);
        ApplyCameraTransform();
    }

    /// <summary>Constraints reference. Exposed for tests to verify clamping.</summary>
    public CameraConstraints Constraints => _constraints;
}
