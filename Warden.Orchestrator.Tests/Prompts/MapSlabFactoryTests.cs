using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Warden.Contracts;
using Warden.Contracts.SchemaValidation;
using Warden.Contracts.Telemetry;
using Warden.Orchestrator.Cache;
using Warden.Orchestrator.Prompts;
using Warden.Telemetry.AsciiMap;
using Xunit;

namespace Warden.Orchestrator.Tests.Prompts;

#if WARDEN

/// <summary>
/// Acceptance tests for <see cref="MapSlabFactory"/> (WP-3.0.W.1).
///
/// AT-01 — MapSlabFactory.Build exists and compiles under #if WARDEN.
/// AT-02 — Empty WorldStateDto → both slabs render, no exception.
/// AT-03 — Single-room world: Stable contains wall outline; Volatile contains tick header.
/// AT-04 — Multi-NPC world: NPCs in Volatile, not in Stable.
/// AT-05 — Hazards in Volatile, not in Stable.
/// AT-06 — Fixed furniture (Microwave/Fridge/Sink/Toilet) in Stable legend; movable in Volatile.
/// AT-07 — IncludeStableInVolatile = true → Volatile carries full re-rendered map.
/// AT-08 — Cache dispositions: Ephemeral1h (Haiku), Ephemeral5m (Sonnet), Uncached (Volatile).
/// AT-09 — OpusSpecPacket.SpatialContext defaults false; old specs round-trip without error.
/// AT-12 — Token-budget warning fires when Stable exceeds 16 000 chars.
/// AT-14 — chore-validate.json parses cleanly via schema validator.
/// </summary>
public sealed class MapSlabFactoryTests
{
    // ── Fixtures ─────────────────────────────────────────────────────────────────

    private static WorldStateDto EmptyState(int tick = 0) => new()
    {
        Tick      = tick,
        Clock     = new ClockStateDto { GameTimeDisplay = "08:00" },
        Invariants = new InvariantDigestDto()
    };

    private static RoomDto MakeRoom(string id, int x, int y, int w, int h,
        RoomCategory cat = RoomCategory.Office) => new()
    {
        Id           = id,
        Name         = id,
        Category     = cat,
        Floor        = BuildingFloor.Basement,
        BoundsRect   = new BoundsRectDto { X = x, Y = y, Width = w, Height = h },
        Illumination = new IlluminationDto { AmbientLevel = 100 },
    };

    private static EntityStateDto MakeNpc(string name, float x, float y,
        DominantDrive drive = DominantDrive.None) => new()
    {
        Id      = name,
        ShortId = name,
        Name    = name,
        Species = SpeciesType.Human,
        Position = new PositionStateDto { X = x, Y = y, HasPosition = true },
        Drives   = new DrivesStateDto   { Dominant = drive },
        Physiology = new PhysiologyStateDto(),
    };

    private static WorldObjectDto MakeObject(string id, string name, WorldObjectKind kind,
        float x, float y) => new()
    {
        Id = id, Name = name, Kind = kind, X = x, Y = y
    };

    // ── AT-01: factory exists and compiles ────────────────────────────────────

    [Fact]
    public void AT01_BuildExists_ReturnsTwoSlabs()
    {
        var (stable, volatile_) = MapSlabFactory.Build(EmptyState());
        Assert.NotNull(stable);
        Assert.NotNull(volatile_);
    }

    // ── AT-02: empty state → no exception, minimal text ──────────────────────

    [Fact]
    public void AT02_EmptyState_BothSlabsNonEmpty()
    {
        var (stable, volatile_) = MapSlabFactory.Build(EmptyState());
        Assert.NotEmpty(stable.Text);
        Assert.NotEmpty(volatile_.Text);
    }

    // ── AT-03: single-room world ──────────────────────────────────────────────

    [Fact]
    public void AT03_SingleRoom_StableContainsWallsAndTickHeader()
    {
        var state = EmptyState(tick: 5) with
        {
            Rooms = new List<RoomDto> { MakeRoom("r1", 0, 0, 10, 8) }
        };

        var (stable, volatile_) = MapSlabFactory.Build(state);

        Assert.Contains("=== WORLD MAP — STABLE ===", stable.Text);
        Assert.Contains("═", stable.Text);     // outer boundary
        Assert.Contains("┌", stable.Text);     // inner wall corner

        Assert.Contains("TICK 5", volatile_.Text);
        Assert.Contains("08:00", volatile_.Text);
    }

    // ── AT-04: NPCs in Volatile, not in Stable ────────────────────────────────

    [Fact]
    public void AT04_MultiNpc_InVolatileNotInStable()
    {
        var state = EmptyState() with
        {
            Rooms    = new List<RoomDto> { MakeRoom("r1", 0, 0, 20, 15) },
            Entities = new List<EntityStateDto>
            {
                MakeNpc("Donna", 5, 5, DominantDrive.Eat),
                MakeNpc("Felix", 8, 7, DominantDrive.Sleep),
            }
        };

        var (stable, volatile_) = MapSlabFactory.Build(state);

        // NPCs (lowercase initials) must not appear in stable
        Assert.DoesNotContain("d (", stable.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("f (", stable.Text, StringComparison.Ordinal);

        // NPCs must appear in volatile
        Assert.Contains("d (5, 5) — Eating",  volatile_.Text);
        Assert.Contains("f (8, 7) — Sleeping", volatile_.Text);
    }

    // ── AT-05: hazards in Volatile, not in Stable ─────────────────────────────

    [Fact]
    public void AT05_Hazards_InVolatileNotInStable()
    {
        var state = EmptyState() with
        {
            Rooms       = new List<RoomDto> { MakeRoom("r1", 0, 0, 20, 15) },
            WorldObjects = new List<WorldObjectDto>
            {
                MakeObject("h1", "Coffee stain", WorldObjectKind.Other, 3, 4),
                MakeObject("h2", "Kitchen fire", WorldObjectKind.Other, 6, 9),
            }
        };

        var (stable, volatile_) = MapSlabFactory.Build(state);

        // Hazard glyphs must not appear in stable text body (only in volatile)
        Assert.DoesNotContain("Coffee stain", stable.Text);
        Assert.DoesNotContain("Kitchen fire", stable.Text);

        Assert.Contains("* Coffee stain", volatile_.Text);
        Assert.Contains("! Kitchen fire", volatile_.Text);
    }

    // ── AT-06: fixed furniture in Stable legend ───────────────────────────────

    [Fact]
    public void AT06_FixedFurniture_InStableLegend()
    {
        var state = EmptyState() with
        {
            Rooms        = new List<RoomDto> { MakeRoom("r1", 0, 0, 20, 15) },
            WorldObjects = new List<WorldObjectDto>
            {
                MakeObject("m1", "Microwave",  WorldObjectKind.Other,  3, 5),
                MakeObject("f1", "Fridge",     WorldObjectKind.Fridge, 5, 5),
                MakeObject("s1", "Sink",       WorldObjectKind.Sink,   7, 5),
                MakeObject("t1", "Toilet",     WorldObjectKind.Toilet, 9, 5),
            }
        };

        var (stable, _) = MapSlabFactory.Build(state);

        Assert.Contains("M — Microwave", stable.Text);
        Assert.Contains("F — Fridge",    stable.Text);
        Assert.Contains("S — Sink",      stable.Text);
        Assert.Contains("T — Toilet",    stable.Text);
    }

    // ── AT-07: IncludeStableInVolatile = true ────────────────────────────────

    [Fact]
    public void AT07_IncludeStableInVolatile_VolatileCarriesFullMap()
    {
        var state = EmptyState(tick: 3) with
        {
            Rooms    = new List<RoomDto> { MakeRoom("r1", 0, 0, 20, 15) },
            Entities = new List<EntityStateDto> { MakeNpc("Anna", 5, 5) }
        };

        var opts = new AsciiMapOptions(IncludeStableInVolatile: true);
        var (_, volatile_) = MapSlabFactory.Build(state, opts);

        // Full re-render: tick header present AND grid characters present
        Assert.Contains("TICK 3", volatile_.Text);
        Assert.Contains("═", volatile_.Text);  // outer boundary in full render
        Assert.Contains("a", volatile_.Text);  // NPC 'a' for Anna in grid
    }

    [Fact]
    public void AT07_DefaultMode_VolatileOmitsGrid()
    {
        var state = EmptyState(tick: 3) with
        {
            Rooms    = new List<RoomDto> { MakeRoom("r1", 0, 0, 20, 15) },
            Entities = new List<EntityStateDto> { MakeNpc("Anna", 5, 5) }
        };

        var (_, volatile_) = MapSlabFactory.Build(state);

        Assert.Contains("ASCII layer omitted to save tokens", volatile_.Text);
        Assert.Contains("a (5, 5)", volatile_.Text); // NPC in delta list
    }

    // ── AT-08: cache dispositions ─────────────────────────────────────────────

    [Fact]
    public void AT08_SonnetBatch_StableIsEphemeral5m()
    {
        var (stable, volatile_) = MapSlabFactory.Build(EmptyState(), isHaikuBatch: false);
        Assert.Equal(CacheDisposition.Ephemeral5m, stable.Cache);
        Assert.Equal(CacheDisposition.Uncached,    volatile_.Cache);
    }

    [Fact]
    public void AT08_HaikuBatch_StableIsEphemeral1h()
    {
        var (stable, volatile_) = MapSlabFactory.Build(EmptyState(), isHaikuBatch: true);
        Assert.Equal(CacheDisposition.Ephemeral1h, stable.Cache);
        Assert.Equal(CacheDisposition.Uncached,    volatile_.Cache);
    }

    // ── AT-09: OpusSpecPacket.SpatialContext defaults false ───────────────────

    [Fact]
    public void AT09_SpatialContext_DefaultsFalse()
    {
        var packet = new Warden.Contracts.Handshake.OpusSpecPacket();
        Assert.False(packet.SpatialContext);
    }

    [Fact]
    public void AT09_OldSpec_DeserializesWithoutError()
    {
        // A JSON that predates SpatialContext (field absent) must deserialize fine.
        const string json = """
            {
              "schemaVersion": "0.1.0",
              "specId": "spec-old-01",
              "missionId": "mission-old",
              "title": "Old spec",
              "rationale": "Test backward compat.",
              "inputs": { "referenceFiles": [] },
              "deliverables": [{ "kind": "doc", "path": "x.md", "description": "d" }],
              "acceptanceTests": [{ "id": "AT-01", "assertion": "a", "verification": "unit-test" }],
              "nonGoals": [],
              "timeboxMinutes": 5,
              "workerBudgetUsd": 0.10
            }
            """;

        var packet = System.Text.Json.JsonSerializer.Deserialize<
            Warden.Contracts.Handshake.OpusSpecPacket>(json, JsonOptions.Wire);

        Assert.NotNull(packet);
        Assert.False(packet!.SpatialContext);
    }

    // ── AT-12: token-budget warning ───────────────────────────────────────────

    [Fact]
    public void AT12_LargeMap_EmitsTokenWarning()
    {
        // Build a world large enough to exceed 16 000 chars in the stable render.
        // 60x60 with 8 rooms is documented as ~1 200 tokens; we need >16 000 chars.
        // Use a single enormous room to maximise grid size.
        var rooms = new List<RoomDto>
        {
            MakeRoom("big", 0, 0, 200, 200)
        };
        var state = EmptyState() with { Rooms = rooms };

        // Capture stderr
        var originalErr = Console.Error;
        using var capture = new System.IO.StringWriter();
        Console.SetError(capture);
        try
        {
            MapSlabFactory.Build(state);
        }
        finally
        {
            Console.SetError(originalErr);
        }

        var warning = capture.ToString();
        Assert.Contains("WARNING", warning);
        Assert.Contains("tokens", warning);
    }

    // ── AT-14: chore-validate.json parses via schema validator ────────────────

    [Fact]
    public void AT14_ChoreValidateSpec_ParsesCleanlyViaSchemaValidator()
    {
        var repoRoot = FindRepoRoot();
        var specPath = Path.Combine(repoRoot, "examples", "smoke-specs", "chore-validate.json");

        Assert.True(File.Exists(specPath),
            $"chore-validate.json not found at {specPath}");

        var json       = File.ReadAllText(specPath);
        var validation = SchemaValidator.Validate(json, Schema.OpusToSonnet);

        Assert.True(validation.IsValid,
            $"Schema validation failed: {string.Join("; ", validation.Errors)}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);

        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ECSSimulation.sln")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate repo root (ECSSimulation.sln not found in any ancestor directory).");
    }
}

#endif
