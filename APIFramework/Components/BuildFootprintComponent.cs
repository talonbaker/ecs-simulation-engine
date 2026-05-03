using APIFramework.Core;

namespace APIFramework.Components;

/// <summary>
/// Per-prop occupancy footprint. Declares what tile area a prop covers,
/// its surface heights, and stack-on-top compatibility.
///
/// Rectangular footprints only in v0.2. Non-rectangular (L-shaped, etc.) are a future concern.
/// Per-prop-type granularity; no per-instance overrides in v0.2.
/// Rotation is not included here — that is a future PropRotationComponent concern.
/// </summary>
public struct BuildFootprintComponent : IComponent
{
    public BuildFootprintComponent() { }

    /// <summary>Footprint width in tiles (along the X axis). Minimum 1.</summary>
    public int WidthTiles { get; set; }

    /// <summary>Footprint depth in tiles (along the Z axis). Minimum 1.</summary>
    public int DepthTiles { get; set; }

    /// <summary>
    /// World units from the floor to the prop's bottom surface.
    /// 0 for floor-resting props. Positive for elevated props (e.g. monitor sitting on a desk).
    /// </summary>
    public float BottomHeight { get; set; }

    /// <summary>
    /// World units from BottomHeight to the prop's top surface.
    /// Other props resting on this prop sit at BottomHeight + TopHeight.
    /// </summary>
    public float TopHeight { get; set; }

    /// <summary>
    /// True when other props are allowed to rest on this prop's top surface.
    /// Desk: true. Chair: false. Monitor: false.
    /// </summary>
    public bool CanStackOnTop { get; set; }

    /// <summary>
    /// Optional category tag for richer matching in future stacking-rule packets.
    /// Examples: "Furniture", "DeskAccessory", "WallMounted". Empty by default.
    /// </summary>
    public string FootprintCategory { get; set; } = string.Empty;
}
