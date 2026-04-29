namespace ECSUnity.Config
{
    /// <summary>
    /// Configuration values consumed exclusively by the ECSUnity host.
    /// The headless engine, CLI, and Avalonia visualiser ignore this block entirely.
    ///
    /// Lives in the ECSUnity assembly (not APIFramework) so that the engine remains
    /// host-agnostic per SRD §8.7. Values are mirrored under the "unityHost" key in
    /// SimConfig.json for discoverability — the engine deserialiser sees that key as
    /// an unknown property and ignores it; the Unity host reads it via SimConfigAsset.
    /// </summary>
    public class UnityHostConfig
    {
        // Tick / render
        public int   TicksPerSecond           { get; set; } = 50;
        public int   RenderFrameRateTarget    { get; set; } = 60;

        // Performance gate thresholds (AT-11 — do not weaken)
        public float PerformanceGateMinFps    { get; set; } = 55f;
        public float PerformanceGateMeanFps   { get; set; } = 58f;
        public float PerformanceGateP99Fps    { get; set; } = 50f;

        // Camera
        public float CameraMinAltitude        { get; set; } = 3f;
        public float CameraMaxAltitude        { get; set; } = 5f;
        public float CameraPitchAngle         { get; set; } = 50f;
        public float CameraPanSpeed           { get; set; } = 5f;
        public float CameraRotateSpeed        { get; set; } = 90f;
        public float CameraZoomSpeed          { get; set; } = 2f;

        // Diagnostics
        public float LogTickRateEverySeconds  { get; set; } = 10f;
    }
}
