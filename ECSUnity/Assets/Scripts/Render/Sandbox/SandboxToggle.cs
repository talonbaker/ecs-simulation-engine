using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Sandbox controller for the pixel-art-shader scene.
/// P key: toggle PixelArtRendererFeature on/off.
/// Bottom-left HUD: live dither strength, effect state, and FPS readout.
/// "Spawn 30" button: instantiate 30 cubes for FPS gate testing.
/// </summary>
[AddComponentMenu("Sandbox/SandboxToggle")]
public class SandboxToggle : MonoBehaviour
{
    [SerializeField, Tooltip("The PixelArtRendererFeature to toggle. Drag from URP-PipelineAsset_Renderer asset.")]
    PixelArtRendererFeature _feature;

    [SerializeField, Tooltip("Key that toggles the pixel-art effect.")]
    KeyCode _toggleKey = KeyCode.P;

    [SerializeField, Tooltip("Bottom-left stats Text component.")]
    Text _statsText;

    // FPS over 60 frames
    const int k_FrameSamples = 60;
    readonly float[] _frameTimes = new float[k_FrameSamples];
    int _frameIdx;
    float _avgFps;
    bool _fpsFull;

    void Awake()
    {
        _feature?.SetActive(true);
    }

    void OnDestroy()
    {
        _feature?.SetActive(false);
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

        string dither = _feature.settings.paletteQuantize
            ? $"{_feature.settings.ditherStrength:F2}"
            : "OFF";
        string fx  = _feature.isActive ? "ON" : "OFF";
        string fps = _fpsFull ? Mathf.RoundToInt(_avgFps).ToString() : "...";

        return $"Dither: {dither}\nEffect: {fx}\nFPS: {fps}";
    }

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
