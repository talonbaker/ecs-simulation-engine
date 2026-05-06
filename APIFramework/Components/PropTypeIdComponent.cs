using APIFramework.Core;

namespace APIFramework.Components;

/// <summary>
/// Stable prop-type identifier attached to placeable prop entities.
/// Used by BuildFootprintInitializerSystem to look up the correct
/// BuildFootprintComponent from the catalog.
/// </summary>
public struct PropTypeIdComponent : IComponent
{
    /// <summary>
    /// Stable string id matching a catalog entry in prop-footprints.json.
    /// Examples: "desk", "chair", "monitor", "cube-wall".
    /// </summary>
    public string PropTypeId { get; init; }
}
