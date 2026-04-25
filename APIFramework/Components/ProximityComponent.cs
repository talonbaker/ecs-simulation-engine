namespace APIFramework.Components;

/// <summary>
/// NPC proximity awareness ranges in tile units.
/// Defaults match SimConfig Spatial.ProximityRangeDefaults.
/// Per-NPC overrides are applied at spawn time by the cast generator (Phase 1.8).
/// </summary>
public struct ProximityComponent
{
    /// <summary>~2 tiles: can talk, exchange feelings, pass an item.</summary>
    public int ConversationRangeTiles;

    /// <summary>~8 tiles: notice presence, notice mood, notice arrival/departure.</summary>
    public int AwarenessRangeTiles;

    /// <summary>~32 tiles: can see, cannot interact.</summary>
    public int SightRangeTiles;

    /// <summary>Returns a ProximityComponent initialised to the config default values.</summary>
    public static ProximityComponent Default => new()
    {
        ConversationRangeTiles = 2,
        AwarenessRangeTiles    = 8,
        SightRangeTiles        = 32,
    };
}
