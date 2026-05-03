namespace APIFramework.Components;

/// <summary>
/// Soft personal-space bubble for NPC-to-NPC repulsion.
/// Attached at spawn by <see cref="Systems.Spatial.SpatialBehaviorInitializerSystem"/>;
/// per-archetype multipliers applied from archetype-personal-space.json.
/// </summary>
public struct PersonalSpaceComponent
{
    /// <summary>Radius in world units (metres). Typical range 0.4 – 0.9.</summary>
    public float RadiusMeters;

    /// <summary>
    /// Fraction of the per-tick overlap to apply as a nudge. Range 0..1.
    /// Lower values produce smoother, slower separation; 1.0 snaps apart in one tick.
    /// </summary>
    public float RepulsionStrength;
}
