using UnityEngine;

/// <summary>
/// Draws a 60×60 XZ reference grid and places origin/centre markers and axis labels.
/// All visual elements are created procedurally in Awake; nothing extra to wire in Inspector.
/// </summary>
[ExecuteAlways]
public sealed class ReferenceGrid : MonoBehaviour
{
    private static readonly Color GridColour   = new Color(0.3f, 0.3f, 0.3f);
    private static readonly Color OriginColour = new Color(1.0f, 0.15f, 0.15f);
    private static readonly Color CentreColour = new Color(0.0f, 0.9f, 0.9f);

    private Material _lineMaterial;

    private void Awake()
    {
        if (transform.childCount > 0) return;

        SpawnMarker("Origin (0,0,0)",   new Vector3( 0f, 0.01f,  0f), OriginColour, 2f);
        SpawnMarker("Centre (30,0,20)", new Vector3(30f, 0.01f, 20f), CentreColour, 2f);
        SpawnLabel("X = +", new Vector3(50f, 0.5f, 20f));
        SpawnLabel("Z = +", new Vector3(30f, 0.5f, 50f));
    }

    private void OnEnable()
    {
        var shader = Shader.Find("Hidden/Internal-Colored");
        _lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _lineMaterial.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
        _lineMaterial.SetInt("_ZWrite",   0);
    }

    private void OnRenderObject()
    {
        if (_lineMaterial == null) return;

        _lineMaterial.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);
        GL.Begin(GL.LINES);
        GL.Color(GridColour);

        for (int i = 0; i <= 60; i++)
        {
            // Lines parallel to X axis (constant Z = i)
            GL.Vertex3( 0f, 0f, i);
            GL.Vertex3(60f, 0f, i);
            // Lines parallel to Z axis (constant X = i)
            GL.Vertex3(i, 0f,  0f);
            GL.Vertex3(i, 0f, 60f);
        }

        GL.End();
        GL.PopMatrix();
    }

    private void SpawnMarker(string goName, Vector3 pos, Color colour, float size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = goName;
        go.transform.SetParent(transform);
        go.transform.position    = pos;
        go.transform.rotation    = Quaternion.Euler(-90f, 0f, 0f);
        go.transform.localScale  = new Vector3(size, size, 1f);

        var mat = new Material(Shader.Find("Unlit/Color")) { color = colour };
        go.GetComponent<Renderer>().sharedMaterial = mat;

        DestroyImmediate(go.GetComponent<Collider>());
    }

    private void SpawnLabel(string text, Vector3 pos)
    {
        var go = new GameObject(text);
        go.transform.SetParent(transform);
        go.transform.position   = pos;
        go.transform.rotation   = Quaternion.Euler(-90f, 0f, 0f);
        go.transform.localScale = Vector3.one * 0.5f;

        var tm       = go.AddComponent<TextMesh>();
        tm.text      = text;
        tm.fontSize  = 24;
        tm.color     = Color.white;
        tm.anchor    = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
    }
}
