#if WARDEN
using UnityEngine;

/// <summary>
/// Renders a dim top-left HUD overlay identifying this as the Playtest Scene.
/// Compiled out in non-WARDEN (retail) builds.
/// </summary>
public sealed class PlaytestSceneIndicator : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("Top-left HUD overlay text. Compiled out under non-WARDEN.")]
    private string _label = "PLAYTEST SCENE — WARDEN BUILD";

    [SerializeField]
    [Tooltip("Pixel size of the indicator text.")]
    private int _fontSize = 12;

    [SerializeField]
    [Tooltip("Indicator opacity. 0.5 keeps it dim and out of the way.")]
    [Range(0f, 1f)]
    private float _alpha = 0.5f;

    // ── Cached style ──────────────────────────────────────────────────────────

    private GUIStyle _style;

    private void OnGUI()
    {
        if (_style == null)
        {
            _style           = new GUIStyle(GUI.skin.label);
            _style.fontSize  = _fontSize;
            _style.normal.textColor = new Color(1f, 1f, 1f, _alpha);
        }

        GUI.Label(new Rect(8, 8, 400, 24), _label, _style);
    }
}
#endif
