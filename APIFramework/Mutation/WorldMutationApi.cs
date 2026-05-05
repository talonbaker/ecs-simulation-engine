using System;
using System.Linq;
using APIFramework.Bootstrap;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Spatial;

namespace APIFramework.Mutation;

/// <summary>
/// Default implementation of IWorldMutationApi.
/// Every method validates inputs, applies the mutation through the entity manager,
/// and emits the corresponding StructuralChangeEvent on the bus.
/// </summary>
public sealed class WorldMutationApi : IWorldMutationApi
{
    private readonly EntityManager      _em;
    private readonly StructuralChangeBus _bus;
    private long _seq;

    // NPC-creation deps (WP-4.0.K) — null disables CreateNpc.
    private readonly ArchetypeCatalog?    _archetypeCatalog;
    private readonly CastGeneratorConfig? _castConfig;
    private readonly SeededRandom?        _castRng;

    public WorldMutationApi(EntityManager em, StructuralChangeBus bus)
    {
        _em  = em;
        _bus = bus;
    }

    /// <summary>
    /// Constructor with NPC-creation enabled. Pass the archetype catalog, cast-generator
    /// config, and a seeded RNG for runtime NPC spawning via <see cref="CreateNpc"/>.
    /// </summary>
    public WorldMutationApi(
        EntityManager       em,
        StructuralChangeBus bus,
        ArchetypeCatalog    archetypeCatalog,
        CastGeneratorConfig castConfig,
        SeededRandom        castRng)
        : this(em, bus)
    {
        _archetypeCatalog = archetypeCatalog ?? throw new ArgumentNullException(nameof(archetypeCatalog));
        _castConfig       = castConfig       ?? throw new ArgumentNullException(nameof(castConfig));
        _castRng          = castRng          ?? throw new ArgumentNullException(nameof(castRng));
    }

    /// <inheritdoc/>
    public void MoveEntity(Guid entityId, int newTileX, int newTileY)
    {
        var entity = FindById(entityId)
            ?? throw new InvalidOperationException($"Entity {entityId} not found.");

        if (!entity.Has<MutableTopologyTag>())
            throw new InvalidOperationException(
                $"Entity {entityId} does not have MutableTopologyTag and cannot be moved via IWorldMutationApi.");

        int prevX = 0, prevY = 0;
        if (entity.Has<PositionComponent>())
        {
            var pos = entity.Get<PositionComponent>();
            prevX = (int)Math.Round(pos.X);
            prevY = (int)Math.Round(pos.Z);
        }

        entity.Add(new PositionComponent { X = newTileX, Y = 0f, Z = newTileY });

        _bus.Emit(StructuralChangeKind.EntityMoved, entityId,
            prevX, prevY, newTileX, newTileY, Guid.Empty, ++_seq);
    }

    /// <inheritdoc/>
    public Guid SpawnStructural(int tileX, int tileY)
    {
        var entity = _em.CreateEntity();
        entity.Add(default(StructuralTag));
        entity.Add(default(MutableTopologyTag));
        entity.Add(default(ObstacleTag));
        entity.Add(new PositionComponent { X = tileX, Y = 0f, Z = tileY });

        _bus.Emit(StructuralChangeKind.EntityAdded, entity.Id,
            tileX, tileY, tileX, tileY, Guid.Empty, ++_seq);

        return entity.Id;
    }

    /// <inheritdoc/>
    public void DespawnStructural(Guid entityId)
    {
        var entity = FindById(entityId)
            ?? throw new InvalidOperationException($"Entity {entityId} not found.");

        int tileX = 0, tileY = 0;
        if (entity.Has<PositionComponent>())
        {
            var pos = entity.Get<PositionComponent>();
            tileX = (int)Math.Round(pos.X);
            tileY = (int)Math.Round(pos.Z);
        }

        // Emit before destruction so subscribers can read entity state if needed
        _bus.Emit(StructuralChangeKind.EntityRemoved, entityId,
            tileX, tileY, tileX, tileY, Guid.Empty, ++_seq);

        _em.DestroyEntity(entity);
    }

    /// <inheritdoc/>
    public void AttachObstacle(Guid entityId)
    {
        var entity = FindById(entityId)
            ?? throw new InvalidOperationException($"Entity {entityId} not found.");

        if (!entity.Has<ObstacleTag>())
            entity.Add(default(ObstacleTag));
        if (!entity.Has<StructuralTag>())
            entity.Add(default(StructuralTag));

        int tileX = 0, tileY = 0;
        if (entity.Has<PositionComponent>())
        {
            var pos = entity.Get<PositionComponent>();
            tileX = (int)Math.Round(pos.X);
            tileY = (int)Math.Round(pos.Z);
        }

        _bus.Emit(StructuralChangeKind.ObstacleAttached, entityId,
            tileX, tileY, tileX, tileY, Guid.Empty, ++_seq);
    }

    /// <inheritdoc/>
    public void DetachObstacle(Guid entityId)
    {
        var entity = FindById(entityId)
            ?? throw new InvalidOperationException($"Entity {entityId} not found.");

        entity.Remove<ObstacleTag>();

        int tileX = 0, tileY = 0;
        if (entity.Has<PositionComponent>())
        {
            var pos = entity.Get<PositionComponent>();
            tileX = (int)Math.Round(pos.X);
            tileY = (int)Math.Round(pos.Z);
        }

        _bus.Emit(StructuralChangeKind.ObstacleDetached, entityId,
            tileX, tileY, tileX, tileY, Guid.Empty, ++_seq);
    }

    /// <inheritdoc/>
    public void ChangeRoomBounds(Guid roomId, BoundsRect newBounds)
    {
        var entity = FindById(roomId)
            ?? throw new InvalidOperationException($"Room entity {roomId} not found.");

        if (!entity.Has<RoomComponent>())
            throw new InvalidOperationException($"Entity {roomId} does not have RoomComponent.");

        var room = entity.Get<RoomComponent>();
        entity.Add(room with { Bounds = newBounds });

        _bus.Emit(StructuralChangeKind.RoomBoundsChanged, roomId,
            room.Bounds.X, room.Bounds.Y, newBounds.X, newBounds.Y, roomId, ++_seq);
    }

    /// <inheritdoc/>
    public void ThrowEntity(Guid entityId, float velocityX, float velocityZ, float velocityY, float decayPerTick)
    {
        var entity = FindById(entityId)
            ?? throw new InvalidOperationException($"Entity {entityId} not found.");

        entity.Add(new ThrownVelocityComponent
        {
            VelocityX       = velocityX,
            VelocityZ       = velocityZ,
            VelocityY       = velocityY,
            DecayPerTick    = decayPerTick,
            ThrownAtTick    = 0,
            ThrownByEntityId = Guid.Empty
        });

        if (!entity.Has<ThrownTag>())
            entity.Add(default(ThrownTag));
    }

    /// <inheritdoc/>
    public Guid SpawnStain(string templateId, int tileX, int tileY)
    {
        var entity = _em.CreateEntity();
        entity.Add(default(StainTag));
        entity.Add(new PositionComponent { X = tileX, Y = 0f, Z = tileY });

        float fallRisk;
        string source;

        switch (templateId)
        {
            case Systems.Physics.StainTemplates.WaterPuddle:
                source   = "physics:liquid-spill";
                fallRisk = 0.40f;
                entity.Add(new StainComponent
                {
                    Source          = source,
                    Magnitude       = 40,
                    CreatedAtTick   = 0,
                    ChronicleEntryId = string.Empty
                });
                entity.Add(new FallRiskComponent { RiskLevel = fallRisk });
                break;

            case Systems.Physics.StainTemplates.BrokenGlass:
                source   = "physics:glass-shards";
                fallRisk = 0.60f;
                entity.Add(new StainComponent
                {
                    Source          = source,
                    Magnitude       = 60,
                    CreatedAtTick   = 0,
                    ChronicleEntryId = string.Empty
                });
                entity.Add(new FallRiskComponent { RiskLevel = fallRisk });
                break;

            default:
                entity.Add(new StainComponent
                {
                    Source          = $"physics:{templateId}",
                    Magnitude       = 30,
                    CreatedAtTick   = 0,
                    ChronicleEntryId = string.Empty
                });
                entity.Add(new FallRiskComponent { RiskLevel = 0.30f });
                break;
        }

        _bus.Emit(StructuralChangeKind.EntityAdded, entity.Id,
            tileX, tileY, tileX, tileY, Guid.Empty, ++_seq);

        return entity.Id;
    }

    // ── Author-mode extensions (WP-4.0.J) ────────────────────────────────────────

    /// <inheritdoc/>
    public Guid CreateRoom(RoomCategory category, BuildingFloor floor, BoundsRect bounds, string? name = null)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new InvalidOperationException(
                $"CreateRoom: bounds must have positive width and height; got {bounds.Width}x{bounds.Height}.");

        // Authored room id — short, unique, predictable for save round-trip.
        var entity = _em.CreateEntity();
        var roomId = $"authored-room-{entity.Id:N}".Substring(0, 24);
        var roomName = name ?? SynthesizeRoomName(category);

        entity.Add(new RoomTag());
        entity.Add(new RoomComponent
        {
            Id           = roomId,
            Name         = roomName,
            Category     = category,
            Floor        = floor,
            Bounds       = bounds,
            // Sensible default indoor illumination (matches typical office overhead fluorescent).
            // The schema requires colorTemperatureK ≥ 1000; using `default` here breaks save/reload
            // (caught by AuthoringLoopEndToEndTests). Lighting systems can override at runtime.
            Illumination = new RoomIllumination
            {
                AmbientLevel      = 50,
                ColorTemperatureK = 4000,
            },
        });
        entity.Add(new PositionComponent
        {
            X = bounds.X + bounds.Width  * 0.5f,
            Y = 0f,
            Z = bounds.Y + bounds.Height * 0.5f,
        });

        _bus.Emit(StructuralChangeKind.EntityAdded, entity.Id,
            bounds.X, bounds.Y, bounds.X, bounds.Y, entity.Id, ++_seq);

        return entity.Id;
    }

    /// <inheritdoc/>
    public void DespawnRoom(Guid roomId, RoomDespawnPolicy policy)
    {
        var roomEntity = FindById(roomId)
            ?? throw new InvalidOperationException($"Room entity {roomId} not found.");
        if (!roomEntity.Has<RoomComponent>())
            throw new InvalidOperationException($"Entity {roomId} does not have RoomComponent.");

        var room    = roomEntity.Get<RoomComponent>();
        var roomKey = room.Id;

        if (policy == RoomDespawnPolicy.CascadeDelete)
        {
            // Delete all lights / apertures / anchor objects whose RoomId matches.
            // NPC slots are NOT cascade-deleted (NPCs persist as a deliberate invariant).
            DeleteByRoomKey<LightSourceComponent>(roomKey, c => c.RoomId);
            DeleteByRoomKey<LightApertureComponent>(roomKey, c => c.RoomId);
            DeleteByRoomKey<AnchorObjectComponent>(roomKey, c => c.RoomId);
        }

        _bus.Emit(StructuralChangeKind.EntityRemoved, roomId,
            room.Bounds.X, room.Bounds.Y, room.Bounds.X, room.Bounds.Y, roomId, ++_seq);

        _em.DestroyEntity(roomEntity);
    }

    /// <inheritdoc/>
    public Guid CreateLightSource(string roomId, int tileX, int tileY,
                                  LightKind kind, LightState state, int intensity, int colorTempK)
    {
        if (string.IsNullOrEmpty(roomId))
            throw new InvalidOperationException("CreateLightSource: roomId is required.");
        if (intensity < 0 || intensity > 100)
            throw new InvalidOperationException(
                $"CreateLightSource: intensity must be 0-100; got {intensity}.");
        if (colorTempK < 1000 || colorTempK > 10000)
            throw new InvalidOperationException(
                $"CreateLightSource: colorTempK must be 1000-10000; got {colorTempK}.");

        var entity = _em.CreateEntity();
        entity.Add(new LightSourceTag());
        entity.Add(new LightSourceComponent
        {
            Id                = $"authored-light-{entity.Id:N}".Substring(0, 25),
            Kind              = kind,
            State             = state,
            Intensity         = intensity,
            ColorTemperatureK = colorTempK,
            TileX             = tileX,
            TileY             = tileY,
            RoomId            = roomId,
        });
        entity.Add(new PositionComponent { X = tileX, Y = 0f, Z = tileY });

        return entity.Id;
    }

    /// <inheritdoc/>
    public void TuneLightSource(Guid lightId, LightState state, int intensity, int colorTempK)
    {
        var entity = FindById(lightId)
            ?? throw new InvalidOperationException($"Light entity {lightId} not found.");
        if (!entity.Has<LightSourceComponent>())
            throw new InvalidOperationException($"Entity {lightId} is not a light source.");
        if (intensity < 0 || intensity > 100)
            throw new InvalidOperationException(
                $"TuneLightSource: intensity must be 0-100; got {intensity}.");
        if (colorTempK < 1000 || colorTempK > 10000)
            throw new InvalidOperationException(
                $"TuneLightSource: colorTempK must be 1000-10000; got {colorTempK}.");

        var current = entity.Get<LightSourceComponent>();
        entity.Add(new LightSourceComponent
        {
            Id                = current.Id,
            Kind              = current.Kind,
            State             = state,
            Intensity         = intensity,
            ColorTemperatureK = colorTempK,
            TileX             = current.TileX,
            TileY             = current.TileY,
            RoomId            = current.RoomId,
        });
    }

    /// <inheritdoc/>
    public Guid CreateLightAperture(string roomId, int tileX, int tileY,
                                    ApertureFacing facing, double areaSqTiles)
    {
        if (string.IsNullOrEmpty(roomId))
            throw new InvalidOperationException("CreateLightAperture: roomId is required.");
        if (areaSqTiles < 0.5 || areaSqTiles > 64.0)
            throw new InvalidOperationException(
                $"CreateLightAperture: areaSqTiles must be 0.5-64.0; got {areaSqTiles}.");

        var entity = _em.CreateEntity();
        entity.Add(new LightApertureTag());
        entity.Add(new LightApertureComponent
        {
            Id          = $"authored-aperture-{entity.Id:N}".Substring(0, 28),
            TileX       = tileX,
            TileY       = tileY,
            RoomId      = roomId,
            Facing      = facing,
            AreaSqTiles = areaSqTiles,
        });
        entity.Add(new PositionComponent { X = tileX, Y = 0f, Z = tileY });

        return entity.Id;
    }

    /// <inheritdoc/>
    public void DespawnLight(Guid lightId)
    {
        var entity = FindById(lightId)
            ?? throw new InvalidOperationException($"Light entity {lightId} not found.");

        var isSource   = entity.Has<LightSourceComponent>();
        var isAperture = entity.Has<LightApertureComponent>();
        if (!isSource && !isAperture)
            throw new InvalidOperationException(
                $"Entity {lightId} is neither a light source nor an aperture.");

        _em.DestroyEntity(entity);
    }

    // ── NPC authoring (WP-4.0.K) ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public Guid CreateNpc(string roomId, int tileX, int tileY, string archetypeId, string name)
    {
        if (_archetypeCatalog is null || _castConfig is null || _castRng is null)
            throw new InvalidOperationException(
                "CreateNpc requires the WorldMutationApi to be constructed with " +
                "ArchetypeCatalog, CastGeneratorConfig, and SeededRandom (use the 5-arg constructor).");
        if (string.IsNullOrEmpty(archetypeId))
            throw new InvalidOperationException("CreateNpc: archetypeId is required.");

        var archetype = _archetypeCatalog.TryGet(archetypeId)
            ?? throw new InvalidOperationException($"CreateNpc: unknown archetype '{archetypeId}'.");

        // Synthesise a slot for CastGenerator.SpawnNpc to consume.
        var slot = new NpcSlotComponent
        {
            X             = tileX,
            Y             = tileY,
            ArchetypeHint = archetypeId,
            RoomId        = roomId,
        };

        var npc = CastGenerator.SpawnNpc(archetype, slot, _em, _castRng, _castConfig);

        if (!string.IsNullOrWhiteSpace(name))
            npc.Add(new IdentityComponent(name));

        return npc.Id;
    }

    /// <inheritdoc/>
    public void DespawnNpc(Guid npcId)
    {
        var entity = FindById(npcId)
            ?? throw new InvalidOperationException($"NPC entity {npcId} not found.");

        if (!entity.Has<NpcArchetypeComponent>())
            throw new InvalidOperationException($"Entity {npcId} is not an NPC (no NpcArchetypeComponent).");

        _em.DestroyEntity(entity);
    }

    /// <inheritdoc/>
    public void RenameNpc(Guid npcId, string newName)
    {
        var entity = FindById(npcId)
            ?? throw new InvalidOperationException($"NPC entity {npcId} not found.");

        if (!entity.Has<IdentityComponent>())
            throw new InvalidOperationException($"Entity {npcId} has no IdentityComponent to rename.");

        var current = entity.Get<IdentityComponent>();
        entity.Add(new IdentityComponent(newName, current.Value));
    }

    // ── Author-mode helpers ──────────────────────────────────────────────────────

    private void DeleteByRoomKey<T>(string roomKey, Func<T, string> getRoomId) where T : struct
    {
        var matches = _em.Query<T>()
            .Where(e => string.Equals(getRoomId(e.Get<T>()), roomKey, StringComparison.Ordinal))
            .ToList();
        foreach (var match in matches)
            _em.DestroyEntity(match);
    }

    private static string SynthesizeRoomName(RoomCategory c) => c switch
    {
        RoomCategory.Breakroom       => "Breakroom",
        RoomCategory.Bathroom        => "Bathroom",
        RoomCategory.CubicleGrid     => "Cubicle Area",
        RoomCategory.Office          => "Office",
        RoomCategory.ConferenceRoom  => "Conference Room",
        RoomCategory.SupplyCloset    => "Supply Closet",
        RoomCategory.ItCloset        => "IT Closet",
        RoomCategory.Hallway         => "Hallway",
        RoomCategory.Stairwell       => "Stairwell",
        RoomCategory.Elevator        => "Elevator",
        RoomCategory.ParkingLot      => "Parking Lot",
        RoomCategory.SmokingArea     => "Smoking Area",
        RoomCategory.LoadingDock     => "Loading Dock",
        RoomCategory.ProductionFloor => "Production Floor",
        RoomCategory.Lobby           => "Lobby",
        RoomCategory.Outdoor         => "Outdoor Area",
        _                            => "Room",
    };

    private Entity? FindById(Guid id) =>
        _em.GetAllEntities().FirstOrDefault(e => e.Id == id);
}
