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
/// Glyph contract:
///
/// WALLS — Interior single-line:
///   ┌ U+250C upper-left    ┐ U+2510 upper-right   └ U+2514 lower-left    ┘ U+2518 lower-right
///   ─ U+2500 horizontal    │ U+2502 vertical
///   ┬ U+252C T-down        ┴ U+2534 T-up          ├ U+251C T-right       ┤ U+2524 T-left
///   ┼ U+253C cross
///
/// WALLS — Exterior double-line boundary:
///   ╔ U+2554 upper-left    ╗ U+2557 upper-right   ╚ U+255A lower-left    ╝ U+255D lower-right
///   ═ U+2550 horizontal    ║ U+2551 vertical
///   ╦ U+2566 T-down        ╩ U+2569 T-up          ╠ U+2560 T-right       ╣ U+2563 T-left
///
/// DOORS:
///   · U+00B7 open (LightAperture at wall position)
///   + U+002B closed (WorldObject Kind=Other, Name contains "door")
///
/// FLOOR SHADING (RoomCategory):
///   ' '  Office/generic    ░ U+2591 Corridor/hallway    ▒ U+2592 Breakroom    ▓ U+2593 Bathroom
///
/// FURNITURE (WorldObjectDto → uppercase letter):
///   D Desk   C Chair   M Microwave   F Fridge   T Toilet   S Sink   B Bed   O Other
///   Kind-based: Fridge→F, Sink→S, Toilet→T, Bed→B.
///   Kind=Other: name-based match (microwave→M, desk/workstation→D, chair→C), fallback O.
///
/// NPCs (EntityStateDto → lowercase first letter of Name):
///   Tile collision (two NPCs on same tile) → * glyph; legend disambiguates both.
///   Name-letter collision (same first letter, different tiles) → both show their letter, both in legend.
///
/// HAZARDS (WorldObjectDto Kind=Other, name-keyword detected):
///   ! fire    ~ water/spill    * stain    x corpse    ? unknown/hazard
///
/// Z-ORDER (highest priority rendered last, wins):
///   floor shading → inner walls → outer boundary → open doors → closed doors →
///   furniture → hazards → NPCs
/// </summary>
public static class AsciiMapProjector
{
    // ── Interior (single-line) wall chars ─────────────────────────────────────
    private const char WI_TL = '┌', WI_TR = '┐', WI_BL = '└', WI_BR = '┘';
    private const char WI_H  = '─', WI_V  = '│';
    private const char WI_TD = '┬', WI_TU = '┴', WI_RI = '├', WI_LE = '┤', WI_X = '┼';

    // ── Exterior (double-line) boundary chars ─────────────────────────────────
    private const char WO_TL = '╔', WO_TR = '╗', WO_BL = '╚', WO_BR = '╝';
    private const char WO_H  = '═', WO_V  = '║';

    // ── Door chars ────────────────────────────────────────────────────────────
    private const char DoorOpen   = '·';
    private const char DoorClosed = '+';

    // ── Floor shading ─────────────────────────────────────────────────────────
    private const char ShadeCorr  = '░';
    private const char ShadeBreak = '▒';
    private const char ShadeBath  = '▓';

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Renders the world state as a Unicode box-drawing floor plan.
    /// See WP-3.0.W spec for the full glyph contract.
    /// Pure function — deterministic, no I/O.
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

        // ── Grid sizing ───────────────────────────────────────────────────────

        int minX, minY, maxX, maxY;
        if (rooms.Count == 0)
        {
            // Minimal 4 × 3 grid for an empty world
            minX = 0; minY = 0; maxX = 1; maxY = 0;
        }
        else
        {
            minX = rooms.Min(r => r.BoundsRect.X);
            minY = rooms.Min(r => r.BoundsRect.Y);
            maxX = rooms.Max(r => r.BoundsRect.X + r.BoundsRect.Width  - 1);
            maxY = rooms.Max(r => r.BoundsRect.Y + r.BoundsRect.Height - 1);
        }

        // Grid is world-bounds + 1-tile outer-boundary margin on every side
        int gridCols = maxX - minX + 3;
        int gridRows = maxY - minY + 3;

        int ToGridCol(int wx) => wx - minX + 1;
        int ToGridRow(int wy) => wy - minY + 1;
        bool InnerBounds(int r, int c) => r >= 1 && r <= gridRows - 2 && c >= 1 && c <= gridCols - 2;

        // ── Grid allocation ───────────────────────────────────────────────────

        var grid = new char[gridRows, gridCols];
        for (int r = 0; r < gridRows; r++)
            for (int c = 0; c < gridCols; c++)
                grid[r, c] = ' ';

        // ── Layer 1: Floor shading ────────────────────────────────────────────

        foreach (var room in rooms)
        {
            char shade = FloorShade(room.Category);
            if (shade == ' ') continue;
            var br = room.BoundsRect;
            for (int wy = br.Y + 1; wy <= br.Y + br.Height - 2; wy++)
                for (int wx = br.X + 1; wx <= br.X + br.Width - 2; wx++)
                    grid[ToGridRow(wy), ToGridCol(wx)] = shade;
        }

        // ── Layer 2: Inner walls ──────────────────────────────────────────────
        //
        // For each perimeter tile of each room, accumulate which cardinal directions
        // the wall continues into. The direction flags derive from which border(s)
        // the tile lies on within its room:
        //   top/bottom border  → horizontal reach (E if not rightmost, W if not leftmost)
        //   left/right border  → vertical reach   (S if not bottommost, N if not topmost)
        //
        // Multiple rooms sharing the same wall tile accumulate flags (OR logic), producing
        // correct T-junction and cross characters at shared walls.

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
            grid[key.row, key.col] = InnerWallChar(dirs.N, dirs.S, dirs.E, dirs.W);

        // ── Layer 3: Outer double-line boundary ───────────────────────────────

        int lastRow = gridRows - 1, lastCol = gridCols - 1;
        for (int c = 1; c < lastCol; c++) { grid[0, c] = WO_H; grid[lastRow, c] = WO_H; }
        for (int r = 1; r < lastRow; r++) { grid[r, 0] = WO_V; grid[r, lastCol] = WO_V; }
        grid[0,       0]       = WO_TL;
        grid[0,       lastCol] = WO_TR;
        grid[lastRow, 0]       = WO_BL;
        grid[lastRow, lastCol] = WO_BR;

        // ── Layer 4: Open doors (LightApertures) ─────────────────────────────

        foreach (var ap in apertures)
        {
            int gr = ToGridRow(ap.Position.Y);
            int gc = ToGridCol(ap.Position.X);
            if (InnerBounds(gr, gc)) grid[gr, gc] = DoorOpen;
        }

        // ── Layer 5: Closed doors (WorldObjects) ──────────────────────────────

        foreach (var obj in worldObjs)
        {
            if (!IsClosedDoor(obj)) continue;
            int gr = ToGridRow((int)Math.Floor(obj.Y));
            int gc = ToGridCol((int)Math.Floor(obj.X));
            if (InnerBounds(gr, gc)) grid[gr, gc] = DoorClosed;
        }

        // ── Layer 6: Furniture ────────────────────────────────────────────────

        var furnitureLegend = new List<(char glyph, string name, int tx, int ty)>();

        if (options.ShowFurniture)
        {
            foreach (var obj in worldObjs.OrderBy(o => o.Id))
            {
                if (IsHazard(obj, out _) || IsClosedDoor(obj)) continue;
                char g = FurnitureGlyph(obj);
                int tx = (int)Math.Floor(obj.X), ty = (int)Math.Floor(obj.Y);
                int gr = ToGridRow(ty), gc = ToGridCol(tx);
                if (InnerBounds(gr, gc)) grid[gr, gc] = g;
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
                if (InnerBounds(gr, gc)) grid[gr, gc] = hg;
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
                    char g = NpcGlyph(e.Name);
                    if (InnerBounds(gr, gc)) grid[gr, gc] = g;
                    npcLegend.Add((g, e.Name, tile.tx, tile.ty, e.Drives.Dominant));
                }
                else
                {
                    // Tile collision → * on the map; each NPC listed in legend with *
                    if (InnerBounds(gr, gc)) grid[gr, gc] = '*';
                    foreach (var e in npcs.OrderBy(e => e.Name))
                        npcLegend.Add(('*', e.Name, tile.tx, tile.ty, e.Drives.Dominant));
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

            // NPCs sorted by name
            foreach (var (g, name, tx, ty, drive) in npcLegend.OrderBy(x => x.name))
                sb.Append($"  {g} — {name} ({tx}, {ty}) — {DriveLabel(drive)}\n");

            // Furniture
            foreach (var (g, name, tx, ty) in furnitureLegend)
                sb.Append($"  {g} — {name} ({tx}, {ty})\n");

            // Hazards — also show hazards occluded by NPCs on same tile
            var npcTiles = new HashSet<(int, int)>(npcLegend.Select(n => (n.tx, n.ty)));
            foreach (var (g, name, tx, ty) in hazardLegend)
                sb.Append($"  {g} — {name} ({tx}, {ty})\n");
        }

        return sb.ToString();
    }

    // ── Floor shading ─────────────────────────────────────────────────────────

    private static char FloorShade(RoomCategory cat) => cat switch
    {
        RoomCategory.Hallway or RoomCategory.Stairwell or RoomCategory.Elevator => ShadeCorr,
        RoomCategory.Breakroom => ShadeBreak,
        RoomCategory.Bathroom  => ShadeBath,
        _                      => ' ',
    };

    // ── Wall character from direction flags ───────────────────────────────────

    private static char InnerWallChar(bool n, bool s, bool e, bool w) => (n, s, e, w) switch
    {
        (true,  true,  true,  true)  => WI_X,
        (true,  true,  true,  false) => WI_RI,
        (true,  true,  false, true)  => WI_LE,
        (false, true,  true,  true)  => WI_TD,
        (true,  false, true,  true)  => WI_TU,
        (true,  true,  false, false) => WI_V,
        (false, false, true,  true)  => WI_H,
        (false, true,  true,  false) => WI_TL,
        (false, true,  false, true)  => WI_TR,
        (true,  false, true,  false) => WI_BL,
        (true,  false, false, true)  => WI_BR,
        (true,  false, false, false) or (false, true,  false, false) => WI_V,
        (false, false, true,  false) or (false, false, false, true)  => WI_H,
        _ => ' ',
    };

    // ── Object classification ─────────────────────────────────────────────────

    private static bool IsHazard(WorldObjectDto obj, out char glyph)
    {
        if (obj.Kind != WorldObjectKind.Other) { glyph = ' '; return false; }
        var n = obj.Name.ToLowerInvariant();
        if (n.Contains("fire"))                         { glyph = '!'; return true; }
        if (n.Contains("stain"))                        { glyph = '*'; return true; }
        if (n.Contains("water") || n.Contains("spill")) { glyph = '~'; return true; }
        if (n.Contains("corpse") || n.Contains("body")) { glyph = 'x'; return true; }
        if (n.Contains("hazard"))                       { glyph = '?'; return true; }
        glyph = ' '; return false;
    }

    private static bool IsClosedDoor(WorldObjectDto obj)
        => obj.Kind == WorldObjectKind.Other
        && obj.Name.Contains("door", StringComparison.OrdinalIgnoreCase)
        && !obj.Name.Contains("hazard", StringComparison.OrdinalIgnoreCase);

    private static char FurnitureGlyph(WorldObjectDto obj) => obj.Kind switch
    {
        WorldObjectKind.Fridge => 'F',
        WorldObjectKind.Sink   => 'S',
        WorldObjectKind.Toilet => 'T',
        WorldObjectKind.Bed    => 'B',
        WorldObjectKind.Other  => NameFurnitureGlyph(obj.Name),
        _                      => 'O',
    };

    private static char NameFurnitureGlyph(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("microwave") || n.Contains("oven"))   return 'M';
        if (n.Contains("desk") || n.Contains("workstation")) return 'D';
        if (n.Contains("chair"))                              return 'C';
        if (n.Contains("fridge"))                             return 'F';
        return 'O';
    }

    private static char NpcGlyph(string name)
        => string.IsNullOrEmpty(name) ? '?' : char.ToLowerInvariant(name[0]);

    private static string DriveLabel(DominantDrive d) => d switch
    {
        DominantDrive.Eat      => "Eating",
        DominantDrive.Drink    => "Drinking",
        DominantDrive.Sleep    => "Sleeping",
        DominantDrive.Defecate => "Defecating",
        DominantDrive.Pee      => "Pee",
        _                      => "Idle",
    };
}

#endif
