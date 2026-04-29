using UnityEngine;

/// <summary>
/// Renders a soft ground halo + outline around the currently selected entity (WP-3.1.E AT-01 / AT-03).
///
/// VISUAL
/// ───────
/// - Halo: a flat circle on Y=0 below the entity; white, alpha 0.8; rendered as a quad with a
///   radial-falloff material.
/// - Outline: a thin line renderer encircling the entity's bounding box. Color: white, 2 px.
///
/// This renderer activates when <see cref="SelectionController.SelectionChanged"/> fires with a
/// non-null entity, and deactivates on null or when <see cref="PlayerUIConfig.SelectionVisual"/>
/// switches to CrtBlink mode.
///
/// MOUNTING
/// ─────────
/// Attach to the same GameObject as <see cref="SelectionController"/>.
/// Subscribe to SelectionController.SelectionChanged in Start().
/// </summary>
[RequireComponent(typeof(SelectionController))]
public sealed class SelectionHaloRenderer : MonoBehaviour
{
    [SerializeField] private PlayerUIConfig _uiConfig;

    [Tooltip("Size (diameter) of the ground halo in world units.")]
    [SerializeField] private float _haloSize = 1.2f;

    [Tooltip("Base alpha of the halo.")]
    [SerializeField] private float _haloAlpha = 0.75f;

    // ── Runtime objects ───────────────────────────────────────────────────────

    private GameObject    _haloGo;
    private MeshRenderer  _haloRenderer;
    private Material      _haloMat;

    private LineRenderer  _outlineRenderer;
    private GameObject    _outlineGo;

    private SelectionController _ctrl;
    private SelectableTag       _tracked;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        _ctrl = GetComponent<SelectionController>();
        _ctrl.SelectionChanged += OnSelectionChanged;

        BuildHalo();
        BuildOutline();
        SetVisible(false);
    }

    private void LateUpdate()
    {
        if (_tracked == null) { SetVisible(false); return; }

        // Only active when HaloAndOutline mode is selected.
        bool haloMode = _uiConfig == null
            || _uiConfig.SelectionVisual == SelectionVisualMode.HaloAndOutline;

        if (!haloMode) { SetVisible(false); return; }

        SetVisible(true);

        Vector3 pos = _tracked.transform.position;

        // Halo sits on the floor.
        _haloGo.transform.position = new Vector3(pos.x, 0.01f, pos.z);

        // Outline follows the entity bounding box.
        UpdateOutline(pos);
    }

    private void OnDestroy()
    {
        if (_ctrl != null) _ctrl.SelectionChanged -= OnSelectionChanged;
        if (_haloGo    != null) Destroy(_haloGo);
        if (_outlineGo != null) Destroy(_outlineGo);
        if (_haloMat   != null) Destroy(_haloMat);
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Selection-change handler. Public so tests can drive it directly without
    /// wiring a SelectionController; production code reaches it via the
    /// SelectionController.SelectionChanged event subscription in Start().
    /// </summary>
    public void OnSelectionChanged(SelectableTag tag)
    {
        _tracked = tag;
        if (tag == null) SetVisible(false);
    }

    // ── Visual builders ───────────────────────────────────────────────────────

    private void BuildHalo()
    {
        _haloGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _haloGo.name = "SelectionHalo";
        Destroy(_haloGo.GetComponent<Collider>());
        _haloGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        _haloGo.transform.localScale = Vector3.one * _haloSize;

        _haloMat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
        Color c  = Color.white;
        c.a      = _haloAlpha;
        _haloMat.color = c;
        _haloRenderer  = _haloGo.GetComponent<MeshRenderer>();
        _haloRenderer.material = _haloMat;
        _haloRenderer.sortingOrder = 5;
    }

    private void BuildOutline()
    {
        _outlineGo = new GameObject("SelectionOutline");
        _outlineRenderer = _outlineGo.AddComponent<LineRenderer>();
        _outlineRenderer.loop       = true;
        _outlineRenderer.startWidth = 0.05f;
        _outlineRenderer.endWidth   = 0.05f;
        _outlineRenderer.material   = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
        _outlineRenderer.startColor = Color.white;
        _outlineRenderer.endColor   = Color.white;
        _outlineRenderer.positionCount = 4;
    }

    private void UpdateOutline(Vector3 center)
    {
        float r = _haloSize * 0.5f;
        float y = center.y + 0.02f;
        _outlineRenderer.SetPositions(new Vector3[]
        {
            new Vector3(center.x - r, y, center.z - r),
            new Vector3(center.x + r, y, center.z - r),
            new Vector3(center.x + r, y, center.z + r),
            new Vector3(center.x - r, y, center.z + r),
        });
        _outlineGo.transform.position = center;
    }

    private void SetVisible(bool v)
    {
        if (_haloGo    != null) _haloGo.SetActive(v);
        if (_outlineGo != null) _outlineGo.SetActive(v);
    }

    // ── Test accessors ────────────────────────────────────────────────────────

    /// <summary>True when the halo is currently displayed.</summary>
    public bool IsHaloVisible => _haloGo != null && _haloGo.activeSelf;

    /// <summary>True when the outline is currently displayed.</summary>
    public bool IsOutlineVisible => _outlineGo != null && _outlineGo.activeSelf;
}
