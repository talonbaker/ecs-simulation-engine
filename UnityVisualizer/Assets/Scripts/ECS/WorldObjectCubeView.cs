using UnityEngine;
using APIFramework.Core;

/// <summary>
/// Renders a single world object (fridge, sink, toilet, bed) as a coloured primitive cube.
/// Spawned and updated by WorldSceneBuilder each frame.
/// </summary>
public class WorldObjectCubeView : MonoBehaviour
{
    private Renderer  _renderer;
    private Material  _mat;
    private TextMesh  _label;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _mat      = new Material(Shader.Find("Standard"));
        _renderer.material = _mat;

        // Floating label above the object
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(transform, worldPositionStays: false);
        labelGo.transform.localScale = Vector3.one * 0.14f;
        _label              = labelGo.AddComponent<TextMesh>();
        _label.alignment    = TextAlignment.Center;
        _label.anchor       = TextAnchor.LowerCenter;
        _label.fontSize     = 28;
        _label.color        = Color.white;
    }

    public void UpdateFromSnapshot(WorldObjectSnapshot obj, float worldScale)
    {
        _mat.color = EcsColors.ForWorldObject(obj);

        // Stock count shown for the fridge
        string extra = obj.IsFridge && obj.StockCount >= 0
            ? $"\n[{obj.StockCount} bananas]"
            : string.Empty;
        _label.text = $"{obj.Name}{extra}";

        // Lift label above the cube top
        float halfHeight = transform.localScale.y * 0.5f;
        _label.transform.localPosition = new Vector3(0f, halfHeight + 0.4f, 0f);
    }
}
