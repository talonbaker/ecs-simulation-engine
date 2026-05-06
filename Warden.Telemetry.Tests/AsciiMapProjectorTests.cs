using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Warden.Contracts.Telemetry;
using Warden.Telemetry.AsciiMap;
using Xunit;

namespace Warden.Telemetry.Tests;

/// <summary>
/// Acceptance tests for <see cref="AsciiMapProjector"/>. Glyph identities use the
/// canonical constants from <see cref="AsciiGlyphCatalog"/> — the symbol
/// vocabulary was extended in WP-3.0.W (extended) so every entity type owns
/// one dedicated codepoint.
///
/// AT-01 — Class exists with the public API; compiles with #if WARDEN guard.
/// AT-02 — Empty WorldStateDto → minimal exterior-boundary outline.
/// AT-03 — Single-room world → outer double-line + inner single-line wall outline.
/// AT-04 — Two-room world with open door → DoorOpen glyph at door tile.
/// AT-05 — Closed-door variant → DoorClosed glyph at door tile.
/// AT-06 — Room shading by category (Corridor / Breakroom / Bathroom).
/// AT-07 — All eight core furniture glyphs (Desk / Chair / Microwave / Fridge / Toilet / Sink / Bed / Other).
/// AT-08 — NPC rendering: Donna → 'd' at her tile.
/// AT-09 — NPC tile collision → NpcCollision glyph ('@'), both names in legend.
/// AT-10 — NPC name-letter collision: both render as 'd', both in legend.
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

        Assert.Contains(AsciiGlyphCatalog.DoorClosed.ToString(), result);
        Assert.DoesNotContain(AsciiGlyphCatalog.DoorOpen.ToString(), result);
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

        Assert.Contains(AsciiGlyphCatalog.FurnitureDesk.ToString(),      result);
        Assert.Contains(AsciiGlyphCatalog.FurnitureChair.ToString(),     result);
        Assert.Contains(AsciiGlyphCatalog.FurnitureMicrowave.ToString(), result);
        Assert.Contains(AsciiGlyphCatalog.FurnitureFridge.ToString(),    result);
        Assert.Contains(AsciiGlyphCatalog.FurnitureToilet.ToString(),    result);
        Assert.Contains(AsciiGlyphCatalog.FurnitureSink.ToString(),      result);
        Assert.Contains(AsciiGlyphCatalog.FurnitureBed.ToString(),       result);
        Assert.Contains(AsciiGlyphCatalog.FurnitureOther.ToString(),     result);
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
    public void AT09_NpcTileCollision_CollisionGlyph()
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

        // The grid must contain the collision marker ('@', distinct from every
        // hazard / furniture / wall glyph).
        Assert.Contains(AsciiGlyphCatalog.NpcCollision.ToString(), result);
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

        Assert.Contains(AsciiGlyphCatalog.HazardStain.ToString(),  result); // stain ∘
        Assert.Contains(AsciiGlyphCatalog.HazardFire.ToString(),   result); // fire  ▲
        Assert.Contains(AsciiGlyphCatalog.HazardCorpse.ToString(), result); // corpse legend entry uses †
        Assert.Contains("d", result);                                       // Donna wins the (7,3) tile
        Assert.Contains("Donna",  result);
        Assert.Contains("corpse", result);                                  // hazard still in legend
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

        // Hazard glyphs must be absent both from the grid and from the
        // SYMBOLS legend (DescribeActiveGlyphs only lists rendered glyphs).
        Assert.DoesNotContain(AsciiGlyphCatalog.HazardFire.ToString(),  result);
        Assert.DoesNotContain(AsciiGlyphCatalog.HazardStain.ToString(), result);
        // Hazard names must be absent from the (per-entity) LEGEND list.
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

    // ─────────────────────────────────────────────────────────────────────────
    //  Extended-vocabulary acceptance tests (WP-3.0.W extended)
    //
    //  Every distinct entity type owns one dedicated codepoint. The tests
    //  below pin those codepoints through AsciiGlyphCatalog so a glyph
    //  rename (e.g. swapping a door variant) surfaces as a single
    //  catalog edit rather than a scattered string-literal hunt.
    // ─────────────────────────────────────────────────────────────────────────

    private static NpcSaveDto MakeNpcSave(
        string id,
        SaveLifeState? lifeState = null,
        bool fainting             = false,
        bool choking              = false) =>
        new NpcSaveDto
        {
            Id        = id,
            Name      = id,
            LifeState = lifeState is null
                ? null
                : new LifeStateSaveDto { State = lifeState.Value },
            Fainting  = fainting ? new FaintSaveDto() : null,
            Choking   = choking  ? new ChokeSaveDto() : null,
        };

    /// <summary>
    /// Locked-door world-objects (Name contains "locked" + "door") render as
    /// the dedicated locked-door glyph, distinct from the closed-door glyph.
    /// </summary>
    [Fact]
    public void ExtendedDoors_LockedDoor_RendersLockedGlyph()
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
                MakeObj("d1", "locked-door", WorldObjectKind.Other, 4f, 2f),
            },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions());

        Assert.Contains(AsciiGlyphCatalog.DoorLocked.ToString(), result);
        Assert.DoesNotContain(AsciiGlyphCatalog.DoorClosed.ToString(), result);
    }

    /// <summary>
    /// Blocked / obstacle world-objects render as the dedicated blocked glyph.
    /// </summary>
    [Fact]
    public void ExtendedDoors_BlockedObstacle_RendersBlockedGlyph()
    {
        var state = EmptyWorld() with
        {
            Rooms = new List<RoomDto> { MakeRoom("r1", 0, 0, 6, 6) },
            WorldObjects = new List<WorldObjectDto>
            {
                MakeObj("o1", "blocked-passage", WorldObjectKind.Other, 2f, 2f),
            },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions());

        Assert.Contains(AsciiGlyphCatalog.DoorBlocked.ToString(), result);
    }

    /// <summary>
    /// Conference / Storage / Reception / Server-room categories all render
    /// their dedicated floor-shading glyphs.
    /// </summary>
    [Fact]
    public void ExtendedFloorShading_AllNewCategories()
    {
        var state = EmptyWorld() with
        {
            Rooms = new List<RoomDto>
            {
                MakeRoom("conf",   0,  0, 4, 4, RoomCategory.ConferenceRoom),
                MakeRoom("store",  4,  0, 4, 4, RoomCategory.SupplyCloset),
                MakeRoom("recpt",  8,  0, 4, 4, RoomCategory.Lobby),
                MakeRoom("svr",   12,  0, 4, 4, RoomCategory.ItCloset),
            },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions());

        Assert.Contains(AsciiGlyphCatalog.FloorConference.ToString(), result);
        Assert.Contains(AsciiGlyphCatalog.FloorStorage.ToString(),    result);
        Assert.Contains(AsciiGlyphCatalog.FloorReception.ToString(),  result);
        Assert.Contains(AsciiGlyphCatalog.FloorServer.ToString(),     result);
    }

    /// <summary>
    /// Eleven extended furniture categories — printer, coffee maker, sofa,
    /// conference table, whiteboard, filing cabinet, water cooler, vending
    /// machine, bookshelf, copy machine, plant — each render their
    /// dedicated glyph from the catalog.
    /// </summary>
    [Fact]
    public void ExtendedFurniture_AllNewCategories_DistinctGlyphs()
    {
        var state = EmptyWorld() with
        {
            Rooms = new List<RoomDto> { MakeRoom("r1", 0, 0, 16, 16) },
            WorldObjects = new List<WorldObjectDto>
            {
                MakeObj("p1",  "Printer",          WorldObjectKind.Other, 1f, 1f),
                MakeObj("cm1", "Coffee maker",     WorldObjectKind.Other, 2f, 1f),
                MakeObj("s1",  "Sofa",             WorldObjectKind.Other, 3f, 1f),
                MakeObj("ct1", "Conference Table", WorldObjectKind.Other, 4f, 1f),
                MakeObj("wb1", "Whiteboard",       WorldObjectKind.Other, 5f, 1f),
                MakeObj("fc1", "Filing Cabinet",   WorldObjectKind.Other, 6f, 1f),
                MakeObj("wc1", "Water cooler",     WorldObjectKind.Other, 7f, 1f),
                MakeObj("vm1", "Vending machine",  WorldObjectKind.Other, 8f, 1f),
                MakeObj("bs1", "Bookshelf",        WorldObjectKind.Other, 9f, 1f),
                MakeObj("cp1", "Copy machine",     WorldObjectKind.Other, 10f, 1f),
                MakeObj("pl1", "Plant",            WorldObjectKind.Other, 11f, 1f),
            },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions());

        Assert.Contains(AsciiGlyphCatalog.FurniturePrinter.ToString(),         result);
        Assert.Contains(AsciiGlyphCatalog.FurnitureCoffeeMaker.ToString(),     result);
        Assert.Contains(AsciiGlyphCatalog.FurnitureSofa.ToString(),            result);
        Assert.Contains(AsciiGlyphCatalog.FurnitureConferenceTable.ToString(), result);
        Assert.Contains(AsciiGlyphCatalog.FurnitureWhiteboard.ToString(),      result);
        Assert.Contains(AsciiGlyphCatalog.FurnitureFilingCabinet.ToString(),   result);
        Assert.Contains(AsciiGlyphCatalog.FurnitureWaterCooler.ToString(),     result);
        Assert.Contains(AsciiGlyphCatalog.FurnitureVendingMachine.ToString(),  result);
        Assert.Contains(AsciiGlyphCatalog.FurnitureBookshelf.ToString(),       result);
        Assert.Contains(AsciiGlyphCatalog.FurnitureCopyMachine.ToString(),     result);
        Assert.Contains(AsciiGlyphCatalog.FurniturePlant.ToString(),           result);
    }

    /// <summary>
    /// Extended hazard categories — broken glass, oil slick, vomit — each
    /// render their dedicated glyph.
    /// </summary>
    [Fact]
    public void ExtendedHazards_GlassOilVomit_DistinctGlyphs()
    {
        var state = EmptyWorld() with
        {
            Rooms = new List<RoomDto> { MakeRoom("r1", 0, 0, 10, 6) },
            WorldObjects = new List<WorldObjectDto>
            {
                MakeObj("g1", "broken-glass", WorldObjectKind.Other, 1f, 1f),
                MakeObj("o1", "oil-slick",    WorldObjectKind.Other, 3f, 1f),
                MakeObj("v1", "vomit",        WorldObjectKind.Other, 5f, 1f),
            },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions());

        Assert.Contains(AsciiGlyphCatalog.HazardBrokenGlass.ToString(), result);
        Assert.Contains(AsciiGlyphCatalog.HazardOilSlick.ToString(),    result);
        Assert.Contains(AsciiGlyphCatalog.HazardVomit.ToString(),       result);
    }

    /// <summary>
    /// Sleeping NPC (Physiology.IsSleeping = true) renders as the dedicated
    /// sleeping glyph in place of the lowercase name letter.
    /// </summary>
    [Fact]
    public void NpcState_Sleeping_RendersSleepGlyph()
    {
        var donna = MakeNpc("Donna", 3f, 3f) with
        {
            Physiology = new PhysiologyStateDto { IsSleeping = true },
        };

        var state = EmptyWorld() with
        {
            Rooms    = new List<RoomDto> { MakeRoom("r1", 0, 0, 10, 6) },
            Entities = new List<EntityStateDto> { donna },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions());

        Assert.Contains(AsciiGlyphCatalog.NpcSleeping.ToString(), result);
        // 'd' should still appear in the LEGEND for the name "Donna" but
        // the on-tile glyph is replaced; that means at least one ezh rendered.
    }

    /// <summary>
    /// Fainted NPC (NpcSaveDto.Fainting != null) renders as the fainted glyph.
    /// </summary>
    [Fact]
    public void NpcState_Fainted_RendersFaintedGlyph()
    {
        var npc   = MakeNpc("Greg", 4f, 4f);
        var state = EmptyWorld() with
        {
            Rooms         = new List<RoomDto> { MakeRoom("r1", 0, 0, 10, 8) },
            Entities      = new List<EntityStateDto> { npc },
            NpcSaveStates = new[] { MakeNpcSave("Greg", fainting: true) },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions());

        Assert.Contains(AsciiGlyphCatalog.NpcFainted.ToString(), result);
    }

    /// <summary>
    /// Choking NPC (NpcSaveDto.Choking != null) renders as the choking glyph.
    /// </summary>
    [Fact]
    public void NpcState_Choking_RendersChokingGlyph()
    {
        var npc   = MakeNpc("Frank", 4f, 4f);
        var state = EmptyWorld() with
        {
            Rooms         = new List<RoomDto> { MakeRoom("r1", 0, 0, 10, 8) },
            Entities      = new List<EntityStateDto> { npc },
            NpcSaveStates = new[] { MakeNpcSave("Frank", choking: true) },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions());

        Assert.Contains(AsciiGlyphCatalog.NpcChoking.ToString(), result);
    }

    /// <summary>
    /// Deceased NPC (NpcSaveDto.LifeState.State == Deceased) renders as the
    /// deceased glyph (dagger — same codepoint as <see cref="AsciiGlyphCatalog.HazardCorpse"/>).
    /// State precedence: deceased beats fainted beats choking beats sleeping.
    /// </summary>
    [Fact]
    public void NpcState_Deceased_RendersDeceasedGlyph()
    {
        var npc   = MakeNpc("Alice", 5f, 5f);
        var state = EmptyWorld() with
        {
            Rooms         = new List<RoomDto> { MakeRoom("r1", 0, 0, 10, 8) },
            Entities      = new List<EntityStateDto> { npc },
            NpcSaveStates = new[] { MakeNpcSave("Alice", lifeState: SaveLifeState.Deceased) },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions());

        Assert.Contains(AsciiGlyphCatalog.NpcDeceased.ToString(), result);
    }

    /// <summary>
    /// SYMBOLS section appears at the bottom of every legend-enabled render
    /// and lists only catalog glyphs that are actually rendered on the map.
    /// </summary>
    [Fact]
    public void SymbolsLegend_ListsOnlyActiveGlyphs()
    {
        var state = EmptyWorld() with
        {
            Rooms = new List<RoomDto>
            {
                MakeRoom("r1", 0, 0, 6, 6, RoomCategory.Bathroom),
            },
            WorldObjects = new List<WorldObjectDto>
            {
                MakeObj("s1", "Sink", WorldObjectKind.Sink, 2f, 2f),
            },
        };

        var result = AsciiMapProjector.Render(state, new AsciiMapOptions());

        Assert.Contains("SYMBOLS", result);

        // Active categories must be described.
        Assert.Contains("Bathroom",          result);
        Assert.Contains("Sink",              result);
        Assert.Contains("Exterior boundary", result);

        // Inactive categories must NOT be described in SYMBOLS.
        Assert.DoesNotContain("Microwave",        result);
        Assert.DoesNotContain("Vending machine",  result);
        Assert.DoesNotContain("Printer",          result);
        Assert.DoesNotContain("Fire",             result);
    }

    /// <summary>
    /// <see cref="AsciiGlyphCatalog.RenderReference"/> emits every catalog
    /// glyph in a single printable block. Used as a one-shot decoder ring
    /// for Haiku prompt slabs.
    /// </summary>
    [Fact]
    public void GlyphCatalog_RenderReference_IncludesEveryCategory()
    {
        var reference = AsciiGlyphCatalog.RenderReference();

        Assert.Contains("WALLS",          reference);
        Assert.Contains("DOORS",          reference);
        Assert.Contains("FLOOR SHADING",  reference);
        Assert.Contains("FURNITURE",      reference);
        Assert.Contains("HAZARDS",        reference);
        Assert.Contains("NPCS",           reference);

        // Spot-check that each category renders at least one canonical glyph.
        Assert.Contains(AsciiGlyphCatalog.WallExteriorTopLeft.ToString(),      reference);
        Assert.Contains(AsciiGlyphCatalog.DoorOpen.ToString(),                 reference);
        Assert.Contains(AsciiGlyphCatalog.FloorCorridor.ToString(),            reference);
        Assert.Contains(AsciiGlyphCatalog.FurnitureMicrowave.ToString(),       reference);
        Assert.Contains(AsciiGlyphCatalog.HazardFire.ToString(),               reference);
        Assert.Contains(AsciiGlyphCatalog.NpcSleeping.ToString(),              reference);
    }

    /// <summary>
    /// Every catalog codepoint is unique within its semantic category. The
    /// only intentional cross-category alias is <see cref="AsciiGlyphCatalog.HazardCorpse"/>
    /// = <see cref="AsciiGlyphCatalog.NpcDeceased"/> ('†' — the dagger
    /// represents death in either context). All other codepoints are
    /// pairwise distinct.
    /// </summary>
    [Fact]
    public void GlyphCatalog_NoUnintendedCodepointReuse()
    {
        // Build a flat list of all catalog constants with their owning category.
        (string Category, string Name, char Glyph)[] all =
        {
            ("Wall",     nameof(AsciiGlyphCatalog.WallInteriorTopLeft),     AsciiGlyphCatalog.WallInteriorTopLeft),
            ("Wall",     nameof(AsciiGlyphCatalog.WallInteriorTopRight),    AsciiGlyphCatalog.WallInteriorTopRight),
            ("Wall",     nameof(AsciiGlyphCatalog.WallInteriorBottomLeft),  AsciiGlyphCatalog.WallInteriorBottomLeft),
            ("Wall",     nameof(AsciiGlyphCatalog.WallInteriorBottomRight), AsciiGlyphCatalog.WallInteriorBottomRight),
            ("Wall",     nameof(AsciiGlyphCatalog.WallInteriorHorizontal),  AsciiGlyphCatalog.WallInteriorHorizontal),
            ("Wall",     nameof(AsciiGlyphCatalog.WallInteriorVertical),    AsciiGlyphCatalog.WallInteriorVertical),
            ("Wall",     nameof(AsciiGlyphCatalog.WallInteriorTDown),       AsciiGlyphCatalog.WallInteriorTDown),
            ("Wall",     nameof(AsciiGlyphCatalog.WallInteriorTUp),         AsciiGlyphCatalog.WallInteriorTUp),
            ("Wall",     nameof(AsciiGlyphCatalog.WallInteriorTRight),      AsciiGlyphCatalog.WallInteriorTRight),
            ("Wall",     nameof(AsciiGlyphCatalog.WallInteriorTLeft),       AsciiGlyphCatalog.WallInteriorTLeft),
            ("Wall",     nameof(AsciiGlyphCatalog.WallInteriorCross),       AsciiGlyphCatalog.WallInteriorCross),
            ("Wall",     nameof(AsciiGlyphCatalog.WallExteriorTopLeft),     AsciiGlyphCatalog.WallExteriorTopLeft),
            ("Wall",     nameof(AsciiGlyphCatalog.WallExteriorTopRight),    AsciiGlyphCatalog.WallExteriorTopRight),
            ("Wall",     nameof(AsciiGlyphCatalog.WallExteriorBottomLeft),  AsciiGlyphCatalog.WallExteriorBottomLeft),
            ("Wall",     nameof(AsciiGlyphCatalog.WallExteriorBottomRight), AsciiGlyphCatalog.WallExteriorBottomRight),
            ("Wall",     nameof(AsciiGlyphCatalog.WallExteriorHorizontal),  AsciiGlyphCatalog.WallExteriorHorizontal),
            ("Wall",     nameof(AsciiGlyphCatalog.WallExteriorVertical),    AsciiGlyphCatalog.WallExteriorVertical),
            ("Door",     nameof(AsciiGlyphCatalog.DoorOpen),                AsciiGlyphCatalog.DoorOpen),
            ("Door",     nameof(AsciiGlyphCatalog.DoorClosed),              AsciiGlyphCatalog.DoorClosed),
            ("Door",     nameof(AsciiGlyphCatalog.DoorLocked),              AsciiGlyphCatalog.DoorLocked),
            ("Door",     nameof(AsciiGlyphCatalog.DoorBlocked),             AsciiGlyphCatalog.DoorBlocked),
            ("Floor",    nameof(AsciiGlyphCatalog.FloorCorridor),           AsciiGlyphCatalog.FloorCorridor),
            ("Floor",    nameof(AsciiGlyphCatalog.FloorBreakroom),          AsciiGlyphCatalog.FloorBreakroom),
            ("Floor",    nameof(AsciiGlyphCatalog.FloorBathroom),           AsciiGlyphCatalog.FloorBathroom),
            ("Floor",    nameof(AsciiGlyphCatalog.FloorConference),         AsciiGlyphCatalog.FloorConference),
            ("Floor",    nameof(AsciiGlyphCatalog.FloorStorage),            AsciiGlyphCatalog.FloorStorage),
            ("Floor",    nameof(AsciiGlyphCatalog.FloorReception),          AsciiGlyphCatalog.FloorReception),
            ("Floor",    nameof(AsciiGlyphCatalog.FloorServer),             AsciiGlyphCatalog.FloorServer),
            ("Furniture",nameof(AsciiGlyphCatalog.FurnitureDesk),            AsciiGlyphCatalog.FurnitureDesk),
            ("Furniture",nameof(AsciiGlyphCatalog.FurnitureChair),           AsciiGlyphCatalog.FurnitureChair),
            ("Furniture",nameof(AsciiGlyphCatalog.FurnitureMicrowave),       AsciiGlyphCatalog.FurnitureMicrowave),
            ("Furniture",nameof(AsciiGlyphCatalog.FurnitureFridge),          AsciiGlyphCatalog.FurnitureFridge),
            ("Furniture",nameof(AsciiGlyphCatalog.FurnitureToilet),          AsciiGlyphCatalog.FurnitureToilet),
            ("Furniture",nameof(AsciiGlyphCatalog.FurnitureSink),            AsciiGlyphCatalog.FurnitureSink),
            ("Furniture",nameof(AsciiGlyphCatalog.FurnitureBed),             AsciiGlyphCatalog.FurnitureBed),
            ("Furniture",nameof(AsciiGlyphCatalog.FurniturePrinter),         AsciiGlyphCatalog.FurniturePrinter),
            ("Furniture",nameof(AsciiGlyphCatalog.FurnitureCoffeeMaker),     AsciiGlyphCatalog.FurnitureCoffeeMaker),
            ("Furniture",nameof(AsciiGlyphCatalog.FurnitureSofa),            AsciiGlyphCatalog.FurnitureSofa),
            ("Furniture",nameof(AsciiGlyphCatalog.FurnitureConferenceTable), AsciiGlyphCatalog.FurnitureConferenceTable),
            ("Furniture",nameof(AsciiGlyphCatalog.FurnitureWhiteboard),      AsciiGlyphCatalog.FurnitureWhiteboard),
            ("Furniture",nameof(AsciiGlyphCatalog.FurnitureFilingCabinet),   AsciiGlyphCatalog.FurnitureFilingCabinet),
            ("Furniture",nameof(AsciiGlyphCatalog.FurnitureWaterCooler),     AsciiGlyphCatalog.FurnitureWaterCooler),
            ("Furniture",nameof(AsciiGlyphCatalog.FurnitureVendingMachine),  AsciiGlyphCatalog.FurnitureVendingMachine),
            ("Furniture",nameof(AsciiGlyphCatalog.FurnitureBookshelf),       AsciiGlyphCatalog.FurnitureBookshelf),
            ("Furniture",nameof(AsciiGlyphCatalog.FurnitureCopyMachine),     AsciiGlyphCatalog.FurnitureCopyMachine),
            ("Furniture",nameof(AsciiGlyphCatalog.FurniturePlant),           AsciiGlyphCatalog.FurniturePlant),
            ("Furniture",nameof(AsciiGlyphCatalog.FurnitureOther),           AsciiGlyphCatalog.FurnitureOther),
            ("Hazard",   nameof(AsciiGlyphCatalog.HazardFire),               AsciiGlyphCatalog.HazardFire),
            ("Hazard",   nameof(AsciiGlyphCatalog.HazardWater),              AsciiGlyphCatalog.HazardWater),
            ("Hazard",   nameof(AsciiGlyphCatalog.HazardStain),              AsciiGlyphCatalog.HazardStain),
            ("Hazard",   nameof(AsciiGlyphCatalog.HazardCorpse),             AsciiGlyphCatalog.HazardCorpse),
            ("Hazard",   nameof(AsciiGlyphCatalog.HazardBrokenGlass),        AsciiGlyphCatalog.HazardBrokenGlass),
            ("Hazard",   nameof(AsciiGlyphCatalog.HazardOilSlick),           AsciiGlyphCatalog.HazardOilSlick),
            ("Hazard",   nameof(AsciiGlyphCatalog.HazardVomit),              AsciiGlyphCatalog.HazardVomit),
            ("Hazard",   nameof(AsciiGlyphCatalog.HazardUnknown),            AsciiGlyphCatalog.HazardUnknown),
            ("Npc",      nameof(AsciiGlyphCatalog.NpcCollision),             AsciiGlyphCatalog.NpcCollision),
            ("Npc",      nameof(AsciiGlyphCatalog.NpcSleeping),              AsciiGlyphCatalog.NpcSleeping),
            ("Npc",      nameof(AsciiGlyphCatalog.NpcFainted),               AsciiGlyphCatalog.NpcFainted),
            ("Npc",      nameof(AsciiGlyphCatalog.NpcDeceased),              AsciiGlyphCatalog.NpcDeceased),
            ("Npc",      nameof(AsciiGlyphCatalog.NpcChoking),               AsciiGlyphCatalog.NpcChoking),
            ("Npc",      nameof(AsciiGlyphCatalog.NpcInConversation),        AsciiGlyphCatalog.NpcInConversation),
        };

        // Within each category every glyph must be unique.
        foreach (var byCat in all.GroupBy(t => t.Category))
        {
            var dupes = byCat.GroupBy(t => t.Glyph).Where(g => g.Count() > 1).ToList();
            Assert.True(dupes.Count == 0,
                $"Category '{byCat.Key}' has duplicate codepoints: " +
                string.Join(", ", dupes.Select(g => $"{g.Key}=[{string.Join(",", g.Select(x => x.Name))}]")));
        }

        // The dagger (corpse / deceased) is the only sanctioned alias across
        // categories; verify nothing else collides.
        var crossCat = all.GroupBy(t => t.Glyph)
                          .Where(g => g.Select(t => t.Category).Distinct().Count() > 1)
                          .ToList();
        Assert.Single(crossCat);
        var alias = crossCat[0].Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { nameof(AsciiGlyphCatalog.HazardCorpse), nameof(AsciiGlyphCatalog.NpcDeceased) }, alias);
    }
}
