using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using APIFramework.Bootstrap;
using APIFramework.Components;
using APIFramework.Core;
using Warden.Contracts;
using Xunit;

namespace APIFramework.Tests.Bootstrap;

/// <summary>
/// Per-section + round-trip tests for <see cref="WorldDefinitionWriter"/>.
/// Round-trip discipline: <c>Load(file) → Write(string) → Load(written)</c> must produce
/// equivalent worlds (same room/light/aperture/anchor counts and key fields).
/// </summary>
public class WorldDefinitionWriterTests
{
    // ── AT-01: writer produces valid JSON ────────────────────────────────────────

    [Fact]
    public void WriteToString_ProducesValidJsonParsableBackToWorldDefinition()
    {
        using var tmp = TempFile(StarterJson);
        var em        = new EntityManager();
        var rng       = new SeededRandom(1);
        WorldDefinitionLoader.LoadFromFile(tmp.Path, em, rng);

        var json = WorldDefinitionWriter.WriteToString(em, "round-trip-test", "Round Trip Test", seed: 42);

        Assert.False(string.IsNullOrWhiteSpace(json));
        // Parses cleanly back into a WorldDefinitionDto via the same JsonOptions the loader uses.
        // We use System.Text.Json's element parsing so we don't depend on the DTO's internal accessibility.
        var doc = JsonDocument.Parse(json);
        Assert.Equal("0.1.0",          doc.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("round-trip-test",doc.RootElement.GetProperty("worldId").GetString());
        Assert.Equal("Round Trip Test",doc.RootElement.GetProperty("name").GetString());
        Assert.Equal(42,               doc.RootElement.GetProperty("seed").GetInt32());
    }

    // ── AT-02 / AT-03: round-trip preserves rooms / lights / apertures / NPCs ────

    [Fact]
    public void RoundTrip_OfficeStarter_PreservesRoomCounts()
    {
        var (countsA, countsB) = RoundTrip(StarterJson);
        Assert.Equal(countsA.Rooms,        countsB.Rooms);
        Assert.Equal(countsA.LightSources, countsB.LightSources);
        Assert.Equal(countsA.Apertures,    countsB.Apertures);
        Assert.Equal(countsA.NpcSlots,     countsB.NpcSlots);
        Assert.Equal(countsA.AnchorObjs,   countsB.AnchorObjs);
    }

    [Fact]
    public void RoundTrip_OfficeStarter_PreservesRoomBoundsById()
    {
        var (emA, emB) = RoundTripWorlds(StarterJson);

        var roomsA = emA.Query<RoomComponent>().Select(e => e.Get<RoomComponent>())
                       .ToDictionary(r => r.Id, StringComparer.Ordinal);
        var roomsB = emB.Query<RoomComponent>().Select(e => e.Get<RoomComponent>())
                       .ToDictionary(r => r.Id, StringComparer.Ordinal);

        Assert.Equal(roomsA.Count, roomsB.Count);
        foreach (var (id, ra) in roomsA)
        {
            Assert.True(roomsB.TryGetValue(id, out var rb), $"Room '{id}' missing after round-trip.");
            Assert.Equal(ra.Bounds.X,      rb.Bounds.X);
            Assert.Equal(ra.Bounds.Y,      rb.Bounds.Y);
            Assert.Equal(ra.Bounds.Width,  rb.Bounds.Width);
            Assert.Equal(ra.Bounds.Height, rb.Bounds.Height);
            Assert.Equal(ra.Floor,         rb.Floor);
            Assert.Equal(ra.Category,      rb.Category);
            Assert.Equal(ra.Name,          rb.Name);
            Assert.Equal(ra.Illumination.AmbientLevel,      rb.Illumination.AmbientLevel);
            Assert.Equal(ra.Illumination.ColorTemperatureK, rb.Illumination.ColorTemperatureK);
        }
    }

    [Fact]
    public void RoundTrip_OfficeStarter_PreservesLightSourceFieldsById()
    {
        var (emA, emB) = RoundTripWorlds(StarterJson);

        var lightsA = emA.Query<LightSourceComponent>().Select(e => e.Get<LightSourceComponent>())
                        .ToDictionary(l => l.Id, StringComparer.Ordinal);
        var lightsB = emB.Query<LightSourceComponent>().Select(e => e.Get<LightSourceComponent>())
                        .ToDictionary(l => l.Id, StringComparer.Ordinal);

        Assert.Equal(lightsA.Count, lightsB.Count);
        foreach (var (id, la) in lightsA)
        {
            Assert.True(lightsB.TryGetValue(id, out var lb), $"Light source '{id}' missing after round-trip.");
            Assert.Equal(la.Kind,              lb.Kind);
            Assert.Equal(la.State,             lb.State);
            Assert.Equal(la.Intensity,         lb.Intensity);
            Assert.Equal(la.ColorTemperatureK, lb.ColorTemperatureK);
            Assert.Equal(la.TileX,             lb.TileX);
            Assert.Equal(la.TileY,             lb.TileY);
            Assert.Equal(la.RoomId,            lb.RoomId);
        }
    }

    [Fact]
    public void RoundTrip_OfficeStarter_PreservesApertureFieldsById()
    {
        var (emA, emB) = RoundTripWorlds(StarterJson);

        var apsA = emA.Query<LightApertureComponent>().Select(e => e.Get<LightApertureComponent>())
                     .ToDictionary(a => a.Id, StringComparer.Ordinal);
        var apsB = emB.Query<LightApertureComponent>().Select(e => e.Get<LightApertureComponent>())
                     .ToDictionary(a => a.Id, StringComparer.Ordinal);

        Assert.Equal(apsA.Count, apsB.Count);
        foreach (var (id, aa) in apsA)
        {
            Assert.True(apsB.TryGetValue(id, out var ab), $"Aperture '{id}' missing after round-trip.");
            Assert.Equal(aa.TileX,       ab.TileX);
            Assert.Equal(aa.TileY,       ab.TileY);
            Assert.Equal(aa.RoomId,      ab.RoomId);
            Assert.Equal(aa.Facing,      ab.Facing);
            Assert.Equal(aa.AreaSqTiles, ab.AreaSqTiles);
        }
    }

    [Fact]
    public void RoundTrip_OfficeStarter_PreservesNpcSlotPositions()
    {
        var (emA, emB) = RoundTripWorlds(StarterJson);

        var slotsA = emA.Query<NpcSlotTag>()
            .Select(e => e.Get<NpcSlotComponent>())
            .OrderBy(s => s.X * 1000 + s.Y).ToList();
        var slotsB = emB.Query<NpcSlotTag>()
            .Select(e => e.Get<NpcSlotComponent>())
            .OrderBy(s => s.X * 1000 + s.Y).ToList();

        Assert.Equal(slotsA.Count, slotsB.Count);
        for (int i = 0; i < slotsA.Count; i++)
        {
            Assert.Equal(slotsA[i].X,             slotsB[i].X);
            Assert.Equal(slotsA[i].Y,             slotsB[i].Y);
            Assert.Equal(slotsA[i].ArchetypeHint, slotsB[i].ArchetypeHint);
        }
    }

    [Fact]
    public void RoundTrip_OfficeStarter_PreservesNamedAnchorsAndNotes()
    {
        var (emA, emB) = RoundTripWorlds(StarterJson);

        var anchorsA = emA.Query<NamedAnchorComponent>()
            .Select(e => e.Get<NamedAnchorComponent>())
            .ToDictionary(a => a.Tag, StringComparer.Ordinal);
        var anchorsB = emB.Query<NamedAnchorComponent>()
            .Select(e => e.Get<NamedAnchorComponent>())
            .ToDictionary(a => a.Tag, StringComparer.Ordinal);

        Assert.Equal(anchorsA.Count, anchorsB.Count);
        foreach (var (tag, a) in anchorsA)
        {
            Assert.True(anchorsB.TryGetValue(tag, out var b));
            Assert.Equal(a.Description, b.Description);
            Assert.Equal(a.SmellTag,    b.SmellTag);
        }

        var notesA = emA.Query<NoteComponent>()
            .Select(e => e.Get<NoteComponent>().Notes?.Count ?? 0).ToList();
        var notesB = emB.Query<NoteComponent>()
            .Select(e => e.Get<NoteComponent>().Notes?.Count ?? 0).ToList();
        Assert.Equal(notesA.Count, notesB.Count);
        Assert.Equal(notesA.Sum(),  notesB.Sum());
    }

    [Fact]
    public void WriteToFile_WritesIndentedReadableJson()
    {
        using var srcTmp = TempFile(StarterJson);
        var em  = new EntityManager();
        var rng = new SeededRandom(7);
        WorldDefinitionLoader.LoadFromFile(srcTmp.Path, em, rng);

        var outPath = Path.GetTempFileName() + ".json";
        try
        {
            WorldDefinitionWriter.WriteToFile(em, outPath, "out", "Out", seed: 7);
            Assert.True(File.Exists(outPath));
            var content = File.ReadAllText(outPath);
            Assert.Contains("\n",   content);    // indented (Pretty) options include newlines
            Assert.Contains("rooms",content);
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private record SectionCounts(int Rooms, int LightSources, int Apertures, int NpcSlots, int AnchorObjs);

    private static (SectionCounts A, SectionCounts B) RoundTrip(string sourceJson)
    {
        var (emA, emB) = RoundTripWorlds(sourceJson);
        return (Counts(emA), Counts(emB));
    }

    private static (EntityManager A, EntityManager B) RoundTripWorlds(string sourceJson)
    {
        // 1. Load source.
        using var srcTmp = TempFile(sourceJson);
        var emA = new EntityManager();
        var rng = new SeededRandom(13);
        WorldDefinitionLoader.LoadFromFile(srcTmp.Path, emA, rng);

        // 2. Write back via the writer.
        var written = WorldDefinitionWriter.WriteToString(emA, "rt", "rt", seed: 13);

        // 3. Load the written JSON into a fresh EM.
        using var rtTmp = TempFile(written);
        var emB = new EntityManager();
        WorldDefinitionLoader.LoadFromFile(rtTmp.Path, emB, new SeededRandom(13));

        return (emA, emB);
    }

    private static SectionCounts Counts(EntityManager em) => new(
        Rooms:        em.Query<RoomComponent>().Count(),
        LightSources: em.Query<LightSourceComponent>().Count(),
        Apertures:    em.Query<LightApertureComponent>().Count(),
        NpcSlots:     em.Query<NpcSlotTag>().Count(),
        AnchorObjs:   em.Query<AnchorObjectComponent>().Count()
    );

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

    private static readonly string StarterJson = LoadStarterJson();

    private static string LoadStarterJson()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(
                dir.FullName, "docs", "c2-content", "world-definitions", "office-starter.json");
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not locate office-starter.json.");
    }
}
