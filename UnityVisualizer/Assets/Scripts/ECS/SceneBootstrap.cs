using UnityEngine;

/// <summary>
/// Automatically creates the full scene hierarchy when you press Play on
/// any empty scene — no manual Inspector setup required.
///
/// HOW TO USE
/// ──────────
/// 1. Open the default Unity scene (or any empty scene).
/// 2. Press Play.
/// 3. Done.  This class fires automatically via RuntimeInitializeOnLoadMethod.
///
/// If a SimulationManager already exists in the scene this script exits early,
/// so it is safe to leave this class in the project permanently.
///
/// SCENE HIERARCHY CREATED
/// ────────────────────────
///   SimulationManager          — ticks the ECS engine, produces Snapshot each frame
///   WorldSceneBuilder          — reads Snapshot, creates/moves cubes
///   Directional Light          — warm overhead light
///   (Main Camera is repositioned if it already exists, or created if not)
/// </summary>
public static class SceneBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        // Already set up — do nothing
        if (Object.FindObjectOfType<SimulationManager>() != null) return;

        Debug.Log("[SceneBootstrap] Bootstrapping ECS visualiser scene…");

        CreateSimulationManager();
        CreateWorldSceneBuilder();
        SetupDirectionalLight();
        SetupCamera();

        Debug.Log("[SceneBootstrap] Scene ready. SimulationManager + WorldSceneBuilder active.");
    }

    // ── SimulationManager ─────────────────────────────────────────────────────

    private static void CreateSimulationManager()
    {
        var go  = new GameObject("SimulationManager");
        var sim = go.AddComponent<SimulationManager>();
        // speedMultiplier = 1 → normal game speed (120x real-time by default from config)
        sim.speedMultiplier = 1f;
    }

    // ── WorldSceneBuilder ─────────────────────────────────────────────────────

    private static void CreateWorldSceneBuilder()
    {
        var go      = new GameObject("WorldSceneBuilder");
        var builder = go.AddComponent<WorldSceneBuilder>();
        builder.worldScale = 1f;

        // Optional: create tidy parent containers for world objects and entities
        var worldObjRoot = new GameObject("WorldObjects");
        var entityRoot   = new GameObject("Entities");
        builder.worldObjectRoot = worldObjRoot.transform;
        builder.entityRoot      = entityRoot.transform;
    }

    // ── Directional light ─────────────────────────────────────────────────────

    private static void SetupDirectionalLight()
    {
        // Use existing directional light if present; otherwise create one
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
        lt.color     = new Color(1.0f, 0.96f, 0.88f);  // warm white
        lt.intensity = 1.1f;
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    // ── Camera ────────────────────────────────────────────────────────────────
    //
    //  The world is roughly 10×10 units.  Entities walk around in the centre.
    //  Organ strips extend ~6 units to the right (+X) of each entity.
    //
    //  This position looks from the front-left at ~45° down so you can see:
    //    • The floor with world objects (fridge, sink, toilet, bed)
    //    • Entity cubes moving around
    //    • The full organ strip for each entity to their right
    //
    //  You can freely move/rotate the Camera in the Inspector while running.

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
        cam.fieldOfView     = 55f;
        cam.nearClipPlane   = 0.1f;
        cam.farClipPlane    = 200f;

        // 10×10 world: objects at corners (2,2) (7,2) (2,8) (7,8).
        // Camera sits above-left looking across the floor and down the organ strips.
        // Adjust freely in the Inspector while running.
        cam.transform.position = new Vector3(-2f, 14f, -4f);
        cam.transform.LookAt(new Vector3(6f, 0f, 5f));
    }
}
