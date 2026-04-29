using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// Era-appropriate color palette for all ECSUnity renderers.
///
/// PALETTE DESIGN (aesthetic-bible §color-palette)
/// ─────────────────────────────────────────────────
/// The aesthetic is "late-90s / early-2000s corporate office" — think muted neutrals,
/// warm fluorescent yellows, slightly faded pastels, nothing fresh or vibrant.
/// Colors are deliberately unsaturated; the office looks used, not designed.
///
/// Room colors are room-kind-driven:
///   CubicleGrid   → muted beige / stale-carpet tan
///   Bathroom       → pale tile blue-grey
///   Breakroom      → warm off-white cream
///   IT Closet      → cool concrete grey (server-room blue-grey)
///   Conference     → neutral corporate grey
///   Office         → warm cream
///   Hallway        → lighter grey (transition space)
///   Default        → mid-beige fallback
///
/// NPC dot colors are archetype-driven (cast-bible primary palette):
///   Donna          → dark plum
///   Greg           → pale yellow-green (sickly desk pallor)
///   Frank          → warm brown (coffee-and-denim)
///   Unknown / other→ neutral mid-grey
/// </summary>
public static class RenderColorPalette
{
    // ── Room colors ───────────────────────────────────────────────────────────

    public static readonly Color CubicleGrid    = new Color(0.84f, 0.79f, 0.68f);   // beige / stale carpet
    public static readonly Color Bathroom       = new Color(0.72f, 0.78f, 0.82f);   // pale tile blue-grey
    public static readonly Color Breakroom      = new Color(0.90f, 0.87f, 0.80f);   // warm cream off-white
    public static readonly Color ItCloset       = new Color(0.55f, 0.60f, 0.65f);   // cool server-room grey
    public static readonly Color ConferenceRoom = new Color(0.70f, 0.70f, 0.68f);   // neutral corporate grey
    public static readonly Color Office         = new Color(0.88f, 0.84f, 0.76f);   // warm cream
    public static readonly Color Hallway        = new Color(0.78f, 0.78f, 0.76f);   // lighter transition grey
    public static readonly Color SupplyCloset   = new Color(0.62f, 0.65f, 0.60f);   // greenish-grey utility
    public static readonly Color Stairwell      = new Color(0.60f, 0.60f, 0.60f);   // plain concrete grey
    public static readonly Color Elevator       = new Color(0.65f, 0.65f, 0.68f);   // slightly cooler grey
    public static readonly Color ParkingLot     = new Color(0.52f, 0.52f, 0.52f);   // dark asphalt grey
    public static readonly Color Outdoor        = new Color(0.55f, 0.68f, 0.50f);   // muted outdoor green
    public static readonly Color DefaultRoom    = new Color(0.78f, 0.74f, 0.66f);   // mid-beige fallback

    // ── NPC dot colors (archetype-driven) ────────────────────────────────────

    // Named NPCs from the cast-bible. Matched by IdentityComponent.Name prefix.
    public static readonly Color NpcDonna   = new Color(0.38f, 0.18f, 0.30f);   // dark plum
    public static readonly Color NpcGreg    = new Color(0.78f, 0.82f, 0.55f);   // pale yellow-green
    public static readonly Color NpcFrank   = new Color(0.52f, 0.35f, 0.22f);   // warm brown
    public static readonly Color NpcDefault = new Color(0.55f, 0.55f, 0.55f);   // neutral mid-grey

    // ── Floor plane ───────────────────────────────────────────────────────────

    public static readonly Color FloorPlane = new Color(0.20f, 0.20f, 0.22f);   // dark near-black

    // ── Lookup helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the era-appropriate color for a room based on its <see cref="RoomCategory"/>.
    /// </summary>
    public static Color ForRoom(RoomCategory category) => category switch
    {
        RoomCategory.CubicleGrid    => CubicleGrid,
        RoomCategory.Bathroom       => Bathroom,
        RoomCategory.Breakroom      => Breakroom,
        RoomCategory.ItCloset       => ItCloset,
        RoomCategory.ConferenceRoom => ConferenceRoom,
        RoomCategory.Office         => Office,
        RoomCategory.Hallway        => Hallway,
        RoomCategory.SupplyCloset   => SupplyCloset,
        RoomCategory.Stairwell      => Stairwell,
        RoomCategory.Elevator       => Elevator,
        RoomCategory.ParkingLot     => ParkingLot,
        RoomCategory.Outdoor        => Outdoor,
        _                           => DefaultRoom,
    };

    /// <summary>
    /// Returns the dot color for an NPC based on its display name.
    /// Matching is by prefix so "Donna Paulsen" matches "Donna".
    /// </summary>
    public static Color ForNpc(string name)
    {
        if (string.IsNullOrEmpty(name))   return NpcDefault;

        // Simple prefix matching against canonical first names.
        if (name.StartsWith("Donna", System.StringComparison.OrdinalIgnoreCase)) return NpcDonna;
        if (name.StartsWith("Greg",  System.StringComparison.OrdinalIgnoreCase)) return NpcGreg;
        if (name.StartsWith("Frank", System.StringComparison.OrdinalIgnoreCase)) return NpcFrank;

        return NpcDefault;
    }
}
