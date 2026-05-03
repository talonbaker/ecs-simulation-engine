using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime control panel for the floor-room-identity sandbox scene.
/// Provides pixel-art toggle, lighting toggle, and altitude snap buttons.
/// </summary>
public sealed class FloorRoomIdentitySandboxController : MonoBehaviour
{
    private Camera _cam;
    private Light  _dirLight;
    private bool   _lightOn  = true;

    private static readonly float[] Altitudes = { 8f, 15f, 25f, 40f };

    private void Start()
    {
        _cam      = Camera.main;
        _dirLight = FindObjectOfType<Light>();
        BuildUi();
    }

    private void BuildUi()
    {
        var panel = new GameObject("ButtonPanel");
        panel.transform.SetParent(transform, false);

        var rt                  = panel.AddComponent<RectTransform>();
        rt.anchorMin            = new Vector2(0, 0);
        rt.anchorMax            = new Vector2(0, 0);
        rt.pivot                = new Vector2(0, 0);
        rt.anchoredPosition     = new Vector2(10, 10);
        rt.sizeDelta            = new Vector2(180, 220);

        var vlg                  = panel.AddComponent<VerticalLayoutGroup>();
        vlg.spacing              = 4;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        AddButton(panel.transform, "Toggle pixel-art", OnTogglePixelArt);
        AddButton(panel.transform, "Toggle lighting",  OnToggleLighting);
        foreach (var alt in Altitudes)
        {
            float captured = alt;
            AddButton(panel.transform, $"Alt {captured:F0}m", () => OnSnapAltitude(captured));
        }
    }

    private static void AddButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        var go       = new GameObject(label);
        go.transform.SetParent(parent, false);

        var rt       = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 32);

        var img      = go.AddComponent<Image>();
        img.color    = new Color(0.18f, 0.18f, 0.18f, 0.88f);

        var btn      = go.AddComponent<Button>();
        btn.onClick.AddListener(action);

        var txtGo    = new GameObject("Text");
        txtGo.transform.SetParent(go.transform, false);
        var txtRt        = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin  = Vector2.zero;
        txtRt.anchorMax  = Vector2.one;
        txtRt.sizeDelta  = Vector2.zero;

        var txt      = txtGo.AddComponent<Text>();
        txt.text     = label;
        txt.font     = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 13;
        txt.color    = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
    }

    private void OnTogglePixelArt()
    {
        // Pixel-art is toggled by enabling/disabling the PixelArtRendererFeature on the URP renderer.
        // This requires the renderer asset to be accessible at runtime; for the sandbox, a simple
        // QualitySettings swap or direct renderer feature toggle can be done here.
        // For now log a hint — full wiring is in WP-4.0.A1-INT.
        Debug.Log("[SandboxController] Toggle pixel-art: modify URP renderer feature in Edit > Project Settings > Quality.");
    }

    private void OnToggleLighting()
    {
        _lightOn = !_lightOn;
        if (_dirLight != null) _dirLight.enabled = _lightOn;
    }

    private void OnSnapAltitude(float altitude)
    {
        if (_cam == null) return;
        var pos = _cam.transform.position;
        _cam.transform.position = new Vector3(pos.x, altitude, pos.z);
    }
}
