namespace APIFramework.Components;

/// <summary>Pixel-art silhouette dimensions sampled at spawn time for per-NPC visual identity.</summary>
public struct SilhouetteComponent
{
    public string Height         { get; init; }   // short / average / tall
    public string Build          { get; init; }   // slight / average / stocky
    public string Hair           { get; init; }   // bald / short / medium / long / distinctive
    public string Headwear       { get; init; }   // none / hat / cap / glasses / glasses+cap
    public string DominantColor  { get; init; }   // single dominant clothing colour
    public string DistinctiveItem{ get; init; }   // clipboard / coffee-mug / lanyard / etc.
}

/// <summary>Records the archetype the NPC was generated from. Used by the relationship seeder.</summary>
public struct NpcArchetypeComponent
{
    public string ArchetypeId { get; init; }
}

/// <summary>The one unusual personal thing about this NPC (deal catalog).</summary>
public struct NpcDealComponent
{
    public string Deal { get; init; }
}
