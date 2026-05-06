using UnityEngine;

/// <summary>
/// Manages the translucent ghost preview shown while the player is placing an item
/// or repositioning an existing entity in Build Mode (WP-3.1.D).
///
/// BEHAVIOR
/// ─────────
///   - Follows the cursor (snapped to grid) in real time.
///   - Tints white when placement is valid; red when invalid.
///   - Hidden when no intent is active.
///   - Does NOT commit the placement — that is handled by <see cref="BuildModeController"/>.
///
/// VISUAL APPROACH
/// ────────────────
/// At v0.1 the ghost is a simple 1×1 cube mesh with an unlit transparent material.
/// When future art assets ship, swap the mesh via <see cref="SetGhostMesh"/>.
///
/// MOUNTING
/// ─────────
/// Attach to the same GameObject as <see cref="BuildModeController"/>. The controller
/// calls <see cref="Activate"/>, <see cref="MoveTo"/>, <see cref="SetValid"/>, and
/// <see cref="Deactivate"/> each Update().
/// </summary>
public sealed class GhostPreview : MonoBehaviour
{
    // ── Inspector ───────────────────────────────��─────────────────────────────

    [SerializeField]
    [Tooltip("Config for ghost alpha and tint colors.")]
    private BuildModeConfig _config;

    // ── Runtime state ────────────────────────────────���────────────────────────

    private GameObject _ghostGo;
    private MeshRenderer _renderer;
    private Material _mat;

    private bool _isActive;
    private bool _isValid;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Build a simple 1×1 cube as the default ghost mesh.
        // A real item-specific mesh can be assigned via SetGhostMesh().
        _ghostGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _ghostGo.name = "BuildGhostPreview";
        _ghostGo.transform.SetParent(null); // world-space root

        // Remove the collider so it does not interfere with placement validation raycasts.
        Destroy(_ghostGo.GetComponent<Collider>());

        _renderer = _ghostGo.GetComponent<MeshRenderer>();

        // Prefer the URP unlit shader; fall back to built-in for non-URP projects.
        _mat = new Material(
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Standard"));
        _renderer.material = _mat;

        SetValid(true);
        _ghostGo.SetActive(false);
        _isActive = false;
    }

    private void OnDestroy()
    {
        if (_ghostGo != null) Destroy(_ghostGo);
        if (_mat     != null) Destroy(_mat);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Show the ghost at the given world position with the given label.
    /// Call this once when intent is first set.
    /// </summary>
    public void Activate(Vector3 worldPos)
    {
        if (_ghostGo == null) return;
        _ghostGo.transform.position = worldPos;
        _ghostGo.SetActive(true);
        _isActive = true;
    }

    /// <summary>Move the ghost to a new snapped world position.</summary>
    public void MoveTo(Vector3 worldPos)
    {
        if (_ghostGo == null) return;
        _ghostGo.transform.position = worldPos;
    }

    /// <summary>Rotate the ghost by <paramref name="degrees"/> around the Y axis.</summary>
    public void Rotate(float degrees)
    {
        if (_ghostGo == null) return;
        _ghostGo.transform.Rotate(Vector3.up, degrees, Space.World);
    }

    /// <summary>
    /// Update the ghost tint to reflect validity.
    /// White = valid; red = invalid.
    /// </summary>
    public void SetValid(bool valid)
    {
        if (_mat == null) return;
        _isValid = valid;

        if (_config != null)
        {
            Color baseColor = valid ? _config.ghostColorValid : _config.ghostColorInvalid;
            float alpha     = valid ? _config.ghostAlphaValid : _config.ghostAlphaInvalid;
            baseColor.a = alpha;
            _mat.color  = baseColor;
        }
        else
        {
            // Fallback colors if config is not wired.
            _mat.color = valid
                ? new Color(1f, 1f, 1f, 0.5f)
                : new Color(1f, 0.2f, 0.2f, 0.5f);
        }
    }

    /// <summary>Hide and reset the ghost. Call when intent is cleared.</summary>
    public void Deactivate()
    {
        if (_ghostGo == null) return;
        _ghostGo.SetActive(false);
        _ghostGo.transform.rotation = Quaternion.identity;
        _isActive = false;
    }

    /// <summary>
    /// Replace the ghost mesh with a custom one (for item-specific previews).
    /// Pass null to revert to the default cube.
    /// </summary>
    public void SetGhostMesh(Mesh mesh)
    {
        if (_ghostGo == null) return;
        var mf = _ghostGo.GetComponent<MeshFilter>();
        if (mf == null) mf = _ghostGo.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh != null ? mesh : new Mesh(); // fallback to empty mesh
    }

    // ── Test accessors ────────────────────────────────────────────────────────

    /// <summary>True when the ghost is currently shown.</summary>
    public bool IsActive => _isActive && _ghostGo != null && _ghostGo.activeSelf;

    /// <summary>Current world position of the ghost.</summary>
    public Vector3 Position => _ghostGo != null ? _ghostGo.transform.position : Vector3.zero;

    /// <summary>True when the ghost is showing a valid (white) tint.</summary>
    public bool IsShowingValid => _isValid;

    /// <summary>Current ghost material color (includes alpha).</summary>
    public Color GhostColor => _mat != null ? _mat.color : Color.clear;
}
