using System;
using System.Linq;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Mutation;

/// <summary>
/// Author-mode extensions to <see cref="IWorldMutationApi"/> (WP-4.0.J).
/// Covers CreateRoom / DespawnRoom (with policy) / CreateLightSource / TuneLightSource /
/// CreateLightAperture / DespawnLight.
/// </summary>
public class AuthorModeMutationTests
{
    private static (EntityManager, StructuralChangeBus, WorldMutationApi) Setup()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var api = new WorldMutationApi(em, bus);
        return (em, bus, api);
    }

    // ── CreateRoom ───────────────────────────────────────────────────────────────

    [Fact]
    public void CreateRoom_SpawnsRoomEntityWithExpectedComponents()
    {
        var (em, _, api) = Setup();
        var bounds = new BoundsRect(5, 5, 10, 8);

        var id = api.CreateRoom(RoomCategory.Office, BuildingFloor.First, bounds);

        var entity = em.GetAllEntities().FirstOrDefault(e => e.Id == id);
        Assert.NotNull(entity);
        Assert.True(entity!.Has<RoomTag>());
        Assert.True(entity.Has<RoomComponent>());
        Assert.True(entity.Has<PositionComponent>());

        var rc = entity.Get<RoomComponent>();
        Assert.Equal(RoomCategory.Office, rc.Category);
        Assert.Equal(BuildingFloor.First, rc.Floor);
        Assert.Equal(bounds, rc.Bounds);
        Assert.False(string.IsNullOrEmpty(rc.Id));
        Assert.False(string.IsNullOrEmpty(rc.Name));
    }

    [Fact]
    public void CreateRoom_AcceptsCustomName()
    {
        var (em, _, api) = Setup();
        var id = api.CreateRoom(RoomCategory.Breakroom, BuildingFloor.First,
            new BoundsRect(0, 0, 5, 5), name: "The Lunch Bunker");
        var rc = em.GetAllEntities().First(e => e.Id == id).Get<RoomComponent>();
        Assert.Equal("The Lunch Bunker", rc.Name);
    }

    [Fact]
    public void CreateRoom_EmitsEntityAddedOnBus()
    {
        var (_, bus, api) = Setup();
        StructuralChangeKind? observedKind = null;
        bus.Subscribe(evt => observedKind = evt.Kind);

        api.CreateRoom(RoomCategory.Hallway, BuildingFloor.First, new BoundsRect(0, 0, 4, 4));

        Assert.Equal(StructuralChangeKind.EntityAdded, observedKind);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    [InlineData(-1, 5)]
    public void CreateRoom_RejectsZeroOrNegativeDimensions(int w, int h)
    {
        var (_, _, api) = Setup();
        Assert.Throws<InvalidOperationException>(() =>
            api.CreateRoom(RoomCategory.Office, BuildingFloor.First, new BoundsRect(0, 0, w, h)));
    }

    // ── DespawnRoom ──────────────────────────────────────────────────────────────

    [Fact]
    public void DespawnRoom_OrphanContents_DeletesOnlyRoom()
    {
        var (em, _, api) = Setup();
        var roomId = api.CreateRoom(RoomCategory.Office, BuildingFloor.First, new BoundsRect(0, 0, 5, 5));
        var roomKey = em.GetAllEntities().First(e => e.Id == roomId).Get<RoomComponent>().Id;

        var lightId = api.CreateLightSource(roomKey, 2, 2, LightKind.DeskLamp, LightState.On, 60, 3800);

        api.DespawnRoom(roomId, RoomDespawnPolicy.OrphanContents);

        Assert.DoesNotContain(em.GetAllEntities(), e => e.Id == roomId);
        Assert.Contains(em.GetAllEntities(), e => e.Id == lightId);     // light orphaned, not deleted
    }

    [Fact]
    public void DespawnRoom_CascadeDelete_RemovesLightsAndApertures()
    {
        var (em, _, api) = Setup();
        var roomId  = api.CreateRoom(RoomCategory.Office, BuildingFloor.First, new BoundsRect(0, 0, 5, 5));
        var roomKey = em.GetAllEntities().First(e => e.Id == roomId).Get<RoomComponent>().Id;

        var lightId = api.CreateLightSource(roomKey, 2, 2, LightKind.DeskLamp, LightState.On, 60, 3800);
        var apId    = api.CreateLightAperture(roomKey, 0, 2, ApertureFacing.North, 3.0);

        api.DespawnRoom(roomId, RoomDespawnPolicy.CascadeDelete);

        Assert.DoesNotContain(em.GetAllEntities(), e => e.Id == roomId);
        Assert.DoesNotContain(em.GetAllEntities(), e => e.Id == lightId);
        Assert.DoesNotContain(em.GetAllEntities(), e => e.Id == apId);
    }

    [Fact]
    public void DespawnRoom_CascadeDelete_LeavesContentsInOtherRoomsAlone()
    {
        var (em, _, api) = Setup();
        var roomA  = api.CreateRoom(RoomCategory.Office,    BuildingFloor.First, new BoundsRect(0, 0, 5, 5));
        var roomB  = api.CreateRoom(RoomCategory.Breakroom, BuildingFloor.First, new BoundsRect(10, 0, 5, 5));
        var keyA   = em.GetAllEntities().First(e => e.Id == roomA).Get<RoomComponent>().Id;
        var keyB   = em.GetAllEntities().First(e => e.Id == roomB).Get<RoomComponent>().Id;
        var lightInB = api.CreateLightSource(keyB, 12, 2, LightKind.OverheadFluorescent, LightState.On, 70, 4000);

        api.DespawnRoom(roomA, RoomDespawnPolicy.CascadeDelete);

        Assert.Contains(em.GetAllEntities(), e => e.Id == roomB);
        Assert.Contains(em.GetAllEntities(), e => e.Id == lightInB);
    }

    [Fact]
    public void DespawnRoom_UnknownId_Throws()
    {
        var (_, _, api) = Setup();
        Assert.Throws<InvalidOperationException>(() => api.DespawnRoom(Guid.NewGuid(), RoomDespawnPolicy.OrphanContents));
    }

    // ── CreateLightSource ────────────────────────────────────────────────────────

    [Fact]
    public void CreateLightSource_SpawnsWithExpectedComponents()
    {
        var (em, _, api) = Setup();
        var id = api.CreateLightSource("room-1", 5, 7, LightKind.DeskLamp, LightState.On, 65, 3800);

        var entity = em.GetAllEntities().First(e => e.Id == id);
        Assert.True(entity.Has<LightSourceTag>());
        Assert.True(entity.Has<LightSourceComponent>());

        var c = entity.Get<LightSourceComponent>();
        Assert.Equal("room-1",          c.RoomId);
        Assert.Equal(5,                 c.TileX);
        Assert.Equal(7,                 c.TileY);
        Assert.Equal(LightKind.DeskLamp,c.Kind);
        Assert.Equal(LightState.On,     c.State);
        Assert.Equal(65,                c.Intensity);
        Assert.Equal(3800,              c.ColorTemperatureK);
    }

    [Theory]
    [InlineData(-1, 4000)]
    [InlineData(101, 4000)]
    [InlineData(50, 999)]
    [InlineData(50, 10001)]
    public void CreateLightSource_RejectsOutOfRangeIntensityOrTemperature(int intensity, int temp)
    {
        var (_, _, api) = Setup();
        Assert.Throws<InvalidOperationException>(() =>
            api.CreateLightSource("r", 0, 0, LightKind.DeskLamp, LightState.On, intensity, temp));
    }

    [Fact]
    public void CreateLightSource_RejectsEmptyRoomId()
    {
        var (_, _, api) = Setup();
        Assert.Throws<InvalidOperationException>(() =>
            api.CreateLightSource("", 0, 0, LightKind.DeskLamp, LightState.On, 50, 4000));
    }

    // ── TuneLightSource ──────────────────────────────────────────────────────────

    [Fact]
    public void TuneLightSource_MutatesStateIntensityAndTemperatureInPlace()
    {
        var (em, _, api) = Setup();
        var id = api.CreateLightSource("r", 0, 0, LightKind.DeskLamp, LightState.On, 50, 3800);

        api.TuneLightSource(id, LightState.Flickering, 30, 4500);

        var c = em.GetAllEntities().First(e => e.Id == id).Get<LightSourceComponent>();
        Assert.Equal(LightState.Flickering, c.State);
        Assert.Equal(30,                    c.Intensity);
        Assert.Equal(4500,                  c.ColorTemperatureK);
        // unchanged fields preserved
        Assert.Equal(LightKind.DeskLamp,    c.Kind);
        Assert.Equal("r",                   c.RoomId);
    }

    [Fact]
    public void TuneLightSource_NonLightEntity_Throws()
    {
        var (em, _, api) = Setup();
        var id = api.CreateRoom(RoomCategory.Office, BuildingFloor.First, new BoundsRect(0, 0, 3, 3));
        Assert.Throws<InvalidOperationException>(() =>
            api.TuneLightSource(id, LightState.Off, 50, 4000));
    }

    // ── CreateLightAperture ──────────────────────────────────────────────────────

    [Fact]
    public void CreateLightAperture_SpawnsWithExpectedComponents()
    {
        var (em, _, api) = Setup();
        var id = api.CreateLightAperture("room-1", 4, 0, ApertureFacing.North, 5.0);

        var entity = em.GetAllEntities().First(e => e.Id == id);
        Assert.True(entity.Has<LightApertureTag>());
        Assert.True(entity.Has<LightApertureComponent>());

        var c = entity.Get<LightApertureComponent>();
        Assert.Equal("room-1",            c.RoomId);
        Assert.Equal(4,                   c.TileX);
        Assert.Equal(0,                   c.TileY);
        Assert.Equal(ApertureFacing.North,c.Facing);
        Assert.Equal(5.0,                 c.AreaSqTiles);
    }

    [Theory]
    [InlineData(0.4)]
    [InlineData(64.1)]
    public void CreateLightAperture_RejectsOutOfRangeArea(double area)
    {
        var (_, _, api) = Setup();
        Assert.Throws<InvalidOperationException>(() =>
            api.CreateLightAperture("r", 0, 0, ApertureFacing.North, area));
    }

    // ── DespawnLight ─────────────────────────────────────────────────────────────

    [Fact]
    public void DespawnLight_RemovesLightSource()
    {
        var (em, _, api) = Setup();
        var id = api.CreateLightSource("r", 0, 0, LightKind.DeskLamp, LightState.On, 50, 4000);

        api.DespawnLight(id);

        Assert.DoesNotContain(em.GetAllEntities(), e => e.Id == id);
    }

    [Fact]
    public void DespawnLight_RemovesLightAperture()
    {
        var (em, _, api) = Setup();
        var id = api.CreateLightAperture("r", 0, 0, ApertureFacing.North, 3.0);

        api.DespawnLight(id);

        Assert.DoesNotContain(em.GetAllEntities(), e => e.Id == id);
    }

    [Fact]
    public void DespawnLight_NonLightEntity_Throws()
    {
        var (_, _, api) = Setup();
        var id = api.CreateRoom(RoomCategory.Office, BuildingFloor.First, new BoundsRect(0, 0, 3, 3));
        Assert.Throws<InvalidOperationException>(() => api.DespawnLight(id));
    }

    // ── Round-trip with WorldDefinitionWriter (cross-packet integration) ────────

    [Fact]
    public void AuthoredEntities_RoundTripThroughWorldDefinitionWriter()
    {
        var (em, _, api) = Setup();
        var roomId = api.CreateRoom(RoomCategory.Office, BuildingFloor.First, new BoundsRect(0, 0, 6, 6));
        var roomKey = em.GetAllEntities().First(e => e.Id == roomId).Get<RoomComponent>().Id;
        api.CreateLightSource(roomKey, 3, 3, LightKind.DeskLamp, LightState.On, 60, 3800);
        api.CreateLightAperture(roomKey, 0, 3, ApertureFacing.West, 4.0);

        var json = APIFramework.Bootstrap.WorldDefinitionWriter.WriteToString(em, "rt", "rt", 1);

        Assert.Contains("\"office\"",                json);
        Assert.Contains("\"deskLamp\"",              json);
        Assert.Contains("\"west\"",                  json);
        Assert.Contains(roomKey,                     json);
    }
}
