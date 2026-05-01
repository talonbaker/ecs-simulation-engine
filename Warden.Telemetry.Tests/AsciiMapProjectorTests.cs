using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Warden.Contracts.Telemetry;
using Warden.Telemetry.AsciiMap;
using Xunit;

namespace Warden.Telemetry.Tests;

/// <summary>
/// Acceptance tests for <see cref="AsciiMapProjector"/>:
///
/// AT-01 — Class exists with the public API; compiles with #if WARDEN guard.
/// AT-02 — Empty WorldStateDto → minimal ╔══╗/╚══╝ outline.
/// AT-03 — Single-room world → outer double-line + inner single-line wall outline.
/// AT-04 — Two-room world with open door → · glyph at door tile.
/// AT-05 — Closed-door variant → + glyph at door tile.
/// AT-06 — Room shading by category (Corridor→░, Breakroom→▒, Bathroom→▓).
/// AT-07 — All eight furniture glyphs (D C M F T S B O).
/// AT-08 — NPC rendering: Donna → d at her tile.
/// AT-09 — NPC tile collision → * glyph, both names in legend.
/// AT-10 — NPC name-letter collision: both render as d, both in legend.
/// AT-11 — Hazard rendering + Z-order (NPC beats hazard on same tile, legend lists both).
/// AT-12 — Z-order: NPC over furniture, both in legend.
/// AT-13 — IncludeLegend = false → no LEGEND section in output.
/// AT-14 — ShowHazards = false → hazards excluded from grid and legend.
/// AT-15 — Determinism: 100 calls produce byte-identical output.
/// AT-16 — Perf: 30-NPC, 8-room, 2-corridor world renders in &lt;50 ms mean.
/// </summary>
public class AsciiMapProjectorTests
{
    // ── Fixture helpers ───────────────────────────────────────────────────────

    private static WorldStateDto EmptyWorld(int tick = 0) =>
        new WorldStateDto
        {
            Tick  = tick,
            Clock = new ClockStateDto { GameTimeDisplay = "08:00" },
        };

    private static RoomDto MakeRoom(string id, int x, int y, int w, int h,
        RoomCategory cat = RoomCategory.Office,
        BuildingFloor floor = BuildingFloor.Basement) =>
        new RoomDto
        {
            Id         = id,
            Name       = id,
            Category   = cat,
            Floor      = floor,
            BoundsRect = new BoundsRectDto { X = x, Y = y, Width = w, Height = h },
            Illumination = new IlluminationDto { AmbientLevel = 100 },
        };

    private static EntityStateDto MakeNpc(string name, float x, float y,
        DominantDrive drive = DominantDrive.None) =>
        new EntityStateDto
        {
            Id      = name,
            ShortId = name,
            Name    = name,
            Species = SpeciesType.Human,
            Position = new PositionStateDto { X = x, Y = y, HasPosition = true },
            Drives   = new DrivesStateDto { Dominant = drive },
            Physiology = new PhysiologyStateDto(),
        };

    private static WorldObjectDto MakeObj(string id, string name, WorldObjectKind kind,
        float x, float y) =>
        new WorldObjectDto { Id = id, Name = name, Kind = kind, X = x, Y = y };

    private static LightApertureDto MakeAperture(string roomId, int x, int y,
        ApertureFacing facing = ApertureFacing.North) =>
        new LightApertureDto
        {
            Id       = $"ap-{roomId}-{x}-{y}",
            RoomId   = roomId,
            Position = new TilePointDto { X = x, Y = y },
            Facing   = facing,
            AreaSqTiles = 1,
        };

    // ── AT-01: Class exists and compiles ──────────────────────────────────────

    [Fact]
    public void AT01_ClassExistsAndCompiles()
    {
        // If this compiles, the #if WARDEN guard is active and the class exists.
        var result = AsciiMapProjector.Render(EmptyWorld());
        Assert.NotNull(result);
    }

    // ── AT-02: Empty world ────────────────────────────────────────────────────

    [Fact]
    public void AT02_EmptyWorld_MinimalOutline()
    {
        var result = AsciiMapProjector.Render(EmptyWorld(tick: 0),
            new AsciiMapOptions(FloorIndex: 0, IncludeLegend: true));

        // Header present
        Assert.Contains("WORLD MAP", result);
        Assert.Contains("Tick 0", result);

        // Minimal outer boundary characters present
        Assert.Contains("╔", result);
        Assert.Contains("╗", result);
        Assert.Contains("╚", result);
        Assert.Contains("╝", result);
        Assert.Contains("═", result);
        Assert.Contains("║", result);

        // No inner wall chars (no rooms)
        Assert.DoesNotContain("┌", result);
        Assert.DoesNotContain("┐", result);

        // Legend section present
        Assert.Contains("LEGEND", result);
    }

    // ── AT-03: Single-room world ──────────────────────────────────────────────

    [Fact]
    public void AT03_SingleRoom_OuterAndInnerWalls()
    {
        var state = EmptyWorld() with
        {
            Rooms = new List<RoomDto> { MakeRoom("r1", 2, 2, 5, 4) },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions(FloorIndex: 0));

        // Outer double-line boundary
        Assert.Contains("╔", result);
        Assert.Contains("╗", result);
        Assert.Contains("╚", result);
        Assert.Contains("╝", result);

        // Inner single-line corners
        Assert.Contains("┌", result);
        Assert.Contains("┐", result);
        Assert.Contains("└", result);
        Assert.Contains("┘", result);

        // Inner horizontal and vertical walls
        Assert.Contains("─", result);
        Assert.Contains("│", result);

        // No door glyphs
        Assert.DoesNotContain("·", result);
        Assert.DoesNotContain("+", result);
    }

    // ── AT-04: Two-room world with open door ──────────────────────────────────

    [Fact]
    public void AT04_TwoRooms_OpenDoor_MidDotGlyph()
    {
        // Room A: x=0..4, y=0..4. Room B: x=4..8, y=0..4. Share column x=4.
        // Aperture on shared wall at (4, 2).
        var state = EmptyWorld() with
        {
            Rooms = new List<RoomDto>
            {
                MakeRoom("ra", 0, 0, 5, 5),
                MakeRoom("rb", 4, 0, 5, 5),
            },
            LightApertures = new List<LightApertureDto>
            {
                MakeAperture("ra", 4, 2, ApertureFacing.East),
            },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions(FloorIndex: 0));

        // Open-door glyph must be present
        Assert.Contains("·", result);
        // No closed-door glyph
        Assert.DoesNotContain("+", result);
    }

    // ── AT-05: Two-room world with closed door ────────────────────────────────

    [Fact]
    public void AT05_TwoRooms_ClosedDoor_PlusGlyph()
    {
        var state = EmptyWorld() with
        {
            Rooms = new List<RoomDto>
            {
                MakeRoom("ra", 0, 0, 5, 5),
                MakeRoom("rb", 4, 0, 5, 5),
            },
            WorldObjects = new List<WorldObjectDto>
            {
                // Closed door on shared wall at (4, 2)
                MakeObj("door1", "door-closed", WorldObjectKind.Other, 4f, 2f),
            },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions(FloorIndex: 0));

        Assert.Contains("+", result);
        Assert.DoesNotContain("·", result);
    }

    // ── AT-06: Room shading by category ──────────────────────────────────────

    [Fact]
    public void AT06_RoomShading_CorrectGlyphs()
    {
        // Three rooms side by side on the same floor, each 4×4.
        var state = EmptyWorld() with
        {
            Rooms = new List<RoomDto>
            {
                MakeRoom("corridor", 0, 0, 4, 4, RoomCategory.Hallway),
                MakeRoom("kitchen",  4, 0, 4, 4, RoomCategory.Breakroom),
                MakeRoom("bathroom", 8, 0, 4, 4, RoomCategory.Bathroom),
            },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions(FloorIndex: 0));

        Assert.Contains("░", result); // corridor
        Assert.Contains("▒", result); // breakroom
        Assert.Contains("▓", result); // bathroom
    }

    // ── AT-07: All eight furniture glyphs ─────────────────────────────────────

    [Fact]
    public void AT07_FurnitureGlyphs_AllEight()
    {
        // Room 10×10, furniture placed on interior tiles
        var state = EmptyWorld() with
        {
            Rooms = new List<RoomDto> { MakeRoom("r1", 0, 0, 12, 12) },
            WorldObjects = new List<WorldObjectDto>
            {
                MakeObj("d1", "Desk",          WorldObjectKind.Other,  1f,  1f),
                MakeObj("c1", "Chair",         WorldObjectKind.Other,  2f,  1f),
                MakeObj("m1", "Microwave",     WorldObjectKind.Other,  3f,  1f),
                MakeObj("f1", "Fridge-item",   WorldObjectKind.Fridge, 4f,  1f),
                MakeObj("t1", "Toilet-unit",   WorldObjectKind.Toilet, 5f,  1f),
                MakeObj("s1", "Sink-unit",     WorldObjectKind.Sink,   6f,  1f),
                MakeObj("b1", "Bed-frame",     WorldObjectKind.Bed,    7f,  1f),
                MakeObj("o1", "OtherFurniture",WorldObjectKind.Other,  8f,  1f),
            },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions(FloorIndex: 0));

        Assert.Contains("D", result); // Desk
        Assert.Contains("C", result); // Chair
        Assert.Contains("M", result); // Microwave
        Assert.Contains("F", result); // Fridge
        Assert.Contains("T", result); // Toilet
        Assert.Contains("S", result); // Sink
        Assert.Contains("B", result); // Bed
        Assert.Contains("O", result); // Other
    }

    // ── AT-08: NPC rendering ──────────────────────────────────────────────────

    [Fact]
    public void AT08_NpcRendering_FirstLetterLower()
    {
        var state = EmptyWorld() with
        {
            Rooms    = new List<RoomDto> { MakeRoom("r1", 0, 0, 10, 10) },
            Entities = new List<EntityStateDto>
            {
                MakeNpc("Donna", 3f, 3f, DominantDrive.Eat),
            },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions(FloorIndex: 0));

        Assert.Contains("d", result);
        Assert.Contains("Donna", result);         // legend
        Assert.Contains("(3, 3)", result);         // legend coords
        Assert.Contains("Eating", result);         // legend drive label
    }

    // ── AT-09: NPC tile collision ─────────────────────────────────────────────

    [Fact]
    public void AT09_NpcTileCollision_StarGlyph()
    {
        var state = EmptyWorld() with
        {
            Rooms    = new List<RoomDto> { MakeRoom("r1", 0, 0, 10, 10) },
            Entities = new List<EntityStateDto>
            {
                MakeNpc("Donna",  3f, 3f),
                MakeNpc("Frank",  3f, 3f), // same tile
            },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions(FloorIndex: 0));

        // The grid must contain * (collision marker)
        Assert.Contains("*", result);
        // Both names in legend
        Assert.Contains("Donna", result);
        Assert.Contains("Frank", result);
    }

    // ── AT-10: NPC name-letter collision (different tiles) ────────────────────

    [Fact]
    public void AT10_NpcNameLetterCollision_BothRenderAsLetter()
    {
        var state = EmptyWorld() with
        {
            Rooms    = new List<RoomDto> { MakeRoom("r1", 0, 0, 10, 10) },
            Entities = new List<EntityStateDto>
            {
                MakeNpc("Donna",  2f, 3f),
                MakeNpc("Daniel", 5f, 3f), // different tile, same first letter 'd'
            },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions(FloorIndex: 0));

        // Both appear in legend
        Assert.Contains("Donna",  result);
        Assert.Contains("Daniel", result);

        // Tile-collision marker not used (they're on different tiles)
        // We check that d appears at least twice in the grid rows (one per NPC)
        var lines = result.Split('\n');
        var gridLines = lines.Skip(2).TakeWhile(l => l.Length > 0 && (l[0] == '╔' || l[0] == '║' || l[0] == '╚'
            || l.Contains('d'))).ToList();
        int dCount = gridLines.Sum(l => l.Count(ch => ch == 'd'));
        Assert.True(dCount >= 2, $"Expected at least 2 'd' glyphs in grid rows, found {dCount}");
    }

    // ── AT-11: Hazard rendering + Z-order ────────────────────────────────────

    [Fact]
    public void AT11_HazardRendering_ZOrder_NpcOverHazard()
    {
        // stain at (3, 3), fire at (5, 3), corpse at (7, 3)
        // NPC Donna at (7, 3) — same tile as corpse; NPC wins, hazard in legend.
        var state = EmptyWorld() with
        {
            Rooms    = new List<RoomDto> { MakeRoom("r1", 0, 0, 12, 8) },
            WorldObjects = new List<WorldObjectDto>
            {
                MakeObj("h1", "stain",  WorldObjectKind.Other, 3f, 3f),
                MakeObj("h2", "fire",   WorldObjectKind.Other, 5f, 3f),
                MakeObj("h3", "corpse", WorldObjectKind.Other, 7f, 3f),
            },
            Entities = new List<EntityStateDto>
            {
                MakeNpc("Donna", 7f, 3f), // same tile as corpse
            },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions(FloorIndex: 0));

        Assert.Contains("*", result);  // stain
        Assert.Contains("!", result);  // fire
        Assert.Contains("x", result);  // corpse appears in legend (NPC wins the tile)
        Assert.Contains("d", result);  // Donna wins the (7,3) tile
        Assert.Contains("Donna", result);
        Assert.Contains("corpse", result); // hazard still in legend
    }

    // ── AT-12: Z-order NPC over furniture ────────────────────────────────────

    [Fact]
    public void AT12_ZOrder_NpcOverFurniture()
    {
        // Desk at (3, 3), NPC Frank also at (3, 3)
        var state = EmptyWorld() with
        {
            Rooms    = new List<RoomDto> { MakeRoom("r1", 0, 0, 10, 10) },
            WorldObjects = new List<WorldObjectDto>
            {
                MakeObj("desk1", "Desk", WorldObjectKind.Other, 3f, 3f),
            },
            Entities = new List<EntityStateDto>
            {
                MakeNpc("Frank", 3f, 3f),
            },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions(FloorIndex: 0));

        // Frank's glyph 'f' appears in the grid; 'D' for desk is overwritten
        Assert.Contains("f", result);
        // Both appear in legend
        Assert.Contains("Frank", result);
        Assert.Contains("Desk", result);
    }

    // ── AT-13: IncludeLegend = false ──────────────────────────────────────────

    [Fact]
    public void AT13_NoLegend_OutputEndsAtGrid()
    {
        var state = EmptyWorld() with
        {
            Rooms    = new List<RoomDto> { MakeRoom("r1", 0, 0, 5, 5) },
            Entities = new List<EntityStateDto> { MakeNpc("Donna", 2f, 2f) },
        };

        var result = AsciiMapProjector.Render(state,
            new AsciiMapOptions(FloorIndex: 0, IncludeLegend: false));

        Assert.DoesNotContain("LEGEND", result);
        Assert.DoesNotContain("Donna", result);  // name only appears in legend
    }

    // ── AT-14: ShowHazards = false ────────────────────────────────────────────

    [Fact]
    public void AT14_ShowHazardsFalse_HazardsHidden()
    {
        var state = EmptyWorld() with
        {
            Rooms    = new List<RoomDto> { MakeRoom("r1", 0, 0, 10, 10) },
            WorldObjects = new List<WorldObjectDto>
            {
                MakeObj("h1", "fire",  WorldObjectKind.Other, 3f, 3f),
                MakeObj("h2", "stain", WorldObjectKind.Other, 5f, 3f),
            },
        };

        var result = AsciiMapProjector.Render(state,
            new AsciiMapOptions(FloorIndex: 0, ShowHazards: false));

        // Grid chars for hazards should not appear in context of their tile
        // (fire '!' and stain-as-hazard '*' should be absent outside wall context)
        Assert.DoesNotContain("!", result);
        Assert.DoesNotContain("fire",  result);
        Assert.DoesNotContain("stain", result);
    }

    // ── AT-15: Determinism ────────────────────────────────────────────────────

    [Fact]
    public void AT15_Determinism_HundredCallsIdentical()
    {
        var state = EmptyWorld() with
        {
            Rooms    = new List<RoomDto> { MakeRoom("r1", 0, 0, 10, 8, RoomCategory.Office) },
            Entities = new List<EntityStateDto>
            {
                MakeNpc("Alice", 3f, 3f, DominantDrive.Eat),
                MakeNpc("Bob",   5f, 4f, DominantDrive.Sleep),
            },
            WorldObjects = new List<WorldObjectDto>
            {
                MakeObj("m1", "Microwave", WorldObjectKind.Other, 4f, 2f),
            },
        };

        var opts = new AsciiMapOptions();
        string first = AsciiMapProjector.Render(state, opts);

        for (int i = 1; i < 100; i++)
            Assert.Equal(first, AsciiMapProjector.Render(state, opts));
    }

    // ── AT-16: Perf microbench <50ms mean ────────────────────────────────────

    [Fact]
    public void AT16_Perf_30Npcs_8Rooms_Under50msPerCall()
    {
        var rooms = new List<RoomDto>();
        for (int i = 0; i < 8; i++)
            rooms.Add(MakeRoom($"room{i}", i * 12, 0, 10, 10));

        // 2 corridors
        rooms.Add(MakeRoom("corridor1", 0,  10, 48, 3, RoomCategory.Hallway));
        rooms.Add(MakeRoom("corridor2", 0,  13, 48, 3, RoomCategory.Hallway));

        var rng      = new Random(42);
        var entities = Enumerable.Range(0, 30).Select(i =>
            MakeNpc($"Npc{(char)('A' + (i % 26))}{i}",
                rng.Next(1, 47),
                rng.Next(1, 16))).ToList();

        var state = EmptyWorld() with { Rooms = rooms, Entities = entities };
        var opts  = new AsciiMapOptions();

        // Warm-up
        AsciiMapProjector.Render(state, opts);

        const int runs = 100;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < runs; i++)
            AsciiMapProjector.Render(state, opts);
        sw.Stop();

        double meanMs = sw.Elapsed.TotalMilliseconds / runs;
        Assert.True(meanMs < 50.0,
            $"Mean render time {meanMs:F2} ms exceeded 50 ms budget.");
    }
}
