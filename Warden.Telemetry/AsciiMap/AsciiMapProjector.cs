using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Warden.Contracts.Telemetry;

namespace Warden.Telemetry.AsciiMap;

#if WARDEN

/// <summary>
/// Renders a <see cref="WorldStateDto"/> as a Unicode box-drawing floor plan.
///
/// Every distinct entity type owns one dedicated codepoint (no reuse) so the
/// rendered map is a true source-of-truth artifact for human and LLM
/// readers alike. The complete glyph table lives in
/// <see cref="AsciiGlyphCatalog"/>; this class consumes those constants
/// rather than inlining literals.
///
/// Z-ORDER (highest priority rendered last, wins):
///   floor shading → inner walls → outer boundary → open doors → closed doors →
///   blocked / locked overlays → furniture → hazards → NPCs.
///
/// NPC state overrides (computed before the name-letter is assigned):
///   deceased → fainted → choking → sleeping → in-conversation → letter.
///
/// Tile-collision marker (multiple NPCs on the same tile) is
/// <see cref="AsciiGlyphCatalog.NpcCollision"/> ('@') — distinct from every
/// hazard / furniture / wall codepoint.
/// </summary>
public static class AsciiMapProjector
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Renders the world state as a Unicode box-drawing floor plan.
    /// Pure function — deterministic, no I/O. Same <paramref name="state"/> +
    /// <paramref name="options"/> always produce a byte-identical string.
    /// </summary>
    public static string Render(WorldStateDto state, AsciiMapOptions options = default)
    {
        // Filter spatial data to the requested floor
        var rooms = (state.Rooms ?? Array.Empty<RoomDto>())
            .Where(r => (int)r.Floor == options.FloorIndex)
            .ToList();

        var roomIds = new HashSet<string>(rooms.Select(r => r.Id));

        var apertures = (state.LightApertures ?? Array.Empty<LightApertureDto>())
            .Where(a => roomIds.Contains(a.RoomId) && a.Facing != ApertureFacing.Ceiling)
            .ToList();

        var worldObjs = state.WorldObjects ?? new List<WorldObjectDto>();
        var entities  = state.Entities     ?? new List<EntityStateDto>();

        // Cross-reference NpcSaveStates by entity id when present (life-state
        // detail isn't on EntityStateDto in v0.5.x; the save-state list carries
        // it for save/load round-trips and we opportunistically read it here).
        var npcSaveById = (state.NpcSaveStates ?? new List<NpcSaveDto>())
            .GroupBy(s => s.Id)
            .ToDictionary(g => g.Key, g => g.First());

        // ── Grid sizing ───────────────────────────────────────────────────────

        int minX, minY, maxX, maxY;
        if (rooms.Count == 0)
        {
            minX = 0; minY = 0; maxX = 1; maxY = 0;
        }
        else
        {
            minX = rooms.Min(r => r.BoundsRect.X);
            minY = rooms.Min(r => r.BoundsRect.Y);
            maxX = rooms.Max(r => r.BoundsRect.X + r.BoundsRect.Width  - 1);
            maxY = rooms.Max(r => r.BoundsRect.Y + r.BoundsRect.Height - 1);
        }

        int gridCols = maxX - minX + 3;
        int gridRows = maxY - minY + 3;

        int ToGridCol(int wx) => wx - minX + 1;
        int ToGridRow(int wy) => wy - minY + 1;
        bool InnerBounds(int r, int c) => r >= 1 && r <= gridRows - 2 && c >= 1 && c <= gridCols - 2;

        var grid = new char[gridRows, gridCols];
        for (int r = 0; r < gridRows; r++)
            for (int c = 0; c < gridCols; c++)
                grid[r, c] = AsciiGlyphCatalog.FloorOffice;

        // Track which catalog glyphs appear on the rendered map so the
        // SYMBOLS legend lists only what is actually visible.
        var activeGlyphs = new HashSet<char>();

        // ── Layer 1: Floor shading ────────────────────────────────────────────

        foreach (var room in rooms)
        {
            char shade = FloorShade(room.Category);
            if (shade == AsciiGlyphCatalog.FloorOffice) continue;
            var br = room.BoundsRect;
            for (int wy = br.Y + 1; wy <= br.Y + br.Height - 2; wy++)
                for (int wx = br.X + 1; wx <= br.X + br.Width - 2; wx++)
                {
                    grid[ToGridRow(wy), ToGridCol(wx)] = shade;
                }
            activeGlyphs.Add(shade);
        }

        // ── Layer 2: Inner walls ──────────────────────────────────────────────

        var wallDirs = new Dictionary<(int row, int col), (bool N, bool S, bool E, bool W)>();

        foreach (var room in rooms)
        {
            var br = room.BoundsRect;
            int rX = br.X, rY = br.Y, rMaxX = rX + br.Width - 1, rMaxY = rY + br.Height - 1;

            for (int wy = rY; wy <= rMaxY; wy++)
            {
                for (int wx = rX; wx <= rMaxX; wx++)
                {
                    bool isTop   = wy == rY;
                    bool isBot   = wy == rMaxY;
                    bool isLeft  = wx == rX;
                    bool isRight = wx == rMaxX;

                    if (!isTop && !isBot && !isLeft && !isRight) continue; // interior tile

                    bool n = false, s = false, e = false, w = false;

                    if (isTop || isBot)
                    {
                        if (wx < rMaxX) e = true;
                        if (wx > rX)    w = true;
                    }
                    if (isLeft || isRight)
                    {
                        if (wy < rMaxY) s = true;
                        if (wy > rY)    n = true;
                    }

                    var key = (ToGridRow(wy), ToGridCol(wx));
                    wallDirs[key] = wallDirs.TryGetValue(key, out var ex)
                        ? (ex.N || n, ex.S || s, ex.E || e, ex.W || w)
                        : (n, s, e, w);
                }
            }
        }

        foreach (var (key, dirs) in wallDirs)
        {
            char wc = InnerWallChar(dirs.N, dirs.S, dirs.E, dirs.W);
            grid[key.row, key.col] = wc;
            activeGlyphs.Add(wc);
        }

        // ── Layer 3: Outer double-line boundary ───────────────────────────────

        int lastRow = gridRows - 1, lastCol = gridCols - 1;
        for (int c = 1; c < lastCol; c++) { grid[0, c] = AsciiGlyphCatalog.WallExteriorHorizontal; grid[lastRow, c] = AsciiGlyphCatalog.WallExteriorHorizontal; }
        for (int r = 1; r < lastRow; r++) { grid[r, 0] = AsciiGlyphCatalog.WallExteriorVertical;   grid[r, lastCol] = AsciiGlyphCatalog.WallExteriorVertical; }
        grid[0,       0]       = AsciiGlyphCatalog.WallExteriorTopLeft;
        grid[0,       lastCol] = AsciiGlyphCatalog.WallExteriorTopRight;
        grid[lastRow, 0]       = AsciiGlyphCatalog.WallExteriorBottomLeft;
        grid[lastRow, lastCol] = AsciiGlyphCatalog.WallExteriorBottomRight;
        activeGlyphs.Add(AsciiGlyphCatalog.WallExteriorTopLeft);

        // ── Layer 4: Open doors (LightApertures) ─────────────────────────────

        foreach (var ap in apertures)
        {
            int gr = ToGridRow(ap.Position.Y);
            int gc = ToGridCol(ap.Position.X);
            if (InnerBounds(gr, gc))
            {
                grid[gr, gc] = AsciiGlyphCatalog.DoorOpen;
                activeGlyphs.Add(AsciiGlyphCatalog.DoorOpen);
            }
        }

        // ── Layer 5: Closed / locked / blocked doors (WorldObjects) ───────────

        foreach (var obj in worldObjs)
        {
            if (!TryDoorGlyph(obj, out char dg)) continue;
            int gr = ToGridRow((int)Math.Floor(obj.Y));
            int gc = ToGridCol((int)Math.Floor(obj.X));
            if (InnerBounds(gr, gc))
            {
                grid[gr, gc] = dg;
                activeGlyphs.Add(dg);
            }
        }

        // ── Layer 6: Furniture ────────────────────────────────────────────────

        var furnitureLegend = new List<(char glyph, string name, int tx, int ty)>();

        if (options.ShowFurniture)
        {
            foreach (var obj in worldObjs.OrderBy(o => o.Id))
            {
                if (IsHazard(obj, out _) || TryDoorGlyph(obj, out _)) continue;
                char g = FurnitureGlyph(obj);
                int tx = (int)Math.Floor(obj.X), ty = (int)Math.Floor(obj.Y);
                int gr = ToGridRow(ty), gc = ToGridCol(tx);
                if (InnerBounds(gr, gc))
                {
                    grid[gr, gc] = g;
                    activeGlyphs.Add(g);
                }
                furnitureLegend.Add((g, obj.Name, tx, ty));
            }
        }

        // ── Layer 7: Hazards ──────────────────────────────────────────────────

        var hazardLegend = new List<(char glyph, string name, int tx, int ty)>();

        if (options.ShowHazards)
        {
            foreach (var obj in worldObjs.OrderBy(o => o.Id))
            {
                if (!IsHazard(obj, out char hg)) continue;
                int tx = (int)Math.Floor(obj.X), ty = (int)Math.Floor(obj.Y);
                int gr = ToGridRow(ty), gc = ToGridCol(tx);
                if (InnerBounds(gr, gc))
                {
                    grid[gr, gc] = hg;
                    activeGlyphs.Add(hg);
                }
                hazardLegend.Add((hg, obj.Name, tx, ty));
            }
        }

        // ── Layer 8: NPCs ─────────────────────────────────────────────────────

        var npcLegend = new List<(char glyph, string name, int tx, int ty, DominantDrive drive)>();

        if (options.ShowNpcs)
        {
            var tileNpcs = new Dictionary<(int tx, int ty), List<EntityStateDto>>();
            foreach (var e in entities.Where(e => e.Position.HasPosition))
            {
                int tx = (int)Math.Floor(e.Position.X);
                int ty = (int)Math.Floor(e.Position.Y);
                var k = (tx, ty);
                if (!tileNpcs.ContainsKey(k)) tileNpcs[k] = new List<EntityStateDto>();
                tileNpcs[k].Add(e);
            }

            foreach (var (tile, npcs) in tileNpcs)
            {
                int gr = ToGridRow(tile.ty), gc = ToGridCol(tile.tx);
                if (npcs.Count == 1)
                {
                    var e = npcs[0];
                    char g = NpcGlyph(e, npcSaveById);
                    if (InnerBounds(gr, gc))
                    {
                        grid[gr, gc] = g;
                        activeGlyphs.Add(g);
                    }
                    npcLegend.Add((g, e.Name, tile.tx, tile.ty, e.Drives.Dominant));
                }
                else
                {
                    if (InnerBounds(gr, gc))
                    {
                        grid[gr, gc] = AsciiGlyphCatalog.NpcCollision;
                        activeGlyphs.Add(AsciiGlyphCatalog.NpcCollision);
                    }
                    foreach (var e in npcs.OrderBy(e => e.Name))
                        npcLegend.Add((AsciiGlyphCatalog.NpcCollision, e.Name, tile.tx, tile.ty, e.Drives.Dominant));
                }
            }
        }

        // ── Build output ──────────────────────────────────────────────────────

        var sb = new StringBuilder();

        // Header
        var floorName = ((BuildingFloor)options.FloorIndex).ToString();
        var timeStr   = state.Clock?.GameTimeDisplay ?? "00:00";
        sb.Append($"WORLD MAP — Tick {state.Tick} | Floor {options.FloorIndex} ({floorName}) | Time {timeStr}\n");
        sb.Append('\n');

        // Grid
        for (int r = 0; r < gridRows; r++)
        {
            for (int c = 0; c < gridCols; c++)
                sb.Append(grid[r, c]);
            sb.Append('\n');
        }

        // Legend
        if (options.IncludeLegend)
        {
            sb.Append('\n');
            sb.Append("LEGEND\n");

            foreach (var (g, name, tx, ty, drive) in npcLegend.OrderBy(x => x.name))
                sb.Append($"  {g} — {name} ({tx}, {ty}) — {DriveLabel(drive)}\n");

            foreach (var (g, name, tx, ty) in furnitureLegend)
                sb.Append($"  {g} — {name} ({tx}, {ty})\n");

            foreach (var (g, name, tx, ty) in hazardLegend)
                sb.Append($"  {g} — {name} ({tx}, {ty})\n");

            // SYMBOLS — list every catalog glyph that appears on the map, with
            // its meaning. This is the readable source-of-truth block the
            // packet calls for: the map alone is enough, no external lookup.
            sb.Append('\n');
            sb.Append("SYMBOLS\n");
            foreach (var line in DescribeActiveGlyphs(activeGlyphs))
                sb.Append($"  {line}\n");
        }

        return sb.ToString();
    }

    // ── Floor shading ─────────────────────────────────────────────────────────

    private static char FloorShade(RoomCategory cat) => cat switch
    {
        RoomCategory.Hallway or RoomCategory.Stairwell or RoomCategory.Elevator => AsciiGlyphCatalog.FloorCorridor,
        RoomCategory.Breakroom      => AsciiGlyphCatalog.FloorBreakroom,
        RoomCategory.Bathroom       => AsciiGlyphCatalog.FloorBathroom,
        RoomCategory.ConferenceRoom => AsciiGlyphCatalog.FloorConference,
        RoomCategory.SupplyCloset   => AsciiGlyphCatalog.FloorStorage,
        RoomCategory.Lobby          => AsciiGlyphCatalog.FloorReception,
        RoomCategory.ItCloset       => AsciiGlyphCatalog.FloorServer,
        _                           => AsciiGlyphCatalog.FloorOffice,
    };

    // ── Wall character from direction flags ───────────────────────────────────

    private static char InnerWallChar(bool n, bool s, bool e, bool w) => (n, s, e, w) switch
    {
        (true,  true,  true,  true)  => AsciiGlyphCatalog.WallInteriorCross,
        (true,  true,  true,  false) => AsciiGlyphCatalog.WallInteriorTRight,
        (true,  true,  false, true)  => AsciiGlyphCatalog.WallInteriorTLeft,
        (false, true,  true,  true)  => AsciiGlyphCatalog.WallInteriorTDown,
        (true,  false, true,  true)  => AsciiGlyphCatalog.WallInteriorTUp,
        (true,  true,  false, false) => AsciiGlyphCatalog.WallInteriorVertical,
        (false, false, true,  true)  => AsciiGlyphCatalog.WallInteriorHorizontal,
        (false, true,  true,  false) => AsciiGlyphCatalog.WallInteriorTopLeft,
        (false, true,  false, true)  => AsciiGlyphCatalog.WallInteriorTopRight,
        (true,  false, true,  false) => AsciiGlyphCatalog.WallInteriorBottomLeft,
        (true,  false, false, true)  => AsciiGlyphCatalog.WallInteriorBottomRight,
        (true,  false, false, false) or (false, true,  false, false) => AsciiGlyphCatalog.WallInteriorVertical,
        (false, false, true,  false) or (false, false, false, true)  => AsciiGlyphCatalog.WallInteriorHorizontal,
        _ => AsciiGlyphCatalog.FloorOffice,
    };

    // ── Object classification ─────────────────────────────────────────────────

    private static bool IsHazard(WorldObjectDto obj, out char glyph)
    {
        if (obj.Kind != WorldObjectKind.Other) { glyph = AsciiGlyphCatalog.FloorOffice; return false; }
        var n = obj.Name.ToLowerInvariant();

        // Order matters: more specific keywords first.
        if (n.Contains("fire"))                                { glyph = AsciiGlyphCatalog.HazardFire;        return true; }
        if (n.Contains("glass") || n.Contains("shatter"))      { glyph = AsciiGlyphCatalog.HazardBrokenGlass; return true; }
        if (n.Contains("oil")   || n.Contains("slick"))        { glyph = AsciiGlyphCatalog.HazardOilSlick;    return true; }
        if (n.Contains("vomit") || n.Contains("puke"))         { glyph = AsciiGlyphCatalog.HazardVomit;       return true; }
        if (n.Contains("blood") || n.Contains("stain"))        { glyph = AsciiGlyphCatalog.HazardStain;       return true; }
        if (n.Contains("water") || n.Contains("spill"))        { glyph = AsciiGlyphCatalog.HazardWater;       return true; }
        if (n.Contains("corpse") || n.Contains("body"))        { glyph = AsciiGlyphCatalog.HazardCorpse;      return true; }
        if (n.Contains("hazard"))                              { glyph = AsciiGlyphCatalog.HazardUnknown;     return true; }

        glyph = AsciiGlyphCatalog.FloorOffice;
        return false;
    }

    /// <summary>
    /// Resolves a door-class glyph for the given world-object. Returns false
    /// when the object is not a door (open apertures are handled separately
    /// via <see cref="LightApertureDto"/>).
    /// </summary>
    private static bool TryDoorGlyph(WorldObjectDto obj, out char glyph)
    {
        if (obj.Kind != WorldObjectKind.Other)
        {
            glyph = AsciiGlyphCatalog.FloorOffice;
            return false;
        }
        var n = obj.Name.ToLowerInvariant();
        // Hazard-door names should not be treated as doors (e.g., "hazard").
        if (n.Contains("hazard"))
        {
            glyph = AsciiGlyphCatalog.FloorOffice;
            return false;
        }

        if (n.Contains("locked") && n.Contains("door"))   { glyph = AsciiGlyphCatalog.DoorLocked;  return true; }
        if (n.Contains("blocked") || n.Contains("obstacle")) { glyph = AsciiGlyphCatalog.DoorBlocked; return true; }
        if (n.Contains("door"))                            { glyph = AsciiGlyphCatalog.DoorClosed;  return true; }

        glyph = AsciiGlyphCatalog.FloorOffice;
        return false;
    }

    private static char FurnitureGlyph(WorldObjectDto obj) => obj.Kind switch
    {
        WorldObjectKind.Fridge => AsciiGlyphCatalog.FurnitureFridge,
        WorldObjectKind.Sink   => AsciiGlyphCatalog.FurnitureSink,
        WorldObjectKind.Toilet => AsciiGlyphCatalog.FurnitureToilet,
        WorldObjectKind.Bed    => AsciiGlyphCatalog.FurnitureBed,
        WorldObjectKind.Other  => NameFurnitureGlyph(obj.Name),
        _                      => AsciiGlyphCatalog.FurnitureOther,
    };

    private static char NameFurnitureGlyph(string name)
    {
        var n = name.ToLowerInvariant();
        // Order matters: more specific keywords first so e.g. "conference table"
        // does not collide with "table".
        if (n.Contains("conference") && n.Contains("table"))   return AsciiGlyphCatalog.FurnitureConferenceTable;
        if (n.Contains("microwave") || n.Contains("oven"))     return AsciiGlyphCatalog.FurnitureMicrowave;
        if (n.Contains("filing")    || n.Contains("cabinet"))  return AsciiGlyphCatalog.FurnitureFilingCabinet;
        if (n.Contains("vending"))                              return AsciiGlyphCatalog.FurnitureVendingMachine;
        if (n.Contains("water cooler") || n.Contains("cooler")) return AsciiGlyphCatalog.FurnitureWaterCooler;
        if (n.Contains("whiteboard") || n.Contains("white board")) return AsciiGlyphCatalog.FurnitureWhiteboard;
        if (n.Contains("bookshelf") || n.Contains("shelf"))    return AsciiGlyphCatalog.FurnitureBookshelf;
        if (n.Contains("copier")    || n.Contains("copy machine") || n.Contains("photocopier")) return AsciiGlyphCatalog.FurnitureCopyMachine;
        if (n.Contains("printer"))                              return AsciiGlyphCatalog.FurniturePrinter;
        if (n.Contains("coffee"))                               return AsciiGlyphCatalog.FurnitureCoffeeMaker;
        if (n.Contains("sofa")  || n.Contains("couch"))         return AsciiGlyphCatalog.FurnitureSofa;
        if (n.Contains("plant") || n.Contains("ficus"))         return AsciiGlyphCatalog.FurniturePlant;
        if (n.Contains("desk")  || n.Contains("workstation"))   return AsciiGlyphCatalog.FurnitureDesk;
        if (n.Contains("chair"))                                return AsciiGlyphCatalog.FurnitureChair;
        if (n.Contains("fridge"))                               return AsciiGlyphCatalog.FurnitureFridge;
        return AsciiGlyphCatalog.FurnitureOther;
    }

    /// <summary>
    /// Resolves the on-tile glyph for an NPC. State overrides (deceased,
    /// fainted, choking, sleeping) replace the default lowercase-name letter
    /// so the map alone reveals the NPC's condition.
    /// </summary>
    private static char NpcGlyph(EntityStateDto e, IReadOnlyDictionary<string, NpcSaveDto> saveById)
    {
        if (saveById.TryGetValue(e.Id, out var save))
        {
            if (save.LifeState?.State == SaveLifeState.Deceased) return AsciiGlyphCatalog.NpcDeceased;
            if (save.Fainting is not null)                       return AsciiGlyphCatalog.NpcFainted;
            if (save.Choking is not null)                        return AsciiGlyphCatalog.NpcChoking;
        }
        if (e.Physiology?.IsSleeping == true) return AsciiGlyphCatalog.NpcSleeping;

        return string.IsNullOrEmpty(e.Name) ? '?' : char.ToLowerInvariant(e.Name[0]);
    }

    private static string DriveLabel(DominantDrive d) => d switch
    {
        DominantDrive.Eat      => "Eating",
        DominantDrive.Drink    => "Drinking",
        DominantDrive.Sleep    => "Sleeping",
        DominantDrive.Defecate => "Defecating",
        DominantDrive.Pee      => "Pee",
        _                      => "Idle",
    };

    // ── Active-glyph descriptor (for the SYMBOLS legend section) ──────────────

    private static IEnumerable<string> DescribeActiveGlyphs(HashSet<char> active)
    {
        // Stable ordering: walk the catalog in declaration order so output
        // is deterministic across runs.
        (char glyph, string label)[] table =
        {
            (AsciiGlyphCatalog.WallExteriorTopLeft,     "Exterior boundary"),
            (AsciiGlyphCatalog.WallInteriorTopLeft,     "Interior wall corner"),
            (AsciiGlyphCatalog.WallInteriorHorizontal,  "Interior horizontal wall"),
            (AsciiGlyphCatalog.WallInteriorVertical,    "Interior vertical wall"),
            (AsciiGlyphCatalog.WallInteriorCross,       "Interior wall cross"),
            (AsciiGlyphCatalog.WallInteriorTDown,       "Interior T-junction (down)"),
            (AsciiGlyphCatalog.WallInteriorTUp,         "Interior T-junction (up)"),
            (AsciiGlyphCatalog.WallInteriorTRight,      "Interior T-junction (right)"),
            (AsciiGlyphCatalog.WallInteriorTLeft,       "Interior T-junction (left)"),
            (AsciiGlyphCatalog.WallInteriorTopRight,    "Interior wall corner"),
            (AsciiGlyphCatalog.WallInteriorBottomLeft,  "Interior wall corner"),
            (AsciiGlyphCatalog.WallInteriorBottomRight, "Interior wall corner"),

            (AsciiGlyphCatalog.DoorOpen,    "Open door"),
            (AsciiGlyphCatalog.DoorClosed,  "Closed door"),
            (AsciiGlyphCatalog.DoorLocked,  "Locked door"),
            (AsciiGlyphCatalog.DoorBlocked, "Blocked / obstacle"),

            (AsciiGlyphCatalog.FloorCorridor,   "Corridor / hallway"),
            (AsciiGlyphCatalog.FloorBreakroom,  "Breakroom / kitchen"),
            (AsciiGlyphCatalog.FloorBathroom,   "Bathroom"),
            (AsciiGlyphCatalog.FloorConference, "Conference room"),
            (AsciiGlyphCatalog.FloorStorage,    "Storage / closet"),
            (AsciiGlyphCatalog.FloorReception,  "Reception / lobby"),
            (AsciiGlyphCatalog.FloorServer,     "Server room"),

            (AsciiGlyphCatalog.FurnitureDesk,            "Desk"),
            (AsciiGlyphCatalog.FurnitureChair,           "Chair"),
            (AsciiGlyphCatalog.FurnitureMicrowave,       "Microwave"),
            (AsciiGlyphCatalog.FurnitureFridge,          "Fridge"),
            (AsciiGlyphCatalog.FurnitureToilet,          "Toilet"),
            (AsciiGlyphCatalog.FurnitureSink,            "Sink"),
            (AsciiGlyphCatalog.FurnitureBed,             "Bed"),
            (AsciiGlyphCatalog.FurniturePrinter,         "Printer"),
            (AsciiGlyphCatalog.FurnitureCoffeeMaker,     "Coffee maker"),
            (AsciiGlyphCatalog.FurnitureSofa,            "Sofa"),
            (AsciiGlyphCatalog.FurnitureConferenceTable, "Conference table"),
            (AsciiGlyphCatalog.FurnitureWhiteboard,      "Whiteboard"),
            (AsciiGlyphCatalog.FurnitureFilingCabinet,   "Filing cabinet"),
            (AsciiGlyphCatalog.FurnitureWaterCooler,     "Water cooler"),
            (AsciiGlyphCatalog.FurnitureVendingMachine,  "Vending machine"),
            (AsciiGlyphCatalog.FurnitureBookshelf,       "Bookshelf"),
            (AsciiGlyphCatalog.FurnitureCopyMachine,     "Copy machine"),
            (AsciiGlyphCatalog.FurniturePlant,           "Plant"),
            (AsciiGlyphCatalog.FurnitureOther,           "Generic furniture"),

            (AsciiGlyphCatalog.HazardFire,        "Fire"),
            (AsciiGlyphCatalog.HazardWater,       "Water / spill"),
            (AsciiGlyphCatalog.HazardStain,       "Blood / stain"),
            (AsciiGlyphCatalog.HazardCorpse,      "Corpse"),
            (AsciiGlyphCatalog.HazardBrokenGlass, "Broken glass"),
            (AsciiGlyphCatalog.HazardOilSlick,    "Oil slick"),
            (AsciiGlyphCatalog.HazardVomit,       "Vomit"),
            (AsciiGlyphCatalog.HazardUnknown,     "Unknown hazard"),

            (AsciiGlyphCatalog.NpcCollision,      "NPC tile collision (2+ NPCs)"),
            (AsciiGlyphCatalog.NpcSleeping,       "Sleeping NPC"),
            (AsciiGlyphCatalog.NpcFainted,        "Fainted NPC"),
            (AsciiGlyphCatalog.NpcChoking,        "Choking NPC"),
            (AsciiGlyphCatalog.NpcInConversation, "NPC in conversation"),
        };

        var seen = new HashSet<char>();
        foreach (var (glyph, label) in table)
        {
            if (!active.Contains(glyph)) continue;
            if (!seen.Add(glyph))        continue; // collapse duplicate keys (e.g., dagger reused for corpse)
            yield return $"{glyph}    {label}";
        }
    }
}

#endif
