using System;
using System.IO;
using System.Linq;
using APIFramework.Bootstrap;
using APIFramework.Components;
using APIFramework.Core;
using Xunit;

namespace APIFramework.Tests.Bootstrap;

/// <summary>
/// Unit tests for <see cref="WorldDefinitionLoader"/>.
///
/// AT-03 — LoadFromFile with the starter produces ≥6 rooms, ≥8 sources, ≥2 apertures, ≥5 NPC slots.
/// AT-04 — Each RoomTag entity has a RoomComponent with correct fields from the JSON.
/// AT-05 — Rooms with namedAnchorTag populate NamedAnchorComponent with correct values.
/// AT-06 — A malformed file throws WorldDefinitionInvalidException with a specific error message.
/// AT-08 — Two loads with same seed produce byte-identical entity component values.
/// </summary>
public class WorldDefinitionLoaderTests
{
    // -- AT-03: entity counts --------------------------------------------------

    [Fact]
    public void LoadFromFile_StarterJson_ProducesMinimumEntityCounts()
    {
        using var tmp = TempFile(StarterJson);
        var em  = new EntityManager();
        var rng = new SeededRandom(1);

        var result = WorldDefinitionLoader.LoadFromFile(tmp.Path, em, rng);

        Assert.True(result.RoomCount       >= 6,  $"Expected ≥6 rooms, got {result.RoomCount}");
        Assert.True(result.LightSourceCount >= 8,  $"Expected ≥8 light sources, got {result.LightSourceCount}");
        Assert.True(result.ApertureCount   >= 2,  $"Expected ≥2 apertures, got {result.ApertureCount}");
        Assert.True(result.NpcSlotCount    >= 5,  $"Expected ≥5 NPC slots, got {result.NpcSlotCount}");
    }

    [Fact]
    public void LoadFromFile_StarterJson_EntityManagerContainsCorrectTagCounts()
    {
        using var tmp = TempFile(StarterJson);
        var em  = new EntityManager();
        var rng = new SeededRandom(1);

        WorldDefinitionLoader.LoadFromFile(tmp.Path, em, rng);

        int rooms    = em.Query<RoomTag>().Count();
        int sources  = em.Query<LightSourceTag>().Count();
        int apertures= em.Query<LightApertureTag>().Count();
        int slots    = em.Query<NpcSlotTag>().Count();

        Assert.True(rooms     >= 6, $"EM: expected ≥6 room entities, got {rooms}");
        Assert.True(sources   >= 8, $"EM: expected ≥8 light-source entities, got {sources}");
        Assert.True(apertures >= 2, $"EM: expected ≥2 aperture entities, got {apertures}");
        Assert.True(slots     >= 5, $"EM: expected ≥5 NPC-slot entities, got {slots}");
    }

    // -- AT-04: RoomComponent fields match JSON --------------------------------

    [Fact]
    public void LoadFromFile_BreakroomEntity_HasCorrectRoomComponentFields()
    {
        using var tmp = TempFile(StarterJson);
        var em  = new EntityManager();
        var rng = new SeededRandom(1);

        WorldDefinitionLoader.LoadFromFile(tmp.Path, em, rng);

        var breakroom = em.Query<RoomTag>()
            .Select(e => e.Get<RoomComponent>())
            .FirstOrDefault(rc => rc.Id == "first-floor-breakroom");

        Assert.Equal("first-floor-breakroom", breakroom.Id); // fails if room not found (struct default has null Id)
        Assert.Equal("First-Floor Breakroom", breakroom.Name);
        Assert.Equal(RoomCategory.Breakroom, breakroom.Category);
        Assert.Equal(BuildingFloor.First, breakroom.Floor);
        Assert.Equal(2, breakroom.Bounds.X);
        Assert.Equal(2, breakroom.Bounds.Y);
        Assert.Equal(15, breakroom.Bounds.Width);
        Assert.Equal(12, breakroom.Bounds.Height);
        Assert.Equal(55, breakroom.Illumination.AmbientLevel);
        Assert.Equal(3800, breakroom.Illumination.ColorTemperatureK);
    }

    [Fact]
    public void LoadFromFile_ConferenceRoom_HasCorrectFloor()
    {
        using var tmp = TempFile(StarterJson);
        var em  = new EntityManager();
        var rng = new SeededRandom(1);

        WorldDefinitionLoader.LoadFromFile(tmp.Path, em, rng);

        var conf = em.Query<RoomTag>()
            .Select(e => e.Get<RoomComponent>())
            .FirstOrDefault(rc => rc.Id == "top-floor-conference-room");

        Assert.Equal("top-floor-conference-room", conf.Id); // fails if room not found
        Assert.Equal(BuildingFloor.Top, conf.Floor);
    }

    // -- AT-05: NamedAnchorComponent populated correctly -----------------------

    [Fact]
    public void LoadFromFile_BreakroomEntity_HasNamedAnchorComponent_WithCorrectTag()
    {
        using var tmp = TempFile(StarterJson);
        var em  = new EntityManager();
        var rng = new SeededRandom(1);

        WorldDefinitionLoader.LoadFromFile(tmp.Path, em, rng);

        var entity = em.Query<RoomTag>()
            .FirstOrDefault(e => e.Get<RoomComponent>().Id == "first-floor-breakroom");

        Assert.NotNull(entity);
        Assert.True(entity!.Has<NamedAnchorComponent>(), "Breakroom should have NamedAnchorComponent.");

        var anchor = entity.Get<NamedAnchorComponent>();
        Assert.Equal("the-microwave", anchor.Tag);
        Assert.Equal("old-microwave", anchor.SmellTag);
        Assert.False(string.IsNullOrEmpty(anchor.Description));
    }

    [Fact]
    public void LoadFromFile_BreakroomEntity_HasNoteComponent_WithExpectedNotes()
    {
        using var tmp = TempFile(StarterJson);
        var em  = new EntityManager();
        var rng = new SeededRandom(1);

        WorldDefinitionLoader.LoadFromFile(tmp.Path, em, rng);

        var entity = em.Query<RoomTag>()
            .FirstOrDefault(e => e.Get<RoomComponent>().Id == "first-floor-breakroom");

        Assert.NotNull(entity);
        Assert.True(entity!.Has<NoteComponent>(), "Breakroom should have NoteComponent.");
        var note  = entity.Get<NoteComponent>();
        var notes = note.Notes ?? System.Array.Empty<string>();
        Assert.True(notes.Count >= 2, $"Expected ≥2 notes, got {notes.Count}");
        Assert.Contains(notes, n => n.Contains("PLEASE LABEL"));
    }

    [Fact]
    public void LoadFromFile_CubicleGridWest_HasNoNamedAnchor()
    {
        using var tmp = TempFile(StarterJson);
        var em  = new EntityManager();
        var rng = new SeededRandom(1);

        WorldDefinitionLoader.LoadFromFile(tmp.Path, em, rng);

        var entity = em.Query<RoomTag>()
            .FirstOrDefault(e => e.Get<RoomComponent>().Id == "first-floor-cubicle-grid-west");

        Assert.NotNull(entity);
        // This room has no namedAnchorTag in the starter.
        Assert.False(entity!.Has<NamedAnchorComponent>(),
            "first-floor-cubicle-grid-west should not have a NamedAnchorComponent.");
    }

    // -- AT-06: malformed files throw WorldDefinitionInvalidException ---------

    [Fact]
    public void LoadFromFile_MissingSchemaVersion_ThrowsWithErrorDetail()
    {
        const string bad = """
            {
              "worldId": "test", "name": "Test", "seed": 1,
              "floors": [], "rooms": [], "lightSources": [],
              "lightApertures": [], "npcSlots": [], "objectsAtAnchors": []
            }
            """;

        using var tmp = TempFile(bad);
        var em  = new EntityManager();
        var rng = new SeededRandom(1);

        var ex = Assert.Throws<WorldDefinitionInvalidException>(
            () => WorldDefinitionLoader.LoadFromFile(tmp.Path, em, rng));

        Assert.NotEmpty(ex.ValidationErrors);
        Assert.Contains(ex.ValidationErrors, e => e.Contains("schemaVersion"));
    }

    [Fact]
    public void LoadFromFile_NegativeSeed_ThrowsWithErrorDetail()
    {
        const string bad = """
            {
              "schemaVersion": "0.1.0", "worldId": "test", "name": "Test", "seed": -5,
              "floors": [], "rooms": [], "lightSources": [],
              "lightApertures": [], "npcSlots": [], "objectsAtAnchors": []
            }
            """;

        using var tmp = TempFile(bad);
        var em  = new EntityManager();
        var rng = new SeededRandom(1);

        var ex = Assert.Throws<WorldDefinitionInvalidException>(
            () => WorldDefinitionLoader.LoadFromFile(tmp.Path, em, rng));

        Assert.NotEmpty(ex.ValidationErrors);
        Assert.Contains(ex.ValidationErrors, e => e.Contains("seed") || e.Contains("minimum"));
    }

    // -- AT-08: determinism ----------------------------------------------------

    [Fact]
    public void LoadFromFile_TwoRunsSameSeed_ProduceIdenticalRoomComponents()
    {
        using var tmp = TempFile(StarterJson);

        // Run 1
        var em1  = new EntityManager();
        var rng1 = new SeededRandom(42);
        var r1   = WorldDefinitionLoader.LoadFromFile(tmp.Path, em1, rng1);

        // Run 2
        var em2  = new EntityManager();
        var rng2 = new SeededRandom(42);
        var r2   = WorldDefinitionLoader.LoadFromFile(tmp.Path, em2, rng2);

        Assert.Equal(r1.RoomCount,        r2.RoomCount);
        Assert.Equal(r1.LightSourceCount, r2.LightSourceCount);
        Assert.Equal(r1.ApertureCount,    r2.ApertureCount);
        Assert.Equal(r1.NpcSlotCount,     r2.NpcSlotCount);

        // Verify room component values are identical.
        var rooms1 = em1.Query<RoomTag>().Select(e => e.Get<RoomComponent>()).OrderBy(r => r.Id).ToList();
        var rooms2 = em2.Query<RoomTag>().Select(e => e.Get<RoomComponent>()).OrderBy(r => r.Id).ToList();

        Assert.Equal(rooms1.Count, rooms2.Count);
        for (int i = 0; i < rooms1.Count; i++)
        {
            Assert.Equal(rooms1[i].Id,       rooms2[i].Id);
            Assert.Equal(rooms1[i].Name,     rooms2[i].Name);
            Assert.Equal(rooms1[i].Category, rooms2[i].Category);
            Assert.Equal(rooms1[i].Floor,    rooms2[i].Floor);
            Assert.Equal(rooms1[i].Bounds.X, rooms2[i].Bounds.X);
            Assert.Equal(rooms1[i].Bounds.Y, rooms2[i].Bounds.Y);
            Assert.Equal(rooms1[i].Illumination.AmbientLevel, rooms2[i].Illumination.AmbientLevel);
        }
    }

    [Fact]
    public void LoadFromFile_SeedIsPreservedInLoadResult()
    {
        using var tmp = TempFile(StarterJson);
        var em  = new EntityManager();
        var rng = new SeededRandom(99);

        var result = WorldDefinitionLoader.LoadFromFile(tmp.Path, em, rng);

        Assert.Equal(19990101, result.SeedUsed); // seed from the starter JSON
    }

    // -- Helpers ---------------------------------------------------------------

    private static TempJsonFile TempFile(string content) => new(content);

    private sealed class TempJsonFile : IDisposable
    {
        public string Path { get; }
        public TempJsonFile(string content)
        {
            Path = System.IO.Path.GetTempFileName() + ".json";
            File.WriteAllText(Path, content);
        }
        public void Dispose()
        {
            if (File.Exists(Path)) File.Delete(Path);
        }
    }

    // -- Starter JSON (mirrors office-starter.json) ----------------------------

    private static readonly string StarterJson = LoadStarterJson();

    private static string LoadStarterJson()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = System.IO.Path.Combine(
                dir.FullName, "docs", "c2-content", "world-definitions", "office-starter.json");
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not locate office-starter.json.");
    }
}
