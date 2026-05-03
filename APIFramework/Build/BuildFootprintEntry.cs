namespace APIFramework.Build;

/// <summary>
/// Catalog data record for a single prop's footprint definition.
/// Returned by <see cref="BuildFootprintCatalog.GetByPropType"/>.
/// This is not an ECS component — it is the raw data the catalog stores per prop-type id.
/// <see cref="APIFramework.Systems.Build.BuildFootprintInitializerSystem"/> copies these
/// values into a <see cref="APIFramework.Components.BuildFootprintComponent"/> struct
/// when attaching footprints to live entities.
/// </summary>
public sealed class BuildFootprintEntry
{
    public string PropTypeId        { get; init; } = string.Empty;
    public int    WidthTiles        { get; init; }
    public int    DepthTiles        { get; init; }
    public float  BottomHeight      { get; init; }
    public float  TopHeight         { get; init; }
    public bool   CanStackOnTop     { get; init; }
    public string FootprintCategory { get; init; } = string.Empty;
}
