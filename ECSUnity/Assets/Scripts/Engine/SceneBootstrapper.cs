using UnityEngine;

/// <summary>
/// Automatically bootstraps the MainScene hierarchy when Play mode starts.
///
/// HOW IT WORKS
/// ─────────────
/// [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] fires after any scene loads.
/// If the required MonoBehaviours are already in the scene, this method exits early
/// — it is safe to leave this class in the project permanently.
///
/// SCENE HIERARCHY CREATED (when scene is bare)
/// ─────────────────────────────────────────────
///   EngineHost          — boots and ticks the ECS engine
///   RoomRectangleRenderer  — renders rooms as flat quads
///   NpcDotRenderer      — renders NPCs as colored dots
///   FrameRateMonitor    — rolling FPS sampler (always on)
///   Main Camera + CameraController  — single-stick camera
///   Directional Light   — warm top-down overhead
///
/// CONFIGURATION
/// ──────────────
/// The SceneBootstrapper creates a default SimConfigAsset in memory. For a custom
/// config, assign a pre-built SimConfigAsset to the EngineHost in the Inspector
/// after the objects are created — or configure the scene directly in the Unity Editor.
///
/// NOTE: The MainScene.unity committed to source control has these objects pre-wired
/// via the Inspector. This class exists as a fallback for tests and bare scenes.
/// </summary>
public static class SceneBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        // Exit early if the scene already has an EngineHost (e.g. the committed MainScene).
        if (Object.FindObjectOfType<EngineHost>() != null)
            return;

        Debug.Log("[SceneBootstrapper] Bootstrapping ECSUnity scene...");

        // ── Engine host ───────────────────────────────────────────────────────

        var hostGo   = new GameObject("EngineHost");
        var host     = hostGo.AddComponent<EngineHost>();

        // Create a default config asset in memory (no file I/O).
        var configAsset = ScriptableObject.CreateInstance<SimConfigAsset>();
        var configField = typeof(EngineHost).GetField("_configAsset",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        configField?.SetValue(host, configAsset);

        // Point at the StreamingAssets office-starter world.
        var pathField = typeof(EngineHost).GetField("_worldDefinitionPath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        pathField?.SetValue(host, "office-starter.json");

        // ── Renderers ─────────────────────────────────────────────────────────

        var renderGo  = new GameObject("Renderers");
        var roomRend  = renderGo.AddComponent<RoomRectangleRenderer>();
        var npcRend   = renderGo.AddComponent<NpcDotRenderer>();

        SetPrivateField(roomRend, "_engineHost", host);
        SetPrivateField(npcRend,  "_engineHost", host);

        // ── Performance monitor (always on) ──────────────────────────────────

        var monitorGo = new GameObject("FrameRateMonitor");
        monitorGo.AddComponent<FrameRateMonitor>();

        // ── Camera ────────────────────────────────────────────────────────────

        SetupCamera(host, configAsset);

        // ── Light ─────────────────────────────────────────────────────────────

        SetupDirectionalLight();

        Debug.Log("[SceneBootstrapper] Scene bootstrapped: EngineHost + Renderers + Camera + Light.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SetupCamera(EngineHost host, SimConfigAsset configAsset)
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam    = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }

        cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f);
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.fieldOfView     = 60f;
        cam.nearClipPlane   = 0.3f;
        cam.farClipPlane    = 1000f;

        // Add CameraController if not already present.
        var controller = cam.gameObject.GetComponent<CameraController>()
                      ?? cam.gameObject.AddComponent<CameraController>();

        // Optionally wire the host so the controller can read HostConfig.
        SetPrivateField(controller, "_engineHost", host);

        // Position camera for office-starter layout (roughly 60x60 tile world).
        // Start looking at the centre of the first floor at the default altitude.
        var startFocus  = new UnityEngine.Vector3(30f, 0f, 20f);
        SetPrivateField(controller, "_startFocusPoint", startFocus);
    }

    private static void SetupDirectionalLight()
    {
        // Reuse existing directional light if the scene already has one.
        var existing = Object.FindObjectOfType<Light>();
        if (existing != null && existing.type == LightType.Directional)
        {
            existing.color     = new Color(1.0f, 0.96f, 0.88f);
            existing.intensity = 1.1f;
            existing.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            return;
        }

        var go = new GameObject("Directional Light");
        var lt = go.AddComponent<Light>();
        lt.type      = LightType.Directional;
        lt.color     = new Color(1.0f, 0.96f, 0.88f);
        lt.intensity = 1.1f;
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(target, value);
    }
}
