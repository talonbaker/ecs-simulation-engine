using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using APIFramework.Components;
using APIFramework.Core;
using Warden.Contracts;
using Warden.Contracts.SchemaValidation;

namespace APIFramework.Bootstrap;

/// <summary>
/// Reads a world-definition.json file, validates it against the embedded
/// <c>world-definition.schema.json</c>, and instantiates all world entities
/// (rooms, light sources, light apertures, NPC slots, anchor objects) into the
/// supplied <see cref="EntityManager"/>.
///
/// This is the data-driven world-spawn path. Test scenarios continue to use
/// <c>EntityTemplates</c> directly; this loader is invoked only when a
/// <c>--world-definition</c> path is supplied to <see cref="SimulationBootstrapper"/>.
///
/// Fail-closed: any validation error throws <see cref="WorldDefinitionInvalidException"/>.
/// No retry, no self-healing, no fallback.
/// </summary>
public static class WorldDefinitionLoader
{
    public static LoadResult LoadFromFile(string path, EntityManager entityManager, SeededRandom rng)
    {
        var json = File.ReadAllText(path);

        // Validate before deserializing — fail-closed per SRD §4.1.
        var validationResult = SchemaValidator.Validate(json, Schema.WorldDefinition);
        if (!validationResult.IsValid)
            throw new WorldDefinitionInvalidException(validationResult.Errors);

        var dto = JsonSerializer.Deserialize<WorldDefinitionDto>(json, JsonOptions.Wire);
        if (dto == null)
            throw new WorldDefinitionInvalidException(new[] { "JSON root deserialised to null." });

        int roomCount = 0, lightSourceCount = 0, apertureCount = 0, npcSlotCount = 0, objectCount = 0;

        // Track room bounds by id for positioning anchor objects.
        var roomBoundsMap = new Dictionary<string, (int X, int Y, int W, int H)>(StringComparer.Ordinal);

        // Floors — metadata only, no entities created.
        var floorEnumMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var floor in dto.Floors)
            floorEnumMap[floor.Id] = floor.FloorEnum;

        // ── Rooms ─────────────────────────────────────────────────────────────
        foreach (var room in dto.Rooms)
        {
            var floor    = ParseFloor(room.FloorId, floorEnumMap);
            var category = ParseCategory(room.Category);
            var bounds   = new BoundsRect(room.Bounds.X, room.Bounds.Y, room.Bounds.Width, room.Bounds.Height);
            var illum    = new RoomIllumination
            {
                AmbientLevel      = room.InitialIllumination.AmbientLevel,
                ColorTemperatureK = room.InitialIllumination.ColorTemperatureK,
            };

            var entity = EntityTemplates.Room(entityManager, room.Id, room.Name, category, floor, bounds, illum);

            if (room.NamedAnchorTag != null)
                entity.Add(new NamedAnchorComponent
                {
                    Tag         = room.NamedAnchorTag,
                    Description = room.Description ?? "",
                    SmellTag    = room.SmellTag,
                });

            if (room.NotesAttached is { Length: > 0 })
                entity.Add(new NoteComponent { Notes = (System.Collections.Generic.IReadOnlyList<string>)room.NotesAttached });

            roomBoundsMap[room.Id] = (room.Bounds.X, room.Bounds.Y, room.Bounds.Width, room.Bounds.Height);
            roomCount++;
        }

        // ── Light sources ─────────────────────────────────────────────────────
        foreach (var src in dto.LightSources)
        {
            var kind  = ParseLightKind(src.Kind);
            var state = ParseLightState(src.State);
            EntityTemplates.LightSource(
                entityManager, src.Id, kind, state,
                src.Intensity, src.ColorTemperatureK,
                src.Position.X, src.Position.Y,
                src.RoomId);
            lightSourceCount++;
        }

        // ── Light apertures ───────────────────────────────────────────────────
        foreach (var apt in dto.LightApertures)
        {
            var facing = ParseFacing(apt.Facing);
            EntityTemplates.LightAperture(
                entityManager, apt.Id,
                apt.Position.X, apt.Position.Y,
                apt.RoomId, facing, apt.AreaSqTiles);
            apertureCount++;
        }

        // ── NPC slots — marker entities for the cast generator ────────────────
        foreach (var slot in dto.NpcSlots)
        {
            var entity = entityManager.CreateEntity();
            entity.Add(new NpcSlotTag());
            entity.Add(new NpcSlotComponent
            {
                X             = slot.X,
                Y             = slot.Y,
                ArchetypeHint = slot.ArchetypeHint,
                RoomId        = slot.RoomId,
            });
            entity.Add(new PositionComponent { X = slot.X, Y = 0f, Z = slot.Y });
            npcSlotCount++;
        }

        // ── Anchor objects ────────────────────────────────────────────────────
        foreach (var obj in dto.ObjectsAtAnchors)
        {
            int tileX = 0, tileY = 0;
            if (roomBoundsMap.TryGetValue(obj.RoomId, out var rb))
            {
                tileX = rb.X + rb.W / 2;
                tileY = rb.Y + rb.H / 2;
            }
            var physState = ParsePhysicalState(obj.PhysicalState);
            EntityTemplates.WorldObject(entityManager, obj.Id, obj.RoomId, obj.Description, physState, tileX, tileY);
            objectCount++;
        }

        return new LoadResult(roomCount, lightSourceCount, apertureCount, npcSlotCount, objectCount, dto.Seed);
    }

    // ── Enum parsers ──────────────────────────────────────────────────────────

    private static BuildingFloor ParseFloor(string floorId, Dictionary<string, string> floorEnumMap)
    {
        if (!floorEnumMap.TryGetValue(floorId, out var floorEnum))
            throw new WorldDefinitionInvalidException(new[] { $"Room references unknown floorId '{floorId}'." });

        return floorEnum switch
        {
            "basement" => BuildingFloor.Basement,
            "first"    => BuildingFloor.First,
            "top"      => BuildingFloor.Top,
            "exterior" => BuildingFloor.Exterior,
            _          => throw new WorldDefinitionInvalidException(new[] { $"Unknown floorEnum value '{floorEnum}'." })
        };
    }

    private static RoomCategory ParseCategory(string s) => s switch
    {
        "breakroom"      => RoomCategory.Breakroom,
        "bathroom"       => RoomCategory.Bathroom,
        "cubicleGrid"    => RoomCategory.CubicleGrid,
        "office"         => RoomCategory.Office,
        "conferenceRoom" => RoomCategory.ConferenceRoom,
        "supplyCloset"   => RoomCategory.SupplyCloset,
        "itCloset"       => RoomCategory.ItCloset,
        "hallway"        => RoomCategory.Hallway,
        "stairwell"      => RoomCategory.Stairwell,
        "elevator"       => RoomCategory.Elevator,
        "parkingLot"     => RoomCategory.ParkingLot,
        "smokingArea"    => RoomCategory.SmokingArea,
        "loadingDock"    => RoomCategory.LoadingDock,
        "productionFloor"=> RoomCategory.ProductionFloor,
        "lobby"          => RoomCategory.Lobby,
        "outdoor"        => RoomCategory.Outdoor,
        _                => throw new WorldDefinitionInvalidException(new[] { $"Unknown room category '{s}'." })
    };

    private static LightKind ParseLightKind(string s) => s switch
    {
        "overheadFluorescent" => LightKind.OverheadFluorescent,
        "deskLamp"            => LightKind.DeskLamp,
        "serverLed"           => LightKind.ServerLed,
        "breakroomStrip"      => LightKind.BreakroomStrip,
        "conferenceTrack"     => LightKind.ConferenceTrack,
        "exteriorWall"        => LightKind.ExteriorWall,
        "signageGlow"         => LightKind.SignageGlow,
        "neon"                => LightKind.Neon,
        "monitorGlow"         => LightKind.MonitorGlow,
        "otherInterior"       => LightKind.OtherInterior,
        _                     => throw new WorldDefinitionInvalidException(new[] { $"Unknown light kind '{s}'." })
    };

    private static LightState ParseLightState(string s) => s switch
    {
        "on"        => LightState.On,
        "off"       => LightState.Off,
        "flickering"=> LightState.Flickering,
        "dying"     => LightState.Dying,
        _           => throw new WorldDefinitionInvalidException(new[] { $"Unknown light state '{s}'." })
    };

    private static ApertureFacing ParseFacing(string s) => s switch
    {
        "north"   => ApertureFacing.North,
        "east"    => ApertureFacing.East,
        "south"   => ApertureFacing.South,
        "west"    => ApertureFacing.West,
        "ceiling" => ApertureFacing.Ceiling,
        _         => throw new WorldDefinitionInvalidException(new[] { $"Unknown aperture facing '{s}'." })
    };

    private static AnchorObjectPhysicalState ParsePhysicalState(string s) => s switch
    {
        "present"                 => AnchorObjectPhysicalState.Present,
        "present-degraded"        => AnchorObjectPhysicalState.PresentDegraded,
        "present-greatly-degraded"=> AnchorObjectPhysicalState.PresentGreatlyDegraded,
        "absent"                  => AnchorObjectPhysicalState.Absent,
        _                         => throw new WorldDefinitionInvalidException(new[] { $"Unknown physical state '{s}'." })
    };
}
