#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using APIFramework.Components;

/// <summary>
/// Editor utility for WP-4.0.D.
///
/// Generates placeholder floor/wall/door textures programmatically and writes them
/// to Assets/Textures/Floor_*.png, then assigns them to the corresponding materials
/// in Assets/Resources/Materials/.
///
/// Also contains a scene-builder that creates the floor-room-identity sandbox scene
/// with all five floor zones, alternating walls, doors, and trim markers.
/// </summary>
public static class RoomVisualIdentityAssetGenerator
{
    private const string TextureDir  = "Assets/Textures";
    private const string MaterialDir = "Assets/Resources/Materials";

    // ── Texture generation ─────────────────────────────────────────────────────

    [MenuItem("ECS/Generate Room Visual Identity Assets")]
    public static void GenerateAll()
    {
        AssetDatabase.StartAssetEditing();
        try
        {
            EnsureDirectory(TextureDir);
            GenerateCarpetTexture();
            GenerateLinoleumTexture();
            GenerateOfficeTileTexture();
            GenerateConcreteTexture();
            GenerateHardwoodTexture();
            GenerateCubicleWallTexture();
            GenerateStructuralWallTexture();
            GenerateWindowWallTexture();
            GenerateDoorRegularTexture();
            GenerateDoorRestroomTexture();
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        AssignTexturesToMaterials();
        Debug.Log("[RoomVisualIdentityAssetGenerator] All assets generated and assigned.");
    }

    // ── Sandbox scene builder ──────────────────────────────────────────────────

    [MenuItem("ECS/_Sandbox/Build Floor-Room-Identity Scene")]
    public static void BuildSandboxScene()
    {
        const string scenePath = "Assets/_Sandbox/floor-room-identity.unity";

        // Open or create the scene.
        Scene scene;
        if (File.Exists(Path.Combine(Application.dataPath, "../" + scenePath)))
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        else
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Clear existing roots (idempotent rebuild).
        foreach (var go in scene.GetRootGameObjects())
            Object.DestroyImmediate(go);

        // Directional light.
        var lightGo = new GameObject("DirectionalLight");
        var light   = lightGo.AddComponent<Light>();
        light.type      = LightType.Directional;
        light.intensity = 1.0f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Camera.
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam   = camGo.AddComponent<Camera>();
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = new Color(0.10f, 0.10f, 0.12f);
        camGo.transform.position = new Vector3(30f, 15f, 15f);
        camGo.transform.rotation = Quaternion.Euler(60f, 0f, 0f);

        // Load materials.
        Material matCarpet   = Resources.Load<Material>("Materials/Floor_Carpet");
        Material matLinoleum = Resources.Load<Material>("Materials/Floor_Linoleum");
        Material matTile     = Resources.Load<Material>("Materials/Floor_OfficeTile");
        Material matConcrete = Resources.Load<Material>("Materials/Floor_Concrete");
        Material matHardwood = Resources.Load<Material>("Materials/Floor_Hardwood");
        Material matCubicle  = Resources.Load<Material>("Materials/Wall_Cubicle");
        Material matStruct   = Resources.Load<Material>("Materials/Wall_Structural");
        Material matDoor     = Resources.Load<Material>("Materials/Door_Regular");
        Material matRestroom = Resources.Load<Material>("Materials/Door_Restroom");

        // Five floor zones (each 10×8), laid out horizontally.
        Material[] floorMats = { matCarpet, matLinoleum, matTile, matConcrete, matHardwood };
        string[]   zoneNames = { "Carpet", "Linoleum", "OfficeTile", "Concrete", "Hardwood" };

        Shader roomShader = Shader.Find("ECSUnity/RoomTint") ?? Shader.Find("Unlit/Color");

        for (int z = 0; z < 5; z++)
        {
            float originX = z * 10f;

            // Floor quad.
            var floor   = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.name  = $"Floor_{zoneNames[z]}";
            floor.transform.position   = new Vector3(originX + 5f, 0.01f, 4f);
            floor.transform.rotation   = Quaternion.Euler(90f, 0f, 0f);
            floor.transform.localScale = new Vector3(10f, 8f, 1f);
            if (floorMats[z] != null)
                floor.GetComponent<Renderer>().sharedMaterial = floorMats[z];
            Object.DestroyImmediate(floor.GetComponent<Collider>());

            // Zone label (world-space text can't be added without TextMeshPro dep — skip for now).

            // Separator wall at zone right edge (except last zone).
            if (z < 4)
            {
                Material wallMat = (z % 2 == 0) ? matCubicle : matStruct;
                float wallH = (z % 2 == 0) ? 1.2f : 2.5f;  // cubicle wall is shorter

                var wall    = GameObject.CreatePrimitive(PrimitiveType.Quad);
                wall.name   = $"Wall_{(z % 2 == 0 ? "Cubicle" : "Structural")}_{z}";
                wall.transform.position   = new Vector3(originX + 10f, wallH * 0.5f + 0.02f, 4f);
                wall.transform.rotation   = Quaternion.Euler(0f, 90f, 0f);
                wall.transform.localScale = new Vector3(8f, wallH, 1f);

                var wm  = wallMat != null ? new Material(wallMat) : new Material(roomShader);
                wm.SetFloat(Shader.PropertyToID("_Alpha"), 1f);
                wall.GetComponent<Renderer>().sharedMaterial = wm;
                wall.AddComponent<WallTag>();

                // Door in the wall.
                Material doorMat = (z % 2 == 0) ? matRestroom : matDoor;
                var door  = GameObject.CreatePrimitive(PrimitiveType.Quad);
                door.name = $"Door_{(z % 2 == 0 ? "Restroom" : "Regular")}_{z}";
                door.transform.position   = new Vector3(originX + 10f, 1.0f, 4f);
                door.transform.rotation   = Quaternion.Euler(0f, 90f, 0f);
                door.transform.localScale = new Vector3(1.2f, 2.0f, 1f);
                if (doorMat != null)
                    door.GetComponent<Renderer>().sharedMaterial = doorMat;
                Object.DestroyImmediate(door.GetComponent<Collider>());
            }
        }

        // Trim line between each zone pair (thin dark quad at Y=0.015).
        for (int z = 0; z < 4; z++)
        {
            float seamX = (z + 1) * 10f;
            var trim    = GameObject.CreatePrimitive(PrimitiveType.Quad);
            trim.name   = $"Trim_{z}_{z + 1}";
            trim.transform.position   = new Vector3(seamX, 0.015f, 4f);
            trim.transform.rotation   = Quaternion.Euler(90f, 0f, 0f);
            trim.transform.localScale = new Vector3(0.08f, 8f, 1f);
            var tm = new Material(roomShader);
            tm.SetColor(Shader.PropertyToID("_Color"), new Color(0.20f, 0.20f, 0.20f));
            tm.SetFloat(Shader.PropertyToID("_Alpha"), 1f);
            trim.GetComponent<Renderer>().sharedMaterial = tm;
            Object.DestroyImmediate(trim.GetComponent<Collider>());
        }

        // Control panel placeholder — FloorRoomIdentitySandboxController builds the UI at runtime.
        // Add it to a ControlPanel GO; it lives in _Sandbox.asmdef so the editor script
        // adds it via reflection to avoid an assembly-reference dependency.
        var cpGo = new GameObject("ControlPanel");
        var controllerType = System.Type.GetType("FloorRoomIdentitySandboxController, _Sandbox");
        if (controllerType != null)
            cpGo.AddComponent(controllerType);
        else
            Debug.LogWarning("[RoomVisualIdentityAssetGenerator] FloorRoomIdentitySandboxController " +
                             "not found — add it manually to the ControlPanel GO.");

        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[RoomVisualIdentityAssetGenerator] Sandbox scene built and saved: {scenePath}");
    }

    private static void AssignTexturesToMaterials()
    {
        AssignTexture("Floor_Carpet",    "floor_carpet");
        AssignTexture("Floor_Linoleum",  "floor_linoleum");
        AssignTexture("Floor_OfficeTile","floor_officetile");
        AssignTexture("Floor_Concrete",  "floor_concrete");
        AssignTexture("Floor_Hardwood",  "floor_hardwood");
        AssignTexture("Wall_Cubicle",    "wall_cubicle");
        AssignTexture("Wall_Structural", "wall_structural");
        AssignTexture("Wall_Window",     "wall_window");
        AssignTexture("Door_Regular",    "door_regular");
        AssignTexture("Door_Restroom",   "door_restroom");
        AssetDatabase.SaveAssets();
    }

    private static void AssignTexture(string materialName, string textureName)
    {
        string matPath = $"{MaterialDir}/{materialName}.mat";
        string texPath = $"{TextureDir}/{textureName}.png";

        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (mat == null || tex == null) return;

        mat.SetTexture("_MainTex", tex);
        EditorUtility.SetDirty(mat);
    }

    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    private static void SaveTexture(Texture2D tex, string name)
    {
        byte[] png  = tex.EncodeToPNG();
        string path = Path.Combine(Application.dataPath, "Textures", $"{name}.png");
        File.WriteAllBytes(path, png);
        Object.DestroyImmediate(tex);
    }

    // ── Texture authoring helpers ──────────────────────────────────────────────

    private static Texture2D MakeTex(int w, int h) =>
        new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };

    private static void Fill(Texture2D t, Color c)
    {
        var pixels = t.GetPixels32();
        var c32    = (Color32)c;
        for (int i = 0; i < pixels.Length; i++) pixels[i] = c32;
        t.SetPixels32(pixels);
    }

    private static void ScatterNoise(Texture2D t, Color noiseCol, float density, System.Random rng)
    {
        var pixels = t.GetPixels32();
        var c32    = (Color32)noiseCol;
        for (int i = 0; i < pixels.Length; i++)
            if (rng.NextDouble() < density) pixels[i] = c32;
        t.SetPixels32(pixels);
    }

    // Carpet: muted blue-gray base with dithered weave hints.
    private static void GenerateCarpetTexture()
    {
        var t   = MakeTex(64, 64);
        var rng = new System.Random(1);
        Color base_ = new Color(0.62f, 0.65f, 0.70f);
        Color weave = new Color(0.55f, 0.58f, 0.63f);
        Fill(t, base_);
        // Dithered weave: every other pixel in a checkerboard.
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                if ((x + y) % 2 == 0 && rng.NextDouble() < 0.3f)
                    t.SetPixel(x, y, weave);
        t.Apply();
        SaveTexture(t, "floor_carpet");
    }

    // Linoleum: pale yellow-green base with 8×8 tile-line grid.
    private static void GenerateLinoleumTexture()
    {
        var t    = MakeTex(128, 128);
        Color b  = new Color(0.80f, 0.83f, 0.70f);
        Color ln = new Color(0.72f, 0.75f, 0.62f);  // slightly darker grid lines
        Fill(t, b);
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
                if (x % 8 == 0 || y % 8 == 0)
                    t.SetPixel(x, y, ln);
        t.Apply();
        SaveTexture(t, "floor_linoleum");
    }

    // OfficeTile: off-white with 1-pixel speckle (~5% density).
    private static void GenerateOfficeTileTexture()
    {
        var t   = MakeTex(64, 64);
        var rng = new System.Random(3);
        Fill(t, new Color(0.87f, 0.87f, 0.84f));
        ScatterNoise(t, new Color(0.78f, 0.78f, 0.76f), 0.05f, rng);
        t.Apply();
        SaveTexture(t, "floor_officetile");
    }

    // Concrete: gray with 2-pixel aggregate dots (~10% density).
    private static void GenerateConcreteTexture()
    {
        var t   = MakeTex(64, 64);
        var rng = new System.Random(4);
        Fill(t, new Color(0.58f, 0.58f, 0.58f));
        for (int y = 0; y < 64; y += 2)
            for (int x = 0; x < 64; x += 2)
                if (rng.NextDouble() < 0.10f)
                {
                    Color agg = new Color(0.44f, 0.44f, 0.44f);
                    t.SetPixel(x,     y,     agg);
                    t.SetPixel(x + 1, y,     agg);
                    t.SetPixel(x,     y + 1, agg);
                    t.SetPixel(x + 1, y + 1, agg);
                }
        t.Apply();
        SaveTexture(t, "floor_concrete");
    }

    // Hardwood: warm brown base with 1-pixel vertical grain lines.
    private static void GenerateHardwoodTexture()
    {
        var t   = MakeTex(64, 128);
        var rng = new System.Random(5);
        Fill(t, new Color(0.52f, 0.36f, 0.20f));
        // Vertical grain: every 3-6 pixels a slightly darker stripe.
        int x = 0;
        while (x < 64)
        {
            Color grain = new Color(0.43f, 0.29f, 0.15f);
            for (int y = 0; y < 128; y++) t.SetPixel(x, y, grain);
            x += rng.Next(3, 7);
        }
        t.Apply();
        SaveTexture(t, "floor_hardwood");
    }

    // CubicleWall: muted blue-gray with tight weave.
    private static void GenerateCubicleWallTexture()
    {
        var t   = MakeTex(64, 64);
        var rng = new System.Random(6);
        Color b = new Color(0.52f, 0.55f, 0.60f);
        Color w = new Color(0.46f, 0.49f, 0.54f);
        Fill(t, b);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                if (((x % 2) ^ (y % 2)) == 1 && rng.NextDouble() < 0.5f)
                    t.SetPixel(x, y, w);
        t.Apply();
        SaveTexture(t, "wall_cubicle");
    }

    // StructuralWall: off-white with very subtle 2-pixel speckle.
    private static void GenerateStructuralWallTexture()
    {
        var t   = MakeTex(64, 64);
        var rng = new System.Random(7);
        Fill(t, new Color(0.80f, 0.80f, 0.78f));
        ScatterNoise(t, new Color(0.73f, 0.73f, 0.71f), 0.03f, rng);
        t.Apply();
        SaveTexture(t, "wall_structural");
    }

    // WindowWall: structural wall base with a lighter horizontal band (~30% from top).
    private static void GenerateWindowWallTexture()
    {
        var t = MakeTex(64, 64);
        Fill(t, new Color(0.80f, 0.80f, 0.78f));
        // Top 30% = window band (lighter / blueish).
        for (int y = 44; y < 64; y++)
            for (int x = 0; x < 64; x++)
                t.SetPixel(x, y, new Color(0.82f, 0.88f, 0.95f));
        t.Apply();
        SaveTexture(t, "wall_window");
    }

    // Door_Regular: warm wood frame with darker handle area.
    private static void GenerateDoorRegularTexture()
    {
        var t   = MakeTex(32, 64);
        var rng = new System.Random(9);
        Color woodBase  = new Color(0.50f, 0.34f, 0.19f);
        Color woodGrain = new Color(0.42f, 0.27f, 0.13f);
        Fill(t, woodBase);
        // Vertical grain.
        int x2 = 0;
        while (x2 < 32)
        {
            for (int y = 0; y < 64; y++) t.SetPixel(x2, y, woodGrain);
            x2 += rng.Next(3, 7);
        }
        // Handle area: 4×4 dark rectangle on right side at mid-height.
        Color handle = new Color(0.28f, 0.22f, 0.15f);
        for (int y = 26; y < 30; y++)
            for (int x = 26; x < 30; x++)
                t.SetPixel(x, y, handle);
        t.Apply();
        SaveTexture(t, "door_regular");
    }

    // Door_Restroom: same as Regular + simple restroom symbol decal (pixel art, 8×8 centered).
    private static void GenerateDoorRestroomTexture()
    {
        var t   = MakeTex(32, 64);
        var rng = new System.Random(10);
        Color woodBase  = new Color(0.50f, 0.34f, 0.24f);
        Color woodGrain = new Color(0.42f, 0.27f, 0.16f);
        Fill(t, woodBase);
        int x2 = 0;
        while (x2 < 32)
        {
            for (int y = 0; y < 64; y++) t.SetPixel(x2, y, woodGrain);
            x2 += rng.Next(3, 7);
        }
        // Handle.
        Color handle = new Color(0.28f, 0.22f, 0.15f);
        for (int y = 26; y < 30; y++)
            for (int x = 26; x < 30; x++)
                t.SetPixel(x, y, handle);
        // Restroom symbol decal — minimalist 8×8 white figure centered upper third.
        Color sym = Color.white;
        int cx = 16, cy = 48;
        // Head (2×2).
        t.SetPixel(cx,   cy,     sym); t.SetPixel(cx + 1, cy,     sym);
        t.SetPixel(cx,   cy + 1, sym); t.SetPixel(cx + 1, cy + 1, sym);
        // Body (vertical line).
        for (int y = cy - 4; y < cy; y++) t.SetPixel(cx, y, sym);
        // Arms (horizontal).
        for (int x = cx - 2; x <= cx + 2; x++) t.SetPixel(x, cy - 2, sym);
        // Legs.
        for (int y = cy - 8; y < cy - 4; y++)
        {
            t.SetPixel(cx - 1, y, sym);
            t.SetPixel(cx + 1, y, sym);
        }
        t.Apply();
        SaveTexture(t, "door_restroom");
    }
}
#endif
