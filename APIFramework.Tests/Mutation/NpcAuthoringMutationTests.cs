using System;
using System.Linq;
using APIFramework.Bootstrap;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Mutation;

/// <summary>
/// WP-4.0.K — NPC authoring extensions to <see cref="IWorldMutationApi"/>:
/// CreateNpc / DespawnNpc / RenameNpc.
/// </summary>
public class NpcAuthoringMutationTests
{
    private static readonly CastGeneratorConfig Cfg = new();

    private static ArchetypeCatalog LoadCatalog()
        => ArchetypeCatalog.LoadDefault()
           ?? throw new InvalidOperationException("Could not locate archetypes.json.");

    private static (EntityManager em, WorldMutationApi api) AuthoringSetup(int seed = 13)
    {
        var em       = new EntityManager();
        var bus      = new StructuralChangeBus();
        var catalog  = LoadCatalog();
        var rng      = new SeededRandom(seed);
        var api      = new WorldMutationApi(em, bus, catalog, Cfg, rng);
        return (em, api);
    }

    private static (EntityManager em, WorldMutationApi api) MinimalSetup()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);
        return (em, api);
    }

    // ── CreateNpc ────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateNpc_SpawnsNpcWithExpectedComponents()
    {
        var (em, api) = AuthoringSetup();

        var id = api.CreateNpc("room-1", 5, 7, "the-vent", "Donna");

        var entity = em.GetAllEntities().First(e => e.Id == id);
        Assert.True(entity.Has<NpcArchetypeComponent>());
        Assert.True(entity.Has<IdentityComponent>());
        Assert.True(entity.Has<PositionComponent>());

        var arch = entity.Get<NpcArchetypeComponent>();
        Assert.Equal("the-vent", arch.ArchetypeId);

        var name = entity.Get<IdentityComponent>().Name;
        Assert.Equal("Donna", name);

        var pos = entity.Get<PositionComponent>();
        Assert.Equal(5, (int)pos.X);
        Assert.Equal(7, (int)pos.Z);
    }

    [Fact]
    public void CreateNpc_UnknownArchetype_Throws()
    {
        var (_, api) = AuthoringSetup();
        var ex = Assert.Throws<InvalidOperationException>(
            () => api.CreateNpc("r", 0, 0, "the-imaginary", "Xerxes"));
        Assert.Contains("the-imaginary", ex.Message);
    }

    [Fact]
    public void CreateNpc_EmptyArchetype_Throws()
    {
        var (_, api) = AuthoringSetup();
        Assert.Throws<InvalidOperationException>(
            () => api.CreateNpc("r", 0, 0, "", "X"));
    }

    [Fact]
    public void CreateNpc_WithoutCastDeps_Throws()
    {
        var (_, api) = MinimalSetup();
        var ex = Assert.Throws<InvalidOperationException>(
            () => api.CreateNpc("r", 0, 0, "the-vent", "Donna"));
        Assert.Contains("requires", ex.Message);
    }

    [Fact]
    public void CreateNpc_AppliesArchetypeBehavior_PersonalityDrawnFromRanges()
    {
        var (em, api) = AuthoringSetup(seed: 42);
        var id = api.CreateNpc("r", 0, 0, "the-vent", "Donna");

        var entity = em.GetAllEntities().First(e => e.Id == id);
        Assert.True(entity.Has<PersonalityComponent>());
        Assert.True(entity.Has<SocialDrivesComponent>());
        Assert.True(entity.Has<WillpowerComponent>());
        Assert.True(entity.Has<InhibitionsComponent>());
        Assert.True(entity.Has<SilhouetteComponent>());
    }

    [Fact]
    public void CreateNpc_DifferentArchetypes_ProduceStructurallyDifferentNpcs()
    {
        var (em, api) = AuthoringSetup(seed: 100);
        var ventId   = api.CreateNpc("r", 0, 0, "the-vent",   "A");
        var hermitId = api.CreateNpc("r", 1, 1, "the-hermit", "B");

        var vent   = em.GetAllEntities().First(e => e.Id == ventId);
        var hermit = em.GetAllEntities().First(e => e.Id == hermitId);

        Assert.Equal("the-vent",   vent.Get<NpcArchetypeComponent>().ArchetypeId);
        Assert.Equal("the-hermit", hermit.Get<NpcArchetypeComponent>().ArchetypeId);
    }

    [Fact]
    public void CreateNpc_EmptyName_LeavesNoIdentityComponent()
    {
        var (em, api) = AuthoringSetup();
        var id = api.CreateNpc("r", 0, 0, "the-vent", "");
        var entity = em.GetAllEntities().First(e => e.Id == id);
        Assert.False(entity.Has<IdentityComponent>());
    }

    // ── DespawnNpc ───────────────────────────────────────────────────────────────

    [Fact]
    public void DespawnNpc_RemovesEntity()
    {
        var (em, api) = AuthoringSetup();
        var id = api.CreateNpc("r", 0, 0, "the-vent", "Donna");

        api.DespawnNpc(id);

        Assert.DoesNotContain(em.GetAllEntities(), e => e.Id == id);
    }

    [Fact]
    public void DespawnNpc_NonNpcEntity_Throws()
    {
        var (em, api) = AuthoringSetup();
        var roomId = api.CreateRoom(RoomCategory.Office, BuildingFloor.First, new BoundsRect(0, 0, 4, 4));
        Assert.Throws<InvalidOperationException>(() => api.DespawnNpc(roomId));
    }

    [Fact]
    public void DespawnNpc_UnknownId_Throws()
    {
        var (_, api) = AuthoringSetup();
        Assert.Throws<InvalidOperationException>(() => api.DespawnNpc(Guid.NewGuid()));
    }

    // ── RenameNpc ────────────────────────────────────────────────────────────────

    [Fact]
    public void RenameNpc_UpdatesIdentityName()
    {
        var (em, api) = AuthoringSetup();
        var id = api.CreateNpc("r", 0, 0, "the-vent", "Donna");

        api.RenameNpc(id, "Karen");

        var name = em.GetAllEntities().First(e => e.Id == id).Get<IdentityComponent>().Name;
        Assert.Equal("Karen", name);
    }

    [Fact]
    public void RenameNpc_PreservesValueField()
    {
        var (em, api) = AuthoringSetup();
        var id = api.CreateNpc("r", 0, 0, "the-vent", "Donna");
        var entity = em.GetAllEntities().First(e => e.Id == id);
        // Set a value field through Add (replaces component, value defaults to "" but we set it)
        entity.Add(new IdentityComponent("Donna", "the-vent-original"));

        api.RenameNpc(id, "Karen");

        var ic = entity.Get<IdentityComponent>();
        Assert.Equal("Karen",              ic.Name);
        Assert.Equal("the-vent-original",  ic.Value);
    }

    [Fact]
    public void RenameNpc_EntityWithoutIdentity_Throws()
    {
        var (_, api) = AuthoringSetup();
        var roomId = api.CreateRoom(RoomCategory.Office, BuildingFloor.First, new BoundsRect(0, 0, 4, 4));
        Assert.Throws<InvalidOperationException>(() => api.RenameNpc(roomId, "X"));
    }

    // ── Round-trip: Create authored NPCs, serialise via WP-4.0.I writer ─────────

    [Fact]
    public void AuthoredNpcs_RoundTripThroughWorldDefinitionWriter()
    {
        var (em, api) = AuthoringSetup(seed: 17);

        api.CreateNpc("cubicle-area", 5, 5, "the-vent",     "Donna");
        api.CreateNpc("cubicle-area", 7, 5, "the-hermit",   "Greg");
        api.CreateNpc("cubicle-area", 9, 5, "the-newbie",   "Amy");

        var json = APIFramework.Bootstrap.WorldDefinitionWriter.WriteToString(em, "auth", "auth", 17);

        Assert.Contains("the-vent",   json);
        Assert.Contains("the-hermit", json);
        Assert.Contains("the-newbie", json);
        Assert.Contains("Donna",      json);
        Assert.Contains("Greg",       json);
        Assert.Contains("Amy",        json);
    }
}
