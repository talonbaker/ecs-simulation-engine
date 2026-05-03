using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Sandbox controller for the pixel-art-shader scene.
/// P key: toggle PixelArtRendererFeature on/off.
/// Bottom-left HUD: live resolution, palette-quantize, and FPS readout.
/// "Spawn 30" button: instantiate 30 cubes for FPS gate testing.
/// </summary>
[AddComponentMenu("Sandbox/SandboxToggle")]
public class SandboxToggle : MonoBehaviour
{
    [SerializeField, Tooltip("The PixelArtRendererFeature to toggle. Drag from SandboxURP-Renderer asset.")]
    PixelArtRendererFeature _feature;

    [SerializeField, Tooltip("Key that toggles the pixel-art effect.")]
    KeyCode _toggleKey = KeyCode.P;

    [SerializeField, Tooltip("Bottom-left stats Text component.")]
    Text _statsText;

    [SerializeField, Tooltip("Renderer index in URP-PipelineAsset that points to SandboxURP-Renderer.asset.")]
    int _sandboxRendererIndex = 1;

    // FPS over 60 frames
    const int k_FrameSamples = 60;
    readonly float[] _frameTimes = new float[k_FrameSamples];
    int _frameIdx;
    float _avgFps;
    bool _fpsFull;

    void Start()
    {
        // Camera.main requires the "MainCamera" tag; fall back to any active camera.
        Camera cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        cam?.GetComponent<UniversalAdditionalCameraData>()?.SetRenderer(_sandboxRendererIndex);
    }

    void Update()
    {
        if (Input.GetKeyDown(_toggleKey) && _feature != null)
            _feature.SetActive(!_feature.isActive);

        SampleFps();

        if (_statsText != null)
            _statsText.text = BuildStats();
    }

    void SampleFps()
    {
        _frameTimes[_frameIdx % k_FrameSamples] = Time.deltaTime;
        _frameIdx++;
        if (_frameIdx >= k_FrameSamples)
        {
            _fpsFull = true;
            float sum = 0f;
            for (int i = 0; i < k_FrameSamples; i++) sum += _frameTimes[i];
            _avgFps = k_FrameSamples / sum;
        }
    }

    string BuildStats()
    {
        if (_feature == null) return "(no feature assigned)";

        string res  = ResString();
        string q    = _feature.settings.paletteQuantize ? "ON" : "OFF";
        string fx   = _feature.isActive ? "ON" : "OFF";
        string fps  = _fpsFull ? Mathf.RoundToInt(_avgFps).ToString() : "...";

        return $"Resolution: {res}\nPalette Quantize: {q}\nEffect: {fx}\nFPS: {fps}";
    }

    string ResString() =>
        _feature.settings.preset switch
        {
            PixelArtRendererFeature.PixelArtPreset.Crisp  => "480×270 (Crisp)",
            PixelArtRendererFeature.PixelArtPreset.Chunky => "320×180 (Chunky)",
            _                                              =>
                $"{_feature.settings.customResolution.x}×{_feature.settings.customResolution.y} (Custom)",
        };

    /// <summary>Called by the "Spawn 30" UI button.</summary>
    public void SpawnThirty()
    {
        for (int i = 0; i < 30; i++)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = new Vector3(
                Random.Range(-13f, 13f), 0.5f, Random.Range(-13f, 13f));
            cube.name = "SpawnedCube_" + i;
        }
    }
}
