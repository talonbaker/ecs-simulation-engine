using UnityEngine;

/// <summary>
/// Draws an inverted-hull outline around a <see cref="Selectable"/> when it is
/// selected.
///
/// APPROACH (Option A — inverted hull)
/// ─────────────────────────────────────
/// Creates a child GameObject with the same mesh, scaled up by
/// <see cref="_outlineThickness"/> world-units per side, rendered with
/// front-face culling via <see cref="Outline.shader"/> ("Custom/Outline").
/// Thickness therefore varies slightly with object scale, which is the
/// accepted trade-off for this simpler approach.
///
/// MOUNTING
/// ─────────
/// Attach alongside <see cref="Selectable"/> on any GameObject that has a
/// <see cref="MeshFilter"/>. The outline child is created at runtime; nothing
/// extra to wire in the Inspector.
/// </summary>
[RequireComponent(typeof(Selectable))]
public sealed class OutlineRenderer : MonoBehaviour
{
    [Tooltip("Outline tint colour.")]
    [SerializeField] private Color _outlineColor = new Color(0.4f, 0.85f, 1.0f, 1.0f);

    [Tooltip("World-units of outline scale-up per side. Raise if z-fighting occurs.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float _outlineThickness = 0.05f;

    [Tooltip("Pulse frequency in Hz. 0 = static outline.")]
    [Range(0f, 5f)]
    [SerializeField] private float _pulseSpeed = 0f;

    [Tooltip("Thickness modulation amount added to the base when pulsing.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _pulseAmount = 0f;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private Selectable _selectable;
    private GameObject _outlineGo;
    private Material   _outlineMat;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
        _selectable.OnSelected   += _ => ShowOutline(true);
        _selectable.OnDeselected += _ => ShowOutline(false);

        BuildOutline();
        ShowOutline(false);
    }

    private void OnDestroy()
    {
        if (_outlineGo  != null) Destroy(_outlineGo);
        if (_outlineMat != null) Destroy(_outlineMat);
    }

    private void Update()
    {
        if (_outlineGo == null || !_outlineGo.activeSelf) return;
        if (_pulseSpeed <= 0f || _pulseAmount <= 0f) return;

        float pulse = _pulseAmount * Mathf.Sin(Time.time * 2f * Mathf.PI * _pulseSpeed);
        ApplyScale(_outlineThickness + pulse);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void BuildOutline()
    {
        var mf = GetComponent<MeshFilter>();
        if (mf == null)
        {
            Debug.LogWarning($"[OutlineRenderer] No MeshFilter on '{name}'. Outline will not render.", this);
            return;
        }

        _outlineGo = new GameObject("Outline");
        _outlineGo.transform.SetParent(transform, worldPositionStays: false);

        var childMf = _outlineGo.AddComponent<MeshFilter>();
        childMf.sharedMesh = mf.sharedMesh;

        var shader = Shader.Find("Custom/Outline");
        if (shader == null)
        {
            Debug.LogError("[OutlineRenderer] Shader 'Custom/Outline' not found. " +
                           "Verify Outline.shader is in Assets/Shaders/.", this);
            shader = Shader.Find("Unlit/Color");
        }

        _outlineMat = new Material(shader);
        _outlineMat.SetColor("_Color", _outlineColor);

        var childMr = _outlineGo.AddComponent<MeshRenderer>();
        childMr.sharedMaterial = _outlineMat;

        ApplyScale(_outlineThickness);
    }

    private void ApplyScale(float thickness)
    {
        if (_outlineGo == null) return;
        Vector3 s = transform.lossyScale;
        _outlineGo.transform.localScale = new Vector3(
            1f + 2f * thickness / Mathf.Max(s.x, 0.0001f),
            1f + 2f * thickness / Mathf.Max(s.y, 0.0001f),
            1f + 2f * thickness / Mathf.Max(s.z, 0.0001f)
        );
    }

    private void ShowOutline(bool show)
    {
        if (_outlineGo == null) return;
        _outlineGo.SetActive(show);
        if (show) ApplyScale(_outlineThickness);
    }
}
