using UnityEngine;

/// <summary>
/// CRT-blinking-box selection cue (WP-3.1.E AT-03).
///
/// VISUAL
/// ───────
/// A flat rectangular outline framing the selected entity, blinking at
/// terminal-cursor cadence (500ms on, 500ms off). Color: phosphor green muted
/// (#3CB371 in [0,1] space = (0.235, 0.702, 0.443)).
///
/// Only active when <see cref="PlayerUIConfig.SelectionVisual"/> ==
/// <see cref="SelectionVisualMode.CrtBlink"/>.
///
/// MOUNTING
/// ─────────
/// Attach to the same GameObject as <see cref="SelectionController"/>.
/// </summary>
[RequireComponent(typeof(SelectionController))]
public sealed class SelectionCrtBlinkRenderer : MonoBehaviour
{
    [SerializeField] private PlayerUIConfig _uiConfig;

    [Tooltip("On/off period in seconds for the blink cycle.")]
    [SerializeField] private float _blinkHalfPeriod = 0.5f;

    // Phosphor green (#3CB371).
    private static readonly Color PhosphorGreen = new Color(0.235f, 0.702f, 0.443f, 1f);

    // ── Runtime state ─────────────────────────────────────────────────────────

    private GameObject      _boxGo;
    private LineRenderer    _lineRenderer;
    private SelectionController _ctrl;
    private SelectableTag   _tracked;
    private float           _blinkTimer;
    private bool            _blinkOn;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        _ctrl = GetComponent<SelectionController>();
        _ctrl.SelectionChanged += OnSelectionChanged;

        _boxGo = new GameObject("CrtSelectionBox");
        _lineRenderer = _boxGo.AddComponent<LineRenderer>();
        _lineRenderer.loop           = true;
        _lineRenderer.positionCount  = 4;
        _lineRenderer.startWidth     = 0.06f;
        _lineRenderer.endWidth       = 0.06f;
        _lineRenderer.material       = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
        _lineRenderer.startColor     = PhosphorGreen;
        _lineRenderer.endColor       = PhosphorGreen;
        _boxGo.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_tracked == null) { _boxGo?.SetActive(false); return; }

        bool crtMode = _uiConfig != null &&
                       _uiConfig.SelectionVisual == SelectionVisualMode.CrtBlink;

        if (!crtMode) { _boxGo?.SetActive(false); return; }

        // Advance blink timer.
        _blinkTimer += Time.unscaledDeltaTime;
        if (_blinkTimer >= _blinkHalfPeriod)
        {
            _blinkTimer = 0f;
            _blinkOn    = !_blinkOn;
        }

        _boxGo.SetActive(_blinkOn);

        if (_blinkOn)
        {
            Vector3 center = _tracked.transform.position;
            float r = 0.6f;
            float y = center.y + 0.03f;
            _lineRenderer.SetPositions(new Vector3[]
            {
                new Vector3(center.x - r, y, center.z - r),
                new Vector3(center.x + r, y, center.z - r),
                new Vector3(center.x + r, y, center.z + r),
                new Vector3(center.x - r, y, center.z + r),
            });
        }
    }

    private void OnDestroy()
    {
        if (_ctrl != null) _ctrl.SelectionChanged -= OnSelectionChanged;
        if (_boxGo != null) Destroy(_boxGo);
    }

    /// <summary>
    /// Selection-change handler. Public so tests can drive it directly without
    /// wiring a SelectionController; production code reaches it via the
    /// SelectionController.SelectionChanged event subscription in Start().
    /// </summary>
    public void OnSelectionChanged(SelectableTag tag)
    {
        _tracked    = tag;
        _blinkTimer = 0f;
        _blinkOn    = false;
        if (_boxGo != null && tag == null) _boxGo.SetActive(false);
    }

    // ── Test accessors ────────────────────────────────────────────────────────

    /// <summary>True when the blinking box is currently in its "on" phase and visible.</summary>
    public bool IsBlinkVisible => _boxGo != null && _boxGo.activeSelf;

    /// <summary>True when a CRT-blink entity is being tracked (regardless of blink phase).</summary>
    public bool IsTracking => _tracked != null;
}
