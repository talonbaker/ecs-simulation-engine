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
        var host = Object.FindObjectOfType<EngineHost>();

        if (host == null)
        {
            Debug.Log("[SceneBootstrapper] Bootstrapping ECSUnity scene...");

            // ── Engine host ───────────────────────────────────────────────────

            var hostGo  = new GameObject("EngineHost");
            host        = hostGo.AddComponent<EngineHost>();

            var configAsset = ScriptableObject.CreateInstance<SimConfigAsset>();
            var configField = typeof(EngineHost).GetField("_configAsset",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            configField?.SetValue(host, configAsset);

            var pathField = typeof(EngineHost).GetField("_worldDefinitionPath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            pathField?.SetValue(host, "office-starter.json");

            // ── Performance monitor ───────────────────────────────────────────

            var monitorGo = new GameObject("FrameRateMonitor");
            monitorGo.AddComponent<FrameRateMonitor>();

            // ── Camera + Light ────────────────────────────────────────────────

            SetupCamera();
            SetupDirectionalLight();

            SpawnDebugQuads();

            Debug.Log("[SceneBootstrapper] Scene bootstrapped: EngineHost + Renderers + Camera + Light.");
        }

        // ── Renderers — always ensure they exist, even in the pre-wired MainScene.
        // The committed scene has an EngineHost but renderers were not added as
        // GameObjects, so nothing renders without this fallback.
        if (Object.FindObjectOfType<RoomRectangleRenderer>() == null)
        {
            var renderGo = new GameObject("Renderers");
            var roomRend = renderGo.AddComponent<RoomRectangleRenderer>();
            var npcRend  = renderGo.AddComponent<NpcDotRenderer>();
            SetPrivateField(roomRend, "_engineHost", host);
            SetPrivateField(npcRend,  "_engineHost", host);
            Debug.Log("[SceneBootstrapper] Added renderers to scene.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SetupCamera()
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

        // Free-fly camera — start directly above origin, angled down toward the NPC cluster.
        if (cam.gameObject.GetComponent<CameraController>() == null)
            cam.gameObject.AddComponent<CameraController>();

        cam.transform.position = new Vector3(0f, 8f, -12f);
        cam.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
    }

    private static void SpawnDebugQuads()
    {
        // Three brightly-lit reference quads placed directly in front of the starting
        // camera position. If these are not visible, there is a renderer / shader problem.
        var mat = new Material(Shader.Find("Unlit/Color"));

        var positions = new[]
        {
            new Vector3(-1.5f, 1f, 0f),
            new Vector3( 0f,   1f, 0f),
            new Vector3( 1.5f, 1f, 0f),
        };
        var colors = new[] { Color.red, Color.green, Color.cyan };

        for (int i = 0; i < 3; i++)
        {
            var go  = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = $"DebugQuad_{i}";
            go.transform.position   = positions[i];
            go.transform.localScale = Vector3.one * 1.5f;

            Object.Destroy(go.GetComponent<Collider>());

            var m = new Material(mat) { color = colors[i] };
            go.GetComponent<Renderer>().material = m;
        }

        Object.Destroy(mat);
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
