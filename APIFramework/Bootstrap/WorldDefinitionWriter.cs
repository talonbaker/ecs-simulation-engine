using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using APIFramework.Components;
using APIFramework.Core;
using Warden.Contracts;

namespace APIFramework.Bootstrap;

/// <summary>
/// Inverse of <see cref="WorldDefinitionLoader"/>: walks the live ECS world and serializes
/// it to a <c>world-definition.json</c> file in the same shape the loader consumes.
///
/// Round-trip discipline: <c>Load(file) → mutate → Write(file)</c> produces a file the loader
/// reads back into a structurally-equivalent world (room counts, light counts, NPC slots
/// preserved at their authored positions and archetypes).
///
/// Scope (v0.1, WP-4.0.M): topology + lighting + NPC slot positions + anchor objects. NOT
/// in scope: in-flight simulation state (drives, memories, in-progress actions); build-mode-placed
/// props (`placedProps`) — both deferred to follow-up packets.
///
/// The writer is the substrate for in-game scene authoring (WP-4.0.J extends author mode with
/// a Save toolbar that calls this writer).
/// </summary>
public static class WorldDefinitionWriter
{
    /// <summary>
    /// Serializes the current world to JSON.
    /// </summary>
    /// <param name="entityManager">Source world.</param>
    /// <param name="worldId">Stable identifier for the world (e.g. "playtest-office").</param>
    /// <param name="worldName">Human-readable name.</param>
    /// <param name="seed">Seed value to record in the file (informational; doesn't affect round-trip).</param>
    /// <returns>Indented JSON string suitable for <see cref="File.WriteAllText(string, string)"/>.</returns>
    public static string WriteToString(
        EntityManager entityManager,
        string        worldId,
        string        worldName,
        int           seed)
    {
        var dto = BuildDto(entityManager, worldId, worldName, seed);
        return JsonSerializer.Serialize(dto, JsonOptions.Pretty);
    }

    /// <summary>
    /// Serializes the current world and writes it to <paramref name="path"/> (overwrites existing).
    /// </summary>
    public static void WriteToFile(
        EntityManager entityManager,
        string        path,
        string        worldId,
        string        worldName,
        int           seed)
    {
        var json = WriteToString(entityManager, worldId, worldName, seed);
        File.WriteAllText(path, json);
    }

    // ── Internal: DTO construction ────────────────────────────────────────────────

    internal static WorldDefinitionDto BuildDto(
        EntityManager em,
        string        worldId,
        string        worldName,
        int           seed)
    {
        return new WorldDefinitionDto
        {
            SchemaVersion    = "0.1.0",
            WorldId          = worldId,
            Name             = worldName,
            Seed             = seed,
            Floors           = SerializeFloors(em),
            Rooms            = SerializeRooms(em),
            LightSources     = SerializeLightSources(em),
            LightApertures   = SerializeLightApertures(em),
            NpcSlots         = SerializeNpcSlots(em),
            ObjectsAtAnchors = SerializeAnchorObjects(em),
        };
    }

    private static FloorDefinitionDto[] SerializeFloors(EntityManager em)
    {
        // Walk all rooms; collect the distinct set of floors in use.
        // Floor id is synthesised from the enum value ("first" → "floor-first").
        var floors = em.Query<RoomComponent>()
            .Select(r => r.Get<RoomComponent>().Floor)
            .Distinct()
            .OrderBy(f => f.ToString(), StringComparer.Ordinal)
            .Select(f => new FloorDefinitionDto
            {
                Id        = SynthesizeFloorId(f),
                Name      = SynthesizeFloorName(f),
                FloorEnum = FloorEnumString(f),
            })
            .ToArray();
        return floors;
    }

    private static RoomDefinitionDto[] SerializeRooms(EntityManager em)
    {
        var list = new List<RoomDefinitionDto>();
        foreach (var entity in em.Query<RoomComponent>())
        {
            var rc  = entity.Get<RoomComponent>();
            var dto = new RoomDefinitionDto
            {
                Id       = rc.Id,
                Name     = rc.Name,
                Category = CategoryString(rc.Category),
                FloorId  = SynthesizeFloorId(rc.Floor),
                Bounds   = new BoundsRectDto
                {
                    X      = rc.Bounds.X,
                    Y      = rc.Bounds.Y,
                    Width  = rc.Bounds.Width,
                    Height = rc.Bounds.Height,
                },
                InitialIllumination = new InitialIlluminationDto
                {
                    AmbientLevel      = rc.Illumination.AmbientLevel,
                    ColorTemperatureK = rc.Illumination.ColorTemperatureK,
                },
            };

            if (entity.Has<NamedAnchorComponent>())
            {
                var anchor = entity.Get<NamedAnchorComponent>();
                dto.NamedAnchorTag = anchor.Tag;
                dto.Description    = string.IsNullOrEmpty(anchor.Description) ? null : anchor.Description;
                dto.SmellTag       = anchor.SmellTag;
            }

            if (entity.Has<NoteComponent>())
            {
                var notes = entity.Get<NoteComponent>().Notes;
                if (notes != null && notes.Count > 0)
                    dto.NotesAttached = notes.ToArray();
            }

            list.Add(dto);
        }

        return list
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static LightSourceDefDto[] SerializeLightSources(EntityManager em)
    {
        return em.Query<LightSourceComponent>()
            .Select(e => e.Get<LightSourceComponent>())
            .Select(c => new LightSourceDefDto
            {
                Id                = c.Id,
                Kind              = LightKindString(c.Kind),
                State             = LightStateString(c.State),
                Intensity         = c.Intensity,
                ColorTemperatureK = c.ColorTemperatureK,
                Position          = new TilePointDto { X = c.TileX, Y = c.TileY },
                RoomId            = c.RoomId,
            })
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static LightApertureDefDto[] SerializeLightApertures(EntityManager em)
    {
        return em.Query<LightApertureComponent>()
            .Select(e => e.Get<LightApertureComponent>())
            .Select(c => new LightApertureDefDto
            {
                Id          = c.Id,
                Position    = new TilePointDto { X = c.TileX, Y = c.TileY },
                RoomId      = c.RoomId,
                Facing      = FacingString(c.Facing),
                AreaSqTiles = c.AreaSqTiles,
            })
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static NpcSlotDto[] SerializeNpcSlots(EntityManager em)
    {
        // Two regimes:
        //  (a) WorldDefinitionLoader has run but CastGenerator hasn't yet — NpcSlotTag entities
        //      exist; serialize them directly.
        //  (b) CastGenerator has run — slots are gone, real NPCs exist; serialize each NPC's
        //      current room/position/archetype/identity back to a slot equivalent.
        //
        // Both regimes produce the same DTO shape; the loader can re-spawn either way.

        var slots = new List<NpcSlotDto>();

        foreach (var entity in em.Query<NpcSlotTag>())
        {
            var slot = entity.Get<NpcSlotComponent>();
            slots.Add(new NpcSlotDto
            {
                Id            = $"slot-{ShortIdOf(entity)}",
                RoomId        = slot.RoomId,
                X             = slot.X,
                Y             = slot.Y,
                ArchetypeHint = slot.ArchetypeHint,
            });
        }

        // CastGenerator-spawned NPCs: keyed on NpcArchetypeComponent which only NPCs carry.
        // (IdentityComponent is broader — anchor objects, water, bolus, fridge all have one —
        //  so it isn't a safe discriminator.)
        foreach (var entity in em.Query<NpcArchetypeComponent>())
        {
            if (entity.Has<NpcSlotTag>()) continue;                  // already serialised above
            if (!entity.Has<PositionComponent>()) continue;          // not a positioned NPC

            var pos       = entity.Get<PositionComponent>();
            var arch      = entity.Get<NpcArchetypeComponent>().ArchetypeId;
            var nameHint  = entity.Has<IdentityComponent>()
                ? entity.Get<IdentityComponent>().Name
                : null;

            slots.Add(new NpcSlotDto
            {
                Id            = $"slot-{(nameHint ?? arch)?.ToLowerInvariant().Replace(' ', '-') ?? ShortIdOf(entity)}",
                RoomId        = null, // best-effort; not always known post-spawn
                X             = (int)System.Math.Floor(pos.X),
                Y             = (int)System.Math.Floor(pos.Z),
                ArchetypeHint = arch,
                NameHint      = nameHint,
            });
        }

        return slots
            .OrderBy(s => s.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static AnchorObjectDto[] SerializeAnchorObjects(EntityManager em)
    {
        return em.Query<AnchorObjectComponent>()
            .Select(e => e.Get<AnchorObjectComponent>())
            .Select(c => new AnchorObjectDto
            {
                Id            = c.Id,
                RoomId        = c.RoomId,
                Description   = c.Description,
                PhysicalState = PhysicalStateString(c.PhysicalState),
            })
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ToArray();
    }

    // ── Inverse-of-loader enum maps ───────────────────────────────────────────────

    private static string FloorEnumString(BuildingFloor f) => f switch
    {
        BuildingFloor.Basement => "basement",
        BuildingFloor.First    => "first",
        BuildingFloor.Top      => "top",
        BuildingFloor.Exterior => "exterior",
        _ => throw new InvalidOperationException($"Unknown BuildingFloor: {f}")
    };

    private static string SynthesizeFloorId(BuildingFloor f)   => $"floor-{FloorEnumString(f)}";

    private static string SynthesizeFloorName(BuildingFloor f) => f switch
    {
        BuildingFloor.Basement => "Basement",
        BuildingFloor.First    => "First Floor",
        BuildingFloor.Top      => "Top Floor",
        BuildingFloor.Exterior => "Exterior",
        _ => throw new InvalidOperationException($"Unknown BuildingFloor: {f}")
    };

    private static string CategoryString(RoomCategory c) => c switch
    {
        RoomCategory.Breakroom       => "breakroom",
        RoomCategory.Bathroom        => "bathroom",
        RoomCategory.CubicleGrid     => "cubicleGrid",
        RoomCategory.Office          => "office",
        RoomCategory.ConferenceRoom  => "conferenceRoom",
        RoomCategory.SupplyCloset    => "supplyCloset",
        RoomCategory.ItCloset        => "itCloset",
        RoomCategory.Hallway         => "hallway",
        RoomCategory.Stairwell       => "stairwell",
        RoomCategory.Elevator        => "elevator",
        RoomCategory.ParkingLot      => "parkingLot",
        RoomCategory.SmokingArea     => "smokingArea",
        RoomCategory.LoadingDock     => "loadingDock",
        RoomCategory.ProductionFloor => "productionFloor",
        RoomCategory.Lobby           => "lobby",
        RoomCategory.Outdoor         => "outdoor",
        _ => throw new InvalidOperationException($"Unknown RoomCategory: {c}")
    };

    private static string LightKindString(LightKind k) => k switch
    {
        LightKind.OverheadFluorescent => "overheadFluorescent",
        LightKind.DeskLamp            => "deskLamp",
        LightKind.ServerLed           => "serverLed",
        LightKind.BreakroomStrip      => "breakroomStrip",
        LightKind.ConferenceTrack     => "conferenceTrack",
        LightKind.ExteriorWall        => "exteriorWall",
        LightKind.SignageGlow         => "signageGlow",
        LightKind.Neon                => "neon",
        LightKind.MonitorGlow         => "monitorGlow",
        _ => throw new InvalidOperationException($"Unknown LightKind: {k}")
    };

    private static string LightStateString(LightState s) => s switch
    {
        LightState.On         => "on",
        LightState.Off        => "off",
        LightState.Flickering => "flickering",
        LightState.Dying      => "dying",
        _ => throw new InvalidOperationException($"Unknown LightState: {s}")
    };

    private static string FacingString(ApertureFacing f) => f switch
    {
        ApertureFacing.North    => "north",
        ApertureFacing.East     => "east",
        ApertureFacing.South    => "south",
        ApertureFacing.West     => "west",
        ApertureFacing.Ceiling  => "ceiling",
        _ => throw new InvalidOperationException($"Unknown ApertureFacing: {f}")
    };

    private static string PhysicalStateString(AnchorObjectPhysicalState s) => s switch
    {
        AnchorObjectPhysicalState.Present                => "present",
        AnchorObjectPhysicalState.PresentDegraded        => "present-degraded",
        AnchorObjectPhysicalState.PresentGreatlyDegraded => "present-greatly-degraded",
        AnchorObjectPhysicalState.Absent                 => "absent",
        _ => throw new InvalidOperationException($"Unknown AnchorObjectPhysicalState: {s}")
    };

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static string ShortIdOf(Entity e) => e.Id.ToString("N").Substring(0, 8);
}
