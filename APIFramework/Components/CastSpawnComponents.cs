namespace APIFramework.Components;

/// <summary>Pixel-art silhouette dimensions sampled at spawn time for per-NPC visual identity.</summary>
public struct SilhouetteComponent
{
    /// <summary>Vertical bucket: short / average / tall.</summary>
    public string Height         { get; init; }   // short / average / tall
    /// <summary>Body-mass bucket: slight / average / stocky.</summary>
    public string Build          { get; init; }   // slight / average / stocky
    /// <summary>Hair description: bald / short / medium / long / distinctive.</summary>
    public string Hair           { get; init; }   // bald / short / medium / long / distinctive
    /// <summary>Headwear description: none / hat / cap / glasses / glasses+cap.</summary>
    public string Headwear       { get; init; }   // none / hat / cap / glasses / glasses+cap
    /// <summary>Single dominant clothing colour for the silhouette.</summary>
    public string DominantColor  { get; init; }   // single dominant clothing colour
    /// <summary>Distinctive carried item (clipboard, coffee-mug, lanyard, etc.).</summary>
    public string DistinctiveItem{ get; init; }   // clipboard / coffee-mug / lanyard / etc.
}

/// <summary>Records the archetype the NPC was generated from. Used by the relationship seeder.</summary>
public struct NpcArchetypeComponent
{
    /// <summary>Stable archetype id from the cast catalog.</summary>
    public string ArchetypeId { get; init; }
}

/// <summary>The one unusual personal thing about this NPC (deal catalog).</summary>
public struct NpcDealComponent
{
    /// <summary>Stable deal id (or short label) describing the NPC's defining quirk.</summary>
    public string Deal { get; init; }
}
