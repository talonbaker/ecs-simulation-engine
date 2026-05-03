#if WARDEN
using UnityEngine;

/// <summary>
/// WARDEN-only HUD pill shown in the top-right corner of the screen when the
/// NPC introspection overlay is active (Selected or All mode).
///
/// Rendered via IMGUI so it requires no prefab or Canvas setup.
/// Hidden when the overlay is in Off mode.
/// </summary>
public sealed class IntrospectionStatusPill : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The overlay whose mode this pill reflects. Auto-found if null.")]
    private NpcIntrospectionOverlay _overlay;

    private static GUIStyle _pillStyle;
    private static GUIStyle _bgStyle;

    private void Start()
    {
        if (_overlay == null)
            _overlay = FindObjectOfType<NpcIntrospectionOverlay>();
    }

    private void OnGUI()
    {
        if (_overlay == null || _overlay.Mode == NpcIntrospectionMode.Off) return;

        EnsureStyles();

        const float W = 190f, H = 22f;
        float x = Screen.width - W - 8f;
        const float y = 8f;

        GUI.Box(new Rect(x - 4f, y - 2f, W + 8f, H + 4f), GUIContent.none, _bgStyle);
        GUI.Label(new Rect(x, y, W, H), $"INTROSPECT: {_overlay.Mode}  [F2]", _pillStyle);
    }

    private static void EnsureStyles()
    {
        if (_pillStyle != null) return;

        var bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.12f, 0.92f));
        bgTex.Apply();

        _bgStyle = new GUIStyle(GUI.skin.box);
        _bgStyle.normal.background = bgTex;

        _pillStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize   = 11,
            fontStyle  = FontStyle.Bold,
            alignment  = TextAnchor.MiddleCenter,
        };
        _pillStyle.normal.textColor = new Color(0.2f, 0.95f, 0.45f);
    }
}
#endif
