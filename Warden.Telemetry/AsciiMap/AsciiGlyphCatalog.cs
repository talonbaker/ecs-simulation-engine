using System.Text;

namespace Warden.Telemetry.AsciiMap;

#if WARDEN

/// <summary>
/// Canonical reference for every Unicode glyph rendered by
/// <see cref="AsciiMapProjector"/>. Every distinct entity type owns one
/// dedicated codepoint — no reuse, no ambiguity — so the rendered map is
/// a true source-of-truth artifact for human and LLM readers alike.
///
/// Constants are grouped by category (walls, doors, floor shading,
/// furniture, hazards, NPC states). Use <see cref="RenderReference"/> to
/// obtain a printable symbol table that can be prepended to a Haiku
/// prompt as a one-shot decoder ring.
/// </summary>
public static class AsciiGlyphCatalog
{
    // ── Walls — Interior (single-line) ────────────────────────────────────────

    /// <summary>U+250C Interior wall upper-left corner.</summary>
    public const char WallInteriorTopLeft     = '┌';
    /// <summary>U+2510 Interior wall upper-right corner.</summary>
    public const char WallInteriorTopRight    = '┐';
    /// <summary>U+2514 Interior wall lower-left corner.</summary>
    public const char WallInteriorBottomLeft  = '└';
    /// <summary>U+2518 Interior wall lower-right corner.</summary>
    public const char WallInteriorBottomRight = '┘';
    /// <summary>U+2500 Interior horizontal wall.</summary>
    public const char WallInteriorHorizontal  = '─';
    /// <summary>U+2502 Interior vertical wall.</summary>
    public const char WallInteriorVertical    = '│';
    /// <summary>U+252C Interior T-junction (wall extends down).</summary>
    public const char WallInteriorTDown       = '┬';
    /// <summary>U+2534 Interior T-junction (wall extends up).</summary>
    public const char WallInteriorTUp         = '┴';
    /// <summary>U+251C Interior T-junction (wall extends right).</summary>
    public const char WallInteriorTRight      = '├';
    /// <summary>U+2524 Interior T-junction (wall extends left).</summary>
    public const char WallInteriorTLeft       = '┤';
    /// <summary>U+253C Interior wall four-way cross.</summary>
    public const char WallInteriorCross       = '┼';

    // ── Walls — Exterior (double-line) ────────────────────────────────────────

    /// <summary>U+2554 Exterior boundary upper-left corner.</summary>
    public const char WallExteriorTopLeft     = '╔';
    /// <summary>U+2557 Exterior boundary upper-right corner.</summary>
    public const char WallExteriorTopRight    = '╗';
    /// <summary>U+255A Exterior boundary lower-left corner.</summary>
    public const char WallExteriorBottomLeft  = '╚';
    /// <summary>U+255D Exterior boundary lower-right corner.</summary>
    public const char WallExteriorBottomRight = '╝';
    /// <summary>U+2550 Exterior horizontal boundary.</summary>
    public const char WallExteriorHorizontal  = '═';
    /// <summary>U+2551 Exterior vertical boundary.</summary>
    public const char WallExteriorVertical    = '║';

    // ── Doors ─────────────────────────────────────────────────────────────────

    /// <summary>U+00B7 Open door / aperture (mid-dot punched into a wall).</summary>
    public const char DoorOpen    = '·';
    /// <summary>U+25AA Closed door (small black square — distinctive vs. ASCII '+').</summary>
    public const char DoorClosed  = '▪';
    /// <summary>U+22A0 Locked door (squared times — denotes locked-in by system).</summary>
    public const char DoorLocked  = '⊠';
    /// <summary>U+22A1 Blocked / impassable door or pathfinding obstacle.</summary>
    public const char DoorBlocked = '⊡';

    // ── Floor shading (per RoomCategory) ──────────────────────────────────────

    /// <summary>U+0020 Office / generic walkable floor (empty space).</summary>
    public const char FloorOffice     = ' ';
    /// <summary>U+2591 Corridor / hallway (light shade).</summary>
    public const char FloorCorridor   = '░';
    /// <summary>U+2592 Breakroom / kitchen (medium shade).</summary>
    public const char FloorBreakroom  = '▒';
    /// <summary>U+2593 Bathroom (heavy shade — privacy).</summary>
    public const char FloorBathroom   = '▓';
    /// <summary>U+2237 Conference room (proportion / four-dot — seated meeting).</summary>
    public const char FloorConference = '∷';
    /// <summary>U+2235 Storage / supply closet (because-dots — archive feel).</summary>
    public const char FloorStorage    = '∵';
    /// <summary>U+2236 Reception / lobby (ratio dots — front-of-house).</summary>
    public const char FloorReception  = '∶';
    /// <summary>U+22EE Server room / IT closet (vertical ellipsis — stacked racks).</summary>
    public const char FloorServer     = '⋮';

    // ── Furniture (each gets a dedicated codepoint — no ASCII reuse) ──────────

    /// <summary>U+25AD Desk / workstation (rectangle outline — flat work surface).</summary>
    public const char FurnitureDesk            = '▭';
    /// <summary>U+2310 Chair (reversed-NOT — chair-back profile).</summary>
    public const char FurnitureChair           = '⌐';
    /// <summary>U+25A3 Microwave (square with inner square — oven window).</summary>
    public const char FurnitureMicrowave       = '▣';
    /// <summary>U+25A4 Fridge (square with horizontal lines — shelves).</summary>
    public const char FurnitureFridge          = '▤';
    /// <summary>U+2295 Toilet (circled plus — universal restroom shorthand).</summary>
    public const char FurnitureToilet          = '⊕';
    /// <summary>U+2297 Sink (circled times — drain symbol).</summary>
    public const char FurnitureSink            = '⊗';
    /// <summary>U+25AC Bed / couch (black rectangle — silhouette).</summary>
    public const char FurnitureBed             = '▬';
    /// <summary>U+25A8 Printer (square with diagonal lines — paper feed).</summary>
    public const char FurniturePrinter         = '▨';
    /// <summary>U+25C9 Coffee maker (bullseye — heat element).</summary>
    public const char FurnitureCoffeeMaker     = '◉';
    /// <summary>U+25B1 Sofa / couch (parallelogram — side profile).</summary>
    public const char FurnitureSofa            = '▱';
    /// <summary>U+229F Conference table (squared minus — table top).</summary>
    public const char FurnitureConferenceTable = '⊟';
    /// <summary>U+25A7 Whiteboard (square with upper-left diagonal — board).</summary>
    public const char FurnitureWhiteboard      = '▧';
    /// <summary>U+25A6 Filing cabinet (orthogonal crosshatch — drawers).</summary>
    public const char FurnitureFilingCabinet   = '▦';
    /// <summary>U+25C8 Water cooler (diamond with dot — vessel).</summary>
    public const char FurnitureWaterCooler     = '◈';
    /// <summary>U+25A9 Vending machine (diagonal crosshatch — grid of items).</summary>
    public const char FurnitureVendingMachine  = '▩';
    /// <summary>U+2261 Bookshelf (identical-to — stacked horizontal lines).</summary>
    public const char FurnitureBookshelf       = '≡';
    /// <summary>U+25A5 Copy machine (square with vertical lines — paper stack).</summary>
    public const char FurnitureCopyMachine     = '▥';
    /// <summary>U+22B9 Plant (hermitian conjugate — branch-like).</summary>
    public const char FurniturePlant           = '⊹';
    /// <summary>U+25EB Generic / other furniture (square with lower-right quadrant).</summary>
    public const char FurnitureOther           = '◫';

    // ── Hazards (distinct from furniture and NPCs) ────────────────────────────

    /// <summary>U+25B2 Fire (solid up-triangle — flame shape).</summary>
    public const char HazardFire        = '▲';
    /// <summary>U+2248 Water / spill (almost-equal — wave-like).</summary>
    public const char HazardWater       = '≈';
    /// <summary>U+2218 Blood / stain (ring operator — spot mark).</summary>
    public const char HazardStain       = '∘';
    /// <summary>U+2020 Corpse object (dagger — universally understood).</summary>
    public const char HazardCorpse      = '†';
    /// <summary>U+25C7 Broken glass (white diamond — shatter shape).</summary>
    public const char HazardBrokenGlass = '◇';
    /// <summary>U+25CC Oil slick (dotted circle — slippery surface).</summary>
    public const char HazardOilSlick    = '◌';
    /// <summary>U+223F Vomit (sine wave — nausea).</summary>
    public const char HazardVomit       = '∿';
    /// <summary>U+26AC Unknown / generic hazard (medium small white circle).</summary>
    public const char HazardUnknown     = '⚬';

    // ── NPC state overrides ───────────────────────────────────────────────────
    //
    // Normal NPCs render as the lowercase first letter of their name. The
    // following codepoints REPLACE that letter when the NPC enters a special
    // state, so the map alone (no legend lookup) reveals their condition.

    /// <summary>U+0040 Tile collision (two or more NPCs sharing a tile).</summary>
    public const char NpcCollision      = '@';
    /// <summary>U+0292 Sleeping NPC (ezh — resembles a sleeping 'z').</summary>
    public const char NpcSleeping       = 'ʒ';
    /// <summary>U+2298 Incapacitated / fainted NPC (circled division-slash — down/out).</summary>
    public const char NpcFainted        = '⊘';
    /// <summary>U+2020 Deceased NPC on tile (dagger — same as <see cref="HazardCorpse"/>).</summary>
    public const char NpcDeceased       = '†';
    /// <summary>U+0398 Choking NPC (theta — blocked-throat O with bar).</summary>
    public const char NpcChoking        = 'Θ';
    /// <summary>U+2039 In-conversation NPC (single left angle quote — speech indicator).</summary>
    public const char NpcInConversation = '‹';

    // ── Reference renderer ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the full glyph table as a printable, multi-line string. Useful
    /// as a one-shot decoder ring for Haiku prompts: paste this once at the
    /// top of a slab-3 cached header and every downstream render becomes
    /// self-explanatory without legend pollution.
    /// </summary>
    public static string RenderReference()
    {
        var sb = new StringBuilder();
        sb.Append("ASCII MAP — GLYPH REFERENCE (WP-3.0.W)\n");
        sb.Append('\n');

        sb.Append("WALLS\n");
        sb.Append($"  {WallInteriorTopLeft}{WallInteriorHorizontal}{WallInteriorTopRight}  Interior single-line corners + edges\n");
        sb.Append($"  {WallExteriorTopLeft}{WallExteriorHorizontal}{WallExteriorTopRight}  Exterior double-line boundary\n");
        sb.Append('\n');

        sb.Append("DOORS\n");
        sb.Append($"  {DoorOpen}    Open door / aperture\n");
        sb.Append($"  {DoorClosed}    Closed door\n");
        sb.Append($"  {DoorLocked}    Locked door (locked-in)\n");
        sb.Append($"  {DoorBlocked}    Blocked / pathfinding obstacle\n");
        sb.Append('\n');

        sb.Append("FLOOR SHADING (RoomCategory)\n");
        sb.Append($"  '{FloorOffice}'  Office / generic walkable\n");
        sb.Append($"  {FloorCorridor}    Corridor / hallway\n");
        sb.Append($"  {FloorBreakroom}    Breakroom / kitchen\n");
        sb.Append($"  {FloorBathroom}    Bathroom\n");
        sb.Append($"  {FloorConference}    Conference room\n");
        sb.Append($"  {FloorStorage}    Storage / supply closet\n");
        sb.Append($"  {FloorReception}    Reception / lobby\n");
        sb.Append($"  {FloorServer}    Server room / IT closet\n");
        sb.Append('\n');

        sb.Append("FURNITURE\n");
        sb.Append($"  {FurnitureDesk}    Desk / workstation\n");
        sb.Append($"  {FurnitureChair}    Chair\n");
        sb.Append($"  {FurnitureMicrowave}    Microwave\n");
        sb.Append($"  {FurnitureFridge}    Fridge\n");
        sb.Append($"  {FurnitureToilet}    Toilet\n");
        sb.Append($"  {FurnitureSink}    Sink\n");
        sb.Append($"  {FurnitureBed}    Bed\n");
        sb.Append($"  {FurniturePrinter}    Printer\n");
        sb.Append($"  {FurnitureCoffeeMaker}    Coffee maker\n");
        sb.Append($"  {FurnitureSofa}    Sofa / couch\n");
        sb.Append($"  {FurnitureConferenceTable}    Conference table\n");
        sb.Append($"  {FurnitureWhiteboard}    Whiteboard\n");
        sb.Append($"  {FurnitureFilingCabinet}    Filing cabinet\n");
        sb.Append($"  {FurnitureWaterCooler}    Water cooler\n");
        sb.Append($"  {FurnitureVendingMachine}    Vending machine\n");
        sb.Append($"  {FurnitureBookshelf}    Bookshelf\n");
        sb.Append($"  {FurnitureCopyMachine}    Copy machine\n");
        sb.Append($"  {FurniturePlant}    Plant\n");
        sb.Append($"  {FurnitureOther}    Generic / other\n");
        sb.Append('\n');

        sb.Append("HAZARDS\n");
        sb.Append($"  {HazardFire}    Fire\n");
        sb.Append($"  {HazardWater}    Water / spill\n");
        sb.Append($"  {HazardStain}    Blood / stain\n");
        sb.Append($"  {HazardCorpse}    Corpse object\n");
        sb.Append($"  {HazardBrokenGlass}    Broken glass\n");
        sb.Append($"  {HazardOilSlick}    Oil slick\n");
        sb.Append($"  {HazardVomit}    Vomit\n");
        sb.Append($"  {HazardUnknown}    Unknown / generic hazard\n");
        sb.Append('\n');

        sb.Append("NPCS\n");
        sb.Append("  a-z  Lowercase first letter of name (default)\n");
        sb.Append($"  {NpcCollision}    Tile collision (2+ NPCs same tile)\n");
        sb.Append($"  {NpcSleeping}    Sleeping NPC\n");
        sb.Append($"  {NpcFainted}    Incapacitated / fainted NPC\n");
        sb.Append($"  {NpcDeceased}    Deceased NPC on tile\n");
        sb.Append($"  {NpcChoking}    Choking NPC\n");
        sb.Append($"  {NpcInConversation}    NPC in conversation\n");

        return sb.ToString();
    }
}

#endif
