using UnityEngine;

/// <summary>
/// Draws a parameterised XZ reference grid for sandbox scenes.
///
/// Position the GameObject in the scene to anchor the grid's corner at that
/// world position.  The grid grows along +X and +Z for <see cref="_sizeTiles"/>
/// tiles in each direction.
///
/// Default _sizeTiles = 10 (suitable for selection/drag sandbox scenes).
/// The full-world 60×60 variant uses the older ReferenceGrid.cs.
/// </summary>
[ExecuteAlways]
public sealed class SmallReferenceGrid : MonoBehaviour
{
    [Tooltip("Grid side length in tiles. E.g. 10 = 10×10 grid.")]
    [SerializeField] private int _sizeTiles = 10;

    [Tooltip("Distance between grid lines in world units. 1.0 = one line per tile; 0.5 = two lines per tile.")]
    [Range(0.1f, 5f)]
    [SerializeField] private float _lineSpacing = 1.0f;

    // Lighter grey than the full-world grid to avoid visual distraction.
    private static readonly Color GridColour = new Color(0.45f, 0.45f, 0.45f);

    private Material _lineMaterial;

    private void OnEnable()
    {
        var shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) return;

        _lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _lineMaterial.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
        _lineMaterial.SetInt("_ZWrite",   0);
    }

    private void OnDisable()
    {
        if (_lineMaterial != null)
            DestroyImmediate(_lineMaterial);
    }

    private void OnRenderObject()
    {
        if (_lineMaterial == null) return;

        _lineMaterial.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(transform.localToWorldMatrix);
        GL.Begin(GL.LINES);
        GL.Color(GridColour);

        float size      = _sizeTiles;
        int   lineCount = Mathf.RoundToInt(_sizeTiles / _lineSpacing);
        for (int i = 0; i <= lineCount; i++)
        {
            float p = i * _lineSpacing;
            // Lines parallel to X (constant Z = p)
            GL.Vertex3(0f,  0f, p);
            GL.Vertex3(size, 0f, p);
            // Lines parallel to Z (constant X = p)
            GL.Vertex3(p, 0f, 0f);
            GL.Vertex3(p, 0f, size);
        }

        GL.End();
        GL.PopMatrix();
    }
}
