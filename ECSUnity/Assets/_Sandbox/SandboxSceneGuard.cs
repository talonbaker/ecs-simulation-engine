using UnityEngine;

/// <summary>
/// Prevents SceneBootstrapper from polluting sandbox scenes with engine objects.
///
/// SceneBootstrapper runs via [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] in every
/// scene, creating an EngineHost (which loads office-starter.json) and room/NPC renderers.
/// This script runs in Start() — which fires after [AfterSceneLoad] — and destroys those
/// objects before they render anything, then spawns minimal axis cubes for orientation.
/// </summary>
public sealed class SandboxSceneGuard : MonoBehaviour
{
    private void Start()
    {
        // Kill any engine objects SceneBootstrapper created.
        var host = FindObjectOfType<EngineHost>();
        if (host != null) Destroy(host.gameObject);

        var roomRend = FindObjectOfType<RoomRectangleRenderer>();
        if (roomRend != null) Destroy(roomRend.gameObject);

        // Spawn three reference cubes so camera movement is visually verifiable.
        SpawnCube("Origin",  Vector3.zero,                    new Color(1f, 1f, 1f));
        SpawnCube("X+5",     new Vector3(5f, 0f, 0f),         new Color(1f, 0.2f, 0.2f));
        SpawnCube("Z+5",     new Vector3(0f, 0f, 5f),         new Color(0.2f, 0.6f, 1f));
    }

    private static void SpawnCube(string label, Vector3 pos, Color colour)
    {
        var go  = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = label;
        go.transform.position = pos;
        Destroy(go.GetComponent<Collider>());

        var mat = new Material(Shader.Find("Standard")) { color = colour };
        go.GetComponent<Renderer>().material = mat;
    }
}
