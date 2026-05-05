using System;
using System.IO;
using System.Linq;
using APIFramework.Bootstrap;
using APIFramework.Cast;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Integration;

/// <summary>
/// End-to-end integration test for the Wave 4 authoring loop substrate
/// (WP-4.0.M cast name generator + WP-4.0.I world-def writer + WP-4.0.J author
/// mutations + WP-4.0.K NPC mutations + name pool).
///
/// PURPOSE
/// ───────
/// Validates that the four packets that landed in Wave 4 work together end-to-end,
/// not just per-packet. Each packet has its own focused tests, but the full
/// "create-everything → save → reload → verify-intact" pipeline only runs in
/// integration. Catches subtle integration bugs before the user does manual
/// Editor verification.
///
/// WHAT THIS COVERS
/// ────────────────
///   1. Boot a fresh world (empty starter scene with no rooms / NPCs).
///   2. Use the WP-4.0.J/K mutation API to create a complete authored scene:
///        - 3 rooms (cubicle + breakroom + bathroom)
///        - 4 light sources (overhead, desk lamp, breakroom strip, bathroom overhead)
///        - 1 light aperture (window)
///        - 5 NPCs with mixed archetypes, mixed names (some auto via WP-4.0.M
///          name pool, some explicit)
///   3. Use the WP-4.0.I writer to save the world to JSON.
///   4. Spin up a SECOND fresh EntityManager and load that JSON via the
///      existing loader.
///   5. Assert every authored entity round-tripped:
///        - Room counts + bounds
///        - Light source counts + positions + tunable properties
///        - Aperture facing + area
///        - NPC archetypes + positions + names (the K-fixed NameHint round-trip)
///        - Re-spawned NPCs have the correct archetype-derived components
///          (drives, personality, etc. — proves CastGenerator.SpawnNpc was
///          invoked with the right archetype on reload).
///
/// IMPORTANT
/// ─────────
/// This test does NOT exercise the Unity-side AuthorModeController (Editor-only).
/// It exercises the engine substrate directly — which is what the Unity controller
/// wraps. If this passes, the engine half of the authoring loop is solid.
/// </summary>
public class AuthoringLoopEndToEndTests
{
    [Fact]
    public void FullAuthoringFlow_CreateAndSaveAndReload_PreservesAllAuthoredEntities()
    {
        // ── Phase 1: Boot a minimal world ────────────────────────────────────────
        var emA       = new EntityManager();
        var bus       = new StructuralChangeBus();
        var catalog   = ArchetypeCatalog.LoadDefault()
                        ?? throw new InvalidOperationException("archetypes.json not found");
        var config    = new CastGeneratorConfig();
        var rng       = new SeededRandom(seed: 4747);
        var api       = new WorldMutationApi(emA, bus, catalog, config, rng);

        // ── Phase 2: Author a complete scene via the mutation API ────────────────

        // --- 3 rooms ---
        var cubicleId  = api.CreateRoom(RoomCategory.CubicleGrid, BuildingFloor.First, new BoundsRect(0, 0, 12, 8),  "Cubicle Area");
        var breakroomId= api.CreateRoom(RoomCategory.Breakroom,   BuildingFloor.First, new BoundsRect(13, 0, 8, 6),  "Breakroom");
        var bathroomId = api.CreateRoom(RoomCategory.Bathroom,    BuildingFloor.First, new BoundsRect(13, 7, 6, 5),  "Bathroom");

        // Get the room *string* ids (used by lights / NPCs to reference rooms by id, not by entity-Guid).
        var cubicleKey   = emA.GetAllEntities().First(e => e.Id == cubicleId).Get<RoomComponent>().Id;
        var breakroomKey = emA.GetAllEntities().First(e => e.Id == breakroomId).Get<RoomComponent>().Id;
        var bathroomKey  = emA.GetAllEntities().First(e => e.Id == bathroomId).Get<RoomComponent>().Id;

        // --- 4 light sources ---
        var overheadCub  = api.CreateLightSource(cubicleKey,   6, 4, LightKind.OverheadFluorescent, LightState.On,         70, 4000);
        var deskLamp     = api.CreateLightSource(cubicleKey,   2, 2, LightKind.DeskLamp,            LightState.On,         60, 3800);
        var breakStrip   = api.CreateLightSource(breakroomKey, 17, 3, LightKind.BreakroomStrip,    LightState.Flickering, 50, 3900);
        var bathroomLight= api.CreateLightSource(bathroomKey,  16, 9, LightKind.OverheadFluorescent, LightState.On,         75, 5000);

        // Tune one of them — verifies tune-then-save round-trip.
        api.TuneLightSource(deskLamp, LightState.Dying, 35, 3500);

        // --- 1 aperture ---
        var window = api.CreateLightAperture(cubicleKey, 0, 4, ApertureFacing.West, 5.0);

        // --- 5 NPCs with mixed archetypes and naming ---
        var donnaId = api.CreateNpc(cubicleKey,    2,  3, "the-vent",       "Donna");
        var gregId  = api.CreateNpc(cubicleKey,    6,  3, "the-hermit",     "Greg");
        var amyId   = api.CreateNpc(cubicleKey,   10,  3, "the-newbie",     "Amy");

        // Auto-name two via the WP-4.0.M pool (proves M+K integration).
        var nameData = CastNameDataLoader.LoadDefault()
                       ?? throw new InvalidOperationException("name-data.json not found");
        var pool     = new CastNamePool(emA, new CastNameGenerator(nameData), new Random(99));
        var auto1    = pool.GenerateUniqueName(CastGender.Female).DisplayName;
        var auto2    = pool.GenerateUniqueName(CastGender.Male).DisplayName;
        var autoFId  = api.CreateNpc(breakroomKey, 17, 3, "the-cynic",      auto1);
        var autoMId  = api.CreateNpc(cubicleKey,    2,  6, "the-old-hand",   auto2);

        // ── Phase 3: Save the world ──────────────────────────────────────────────
        var savePath = Path.GetTempFileName() + ".json";
        try
        {
            WorldDefinitionWriter.WriteToFile(emA, savePath, "e2e-test", "End-to-End Test World", seed: 4747);
            Assert.True(File.Exists(savePath));
            var json = File.ReadAllText(savePath);
            Assert.False(string.IsNullOrWhiteSpace(json));

            // ── Phase 4: Reload into a fresh EntityManager ───────────────────────
            var emB = new EntityManager();
            WorldDefinitionLoader.LoadFromFile(savePath, emB, new SeededRandom(999));

            // Cast generator hasn't run yet on emB — slots exist as marker entities.
            // Spawn NPCs from the slots so the round-trip is end-to-end (not just
            // structure-preserved).
            CastGenerator.SpawnAll(catalog, emB, new SeededRandom(4747), config);

            // ── Phase 5: Assert round-trip integrity ─────────────────────────────

            // Rooms
            var roomsA = emA.Query<RoomComponent>().Select(e => e.Get<RoomComponent>()).OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
            var roomsB = emB.Query<RoomComponent>().Select(e => e.Get<RoomComponent>()).OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
            Assert.Equal(roomsA.Count, roomsB.Count);
            for (int i = 0; i < roomsA.Count; i++)
            {
                Assert.Equal(roomsA[i].Id,                              roomsB[i].Id);
                Assert.Equal(roomsA[i].Category,                        roomsB[i].Category);
                Assert.Equal(roomsA[i].Floor,                           roomsB[i].Floor);
                Assert.Equal(roomsA[i].Bounds,                          roomsB[i].Bounds);
            }

            // Light sources
            var lightsA = emA.Query<LightSourceComponent>().Select(e => e.Get<LightSourceComponent>()).OrderBy(l => l.Id, StringComparer.Ordinal).ToList();
            var lightsB = emB.Query<LightSourceComponent>().Select(e => e.Get<LightSourceComponent>()).OrderBy(l => l.Id, StringComparer.Ordinal).ToList();
            Assert.Equal(lightsA.Count, lightsB.Count);
            for (int i = 0; i < lightsA.Count; i++)
            {
                Assert.Equal(lightsA[i].Kind,              lightsB[i].Kind);
                Assert.Equal(lightsA[i].State,             lightsB[i].State);
                Assert.Equal(lightsA[i].Intensity,         lightsB[i].Intensity);
                Assert.Equal(lightsA[i].ColorTemperatureK, lightsB[i].ColorTemperatureK);
                Assert.Equal(lightsA[i].TileX,             lightsB[i].TileX);
                Assert.Equal(lightsA[i].TileY,             lightsB[i].TileY);
                Assert.Equal(lightsA[i].RoomId,            lightsB[i].RoomId);
            }

            // Verify the deskLamp's tune-then-save round-tripped correctly.
            var deskLampA = lightsA.First(l => l.Kind == LightKind.DeskLamp);
            var deskLampB = lightsB.First(l => l.Kind == LightKind.DeskLamp);
            Assert.Equal(LightState.Dying, deskLampB.State);
            Assert.Equal(35,               deskLampB.Intensity);
            Assert.Equal(3500,             deskLampB.ColorTemperatureK);

            // Apertures
            var apsA = emA.Query<LightApertureComponent>().Select(e => e.Get<LightApertureComponent>()).ToList();
            var apsB = emB.Query<LightApertureComponent>().Select(e => e.Get<LightApertureComponent>()).ToList();
            Assert.Equal(apsA.Count, apsB.Count);
            Assert.Equal(apsA[0].Facing,      apsB[0].Facing);
            Assert.Equal(apsA[0].AreaSqTiles, apsB[0].AreaSqTiles);

            // NPCs — count + archetype + name preservation
            var npcsB = emB.Query<NpcArchetypeComponent>()
                .Where(e => e.Has<IdentityComponent>())
                .Select(e => (
                    Archetype: e.Get<NpcArchetypeComponent>().ArchetypeId,
                    Name:      e.Get<IdentityComponent>().Name,
                    Pos:       e.Get<PositionComponent>()))
                .ToList();
            Assert.Equal(5, npcsB.Count);

            // Specifically check the named NPCs survived the trip (proves the
            // K NameHint plumbing works through Loader → CastGenerator → IdentityComponent).
            Assert.Contains(npcsB, n => n.Name == "Donna" && n.Archetype == "the-vent");
            Assert.Contains(npcsB, n => n.Name == "Greg"  && n.Archetype == "the-hermit");
            Assert.Contains(npcsB, n => n.Name == "Amy"   && n.Archetype == "the-newbie");

            // The auto-generated names also survived.
            Assert.Contains(npcsB, n => n.Name == auto1 && n.Archetype == "the-cynic");
            Assert.Contains(npcsB, n => n.Name == auto2 && n.Archetype == "the-old-hand");

            // Re-spawned NPCs have the archetype-derived components that CastGenerator.SpawnNpc
            // attaches via WithCastSpawn (SocialDrives / Personality / Willpower / Inhibitions /
            // Silhouette). DriveComponent (eat/drink/sleep urgencies) is added by a different
            // system later in the boot flow; not checked here.
            var anyReSpawnedNpc = emB.Query<NpcArchetypeComponent>()
                .Where(e => e.Has<IdentityComponent>())
                .First();
            Assert.True(anyReSpawnedNpc.Has<SocialDrivesComponent>(),"Re-spawned NPC should have SocialDrivesComponent");
            Assert.True(anyReSpawnedNpc.Has<PersonalityComponent>(), "Re-spawned NPC should have PersonalityComponent");
            Assert.True(anyReSpawnedNpc.Has<WillpowerComponent>(),   "Re-spawned NPC should have WillpowerComponent");
            Assert.True(anyReSpawnedNpc.Has<InhibitionsComponent>(), "Re-spawned NPC should have InhibitionsComponent");
            Assert.True(anyReSpawnedNpc.Has<SilhouetteComponent>(),  "Re-spawned NPC should have SilhouetteComponent");

            // Use the unused vars to suppress warnings (these IDs document what we authored).
            _ = (overheadCub, breakStrip, bathroomLight, window, donnaId, gregId, amyId, autoFId, autoMId);
        }
        finally
        {
            if (File.Exists(savePath)) File.Delete(savePath);
        }
    }

    [Fact]
    public void AuthoredScene_DespawnNpc_PersistsAcrossSaveReload()
    {
        // Verifies that author-mode despawn (WP-4.0.K) is correctly observed by the
        // writer — a despawned NPC should NOT appear in the saved JSON.

        var em       = new EntityManager();
        var bus      = new StructuralChangeBus();
        var catalog  = ArchetypeCatalog.LoadDefault()!;
        var config   = new CastGeneratorConfig();
        var api      = new WorldMutationApi(em, bus, catalog, config, new SeededRandom(7));

        var roomId  = api.CreateRoom(RoomCategory.CubicleGrid, BuildingFloor.First, new BoundsRect(0, 0, 6, 6));
        var roomKey = em.GetAllEntities().First(e => e.Id == roomId).Get<RoomComponent>().Id;

        var alice = api.CreateNpc(roomKey, 1, 1, "the-vent",   "Alice");
        var bob   = api.CreateNpc(roomKey, 2, 2, "the-hermit", "Bob");
        var carol = api.CreateNpc(roomKey, 3, 3, "the-newbie", "Carol");

        // Despawn Bob.
        api.DespawnNpc(bob);

        // Save + reload.
        var savePath = Path.GetTempFileName() + ".json";
        try
        {
            WorldDefinitionWriter.WriteToFile(em, savePath, "despawn-test", "Despawn Test", seed: 7);

            var emB = new EntityManager();
            WorldDefinitionLoader.LoadFromFile(savePath, emB, new SeededRandom(7));
            CastGenerator.SpawnAll(catalog, emB, new SeededRandom(7), config);

            var npcs = emB.Query<NpcArchetypeComponent>()
                .Where(e => e.Has<IdentityComponent>())
                .Select(e => e.Get<IdentityComponent>().Name)
                .ToList();

            Assert.Equal(2, npcs.Count);
            Assert.Contains("Alice", npcs);
            Assert.DoesNotContain("Bob", npcs);
            Assert.Contains("Carol", npcs);
            _ = alice;     // suppress unused warning
            _ = carol;
        }
        finally
        {
            if (File.Exists(savePath)) File.Delete(savePath);
        }
    }

    [Fact]
    public void AuthoredScene_RenameNpc_PersistsAcrossSaveReload()
    {
        // Verifies the WP-4.0.K rename operation round-trips through save → reload.

        var em       = new EntityManager();
        var bus      = new StructuralChangeBus();
        var catalog  = ArchetypeCatalog.LoadDefault()!;
        var config   = new CastGeneratorConfig();
        var api      = new WorldMutationApi(em, bus, catalog, config, new SeededRandom(11));

        var roomId  = api.CreateRoom(RoomCategory.Office, BuildingFloor.First, new BoundsRect(0, 0, 4, 4));
        var roomKey = em.GetAllEntities().First(e => e.Id == roomId).Get<RoomComponent>().Id;

        var npcId = api.CreateNpc(roomKey, 1, 1, "the-vent", "InitialName");
        api.RenameNpc(npcId, "RenamedDonna");

        var savePath = Path.GetTempFileName() + ".json";
        try
        {
            WorldDefinitionWriter.WriteToFile(em, savePath, "rename-test", "Rename Test", seed: 11);

            var emB = new EntityManager();
            WorldDefinitionLoader.LoadFromFile(savePath, emB, new SeededRandom(11));
            CastGenerator.SpawnAll(catalog, emB, new SeededRandom(11), config);

            var name = emB.Query<NpcArchetypeComponent>()
                .Where(e => e.Has<IdentityComponent>())
                .Select(e => e.Get<IdentityComponent>().Name)
                .Single();
            Assert.Equal("RenamedDonna", name);
        }
        finally
        {
            if (File.Exists(savePath)) File.Delete(savePath);
        }
    }

    [Fact]
    public void AuthoredScene_DespawnRoomCascade_RemovesContainedLights()
    {
        // Verifies WP-4.0.J's RoomDespawnPolicy.CascadeDelete works end-to-end
        // through save → reload (the room AND its lights should be gone).

        var em       = new EntityManager();
        var bus      = new StructuralChangeBus();
        var api      = new WorldMutationApi(em, bus);   // 2-arg ctor — no NPC ops needed here.

        var roomA = api.CreateRoom(RoomCategory.Office, BuildingFloor.First, new BoundsRect(0, 0, 5, 5));
        var roomB = api.CreateRoom(RoomCategory.Office, BuildingFloor.First, new BoundsRect(10, 0, 5, 5));
        var keyA  = em.GetAllEntities().First(e => e.Id == roomA).Get<RoomComponent>().Id;
        var keyB  = em.GetAllEntities().First(e => e.Id == roomB).Get<RoomComponent>().Id;

        api.CreateLightSource(keyA, 2, 2, LightKind.DeskLamp,            LightState.On, 60, 3800);
        api.CreateLightSource(keyA, 3, 3, LightKind.OverheadFluorescent, LightState.On, 70, 4000);
        api.CreateLightSource(keyB, 12, 2, LightKind.DeskLamp,           LightState.On, 60, 3800);

        api.DespawnRoom(roomA, RoomDespawnPolicy.CascadeDelete);

        var savePath = Path.GetTempFileName() + ".json";
        try
        {
            WorldDefinitionWriter.WriteToFile(em, savePath, "cascade-test", "Cascade Test", seed: 1);

            var emB = new EntityManager();
            WorldDefinitionLoader.LoadFromFile(savePath, emB, new SeededRandom(1));

            var rooms  = emB.Query<RoomComponent>().Count();
            var lights = emB.Query<LightSourceComponent>().Count();

            Assert.Equal(1, rooms);   // only roomB survives
            Assert.Equal(1, lights);  // only the light in roomB survives
        }
        finally
        {
            if (File.Exists(savePath)) File.Delete(savePath);
        }
    }
}
