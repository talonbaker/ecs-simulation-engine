using APIFramework.Build;
using APIFramework.Components;
using UnityEngine;

/// <summary>
/// Bridges <see cref="BuildFootprintComponent"/> data onto a Unity GameObject.
/// DragHandler queries this component at drop time to determine footprint-aware
/// placement validity via <see cref="FootprintGeometry.CanStackOn"/>.
///
/// Populated at spawn time by the prop spawn system or by direct Inspector assignment.
/// Attach to any prop GameObject that participates in footprint-aware stacking.
/// </summary>
public sealed class PropFootprintBridge : MonoBehaviour
{
    [Tooltip("Footprint width in tiles (X axis). Minimum 1.")]
    [SerializeField] private int _widthTiles = 1;

    [Tooltip("Footprint depth in tiles (Z axis). Minimum 1.")]
    [SerializeField] private int _depthTiles = 1;

    [Tooltip("World units from the floor to the prop's bottom surface.")]
    [SerializeField] private float _bottomHeight = 0f;

    [Tooltip("World units from BottomHeight to the prop's top surface.")]
    [SerializeField] private float _topHeight = 0.5f;

    [Tooltip("True when other props may rest on this prop's top surface.")]
    [SerializeField] private bool _canStackOnTop = false;

    public int   WidthTiles    => _widthTiles;
    public int   DepthTiles    => _depthTiles;
    public float BottomHeight  => _bottomHeight;
    public float TopHeight     => _topHeight;
    public bool  CanStackOnTop => _canStackOnTop;

    /// <summary>Configure programmatically (used by spawn systems and tests).</summary>
    public void Configure(int widthTiles, int depthTiles, float bottomHeight, float topHeight, bool canStackOnTop)
    {
        _widthTiles    = widthTiles;
        _depthTiles    = depthTiles;
        _bottomHeight  = bottomHeight;
        _topHeight     = topHeight;
        _canStackOnTop = canStackOnTop;
    }

    /// <summary>Populate from a catalog entry.</summary>
    public void SetFromEntry(BuildFootprintEntry entry)
    {
        _widthTiles    = entry.WidthTiles;
        _depthTiles    = entry.DepthTiles;
        _bottomHeight  = entry.BottomHeight;
        _topHeight     = entry.TopHeight;
        _canStackOnTop = entry.CanStackOnTop;
    }

    /// <summary>Returns the equivalent ECS struct for use with <see cref="FootprintGeometry"/>.</summary>
    public BuildFootprintComponent ToComponent() => new()
    {
        WidthTiles    = _widthTiles,
        DepthTiles    = _depthTiles,
        BottomHeight  = _bottomHeight,
        TopHeight     = _topHeight,
        CanStackOnTop = _canStackOnTop,
    };
}
