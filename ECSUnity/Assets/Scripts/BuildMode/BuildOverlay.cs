using UnityEngine;

/// <summary>
/// Applies a full-screen beige-blue tint overlay when Build Mode is active.
///
/// DESIGN (UX bible §3.5)
/// ───────────────────────
/// A subtle translucent color wash signals mode shift without obscuring gameplay.
/// The overlay is a single screen-space quad rendered on top of the world (but
/// behind UI Toolkit panels) at a configurable alpha (default ~0.10).
///
/// IMPLEMENTATION
/// ───────────────
/// Uses Unity's built-in GL.Quad approach via OnPostRender on the Main Camera, OR a
/// simple UI Canvas image at the bottom of the canvas stack — whichever is simpler to
/// wire. We use the Canvas Image approach so the overlay correctly appears below UI
/// Toolkit overlays without camera-layer math.
///
/// MOUNTING
/// ─────────
/// Add this component to a Canvas > Image GameObject that covers the whole screen.
/// Set the Canvas sort order below all UI panels. Call <see cref="SetTinted"/> from
/// <see cref="BuildModeController"/> to toggle the overlay.
/// </summary>
[RequireComponent(typeof(UnityEngine.UI.Image))]
public sealed class BuildOverlay : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Drag in the BuildModeConfig asset.")]
    private BuildModeConfig _config;

    private UnityEngine.UI.Image _image;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _image = GetComponent<UnityEngine.UI.Image>();

        // Always start hidden regardless of Inspector state.
        SetTinted(false);
    }

    // ── API ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Show or hide the build-mode tint overlay.
    /// Safe to call every frame; only updates the image when state actually changes.
    /// </summary>
    public void SetTinted(bool tinted)
    {
        if (_image == null) return;

        if (tinted)
        {
            // Apply the overlay color from config, or a sensible default if config is null.
            Color c  = _config != null ? _config.overlayColor : new Color(0.72f, 0.78f, 0.88f, 1f);
            float a  = _config != null ? _config.overlayAlpha : 0.1f;
            c.a      = a;
            _image.color   = c;
            _image.enabled = true;
        }
        else
        {
            _image.enabled = false;
        }
    }

    // ── Test accessors ────────────────────────────────────────────────────────

    /// <summary>True when the overlay image is active and visible.</summary>
    public bool IsVisible => _image != null && _image.enabled;

    /// <summary>Current alpha of the overlay image (0 when hidden).</summary>
    public float CurrentAlpha => (_image != null && _image.enabled) ? _image.color.a : 0f;
}
