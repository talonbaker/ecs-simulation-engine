using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Lighting;
using APIFramework.Systems.Spatial;

namespace APIFramework.Systems.Coupling;

/// <summary>
/// Phase: Coupling (8) — runs after Lighting (illumination is fresh) and before Cognition/DriveDynamics.
///
/// Per tick, for each NPC:
///   1. Resolve the NPC's current room via EntityRoomMembership. Skip if none.
///   2. Read the room's RoomComponent.Illumination and resolve the dominant light source's State/Kind.
///   3. Walk LightingDriveCouplingTable.Entries in declaration order; first matching entry wins.
///   4. Accumulate the entry's DeltasPerTick in SocialDriveAccumulator (per-entity, per-drive).
///   5. Flush integer parts of the accumulator into SocialDrivesComponent.Current (clamped 0–100).
///
/// NPCs without a room assignment receive no lighting delta this tick.
/// NPC iteration order is ascending by Entity.Id (Guid) for determinism.
/// </summary>
/// <remarks>
/// Reads: <c>RoomComponent.Illumination</c>, <c>LightSourceComponent</c>, <c>LightApertureComponent</c>,
/// <see cref="SunStateService"/>, <see cref="ApertureBeamSystem"/>, <see cref="EntityRoomMembership"/>.
/// Writes: <c>SocialDrivesComponent</c> via <see cref="SocialDriveAccumulator"/>.
///
/// Skips NPCs that fail <see cref="LifeStateGuard.IsAlive"/>.
/// </remarks>
/// <seealso cref="LightingDriveCouplingTable"/>
/// <seealso cref="SocialDriveAccumulator"/>
public sealed class LightingToDriveCouplingSystem : ISystem
{
    private readonly LightingDriveCouplingTable _table;
    private readonly SocialDriveAccumulator     _accumulator;
    private readonly EntityRoomMembership       _roomMembership;
    private readonly ApertureBeamSystem         _apertureSystem;
    private readonly SunStateService            _sunService;

    // Pre-allocated buffers reused every tick — no per-frame allocation after warm-up.
    private readonly List<Entity>          _npcBuffer       = new();
    private readonly Dictionary<string, Entity> _lightSourceById = new();
    private readonly HashSet<string>       _roomsWithBeam   = new();

    /// <summary>
    /// Stores the coupling table, accumulator, and lighting service references used each tick.
    /// </summary>
    /// <param name="table">First-match-wins table loaded from SimConfig.lighting.driveCouplings.</param>
    /// <param name="accumulator">Per-entity, per-drive fractional accumulator shared across ticks.</param>
    /// <param name="roomMembership">Membership lookup used to resolve each NPC's current room.</param>
    /// <param name="apertureSystem">Provides cached per-aperture beam states for the current tick.</param>
    /// <param name="sunService">Singleton sun-state service — provides DayPhase for context matching.</param>
    public LightingToDriveCouplingSystem(
        LightingDriveCouplingTable table,
        SocialDriveAccumulator     accumulator,
        EntityRoomMembership       roomMembership,
        ApertureBeamSystem         apertureSystem,
        SunStateService            sunService)
    {
        _table          = table;
        _accumulator    = accumulator;
        _roomMembership = roomMembership;
        _apertureSystem = apertureSystem;
        _sunService     = sunService;
    }

    /// <summary>
    /// Per-tick entry point. Refreshes light-source and aperture caches, then iterates
    /// NPCs in entity-id order, resolving the matching coupling entry for each NPC's
    /// room context and accumulating the entry's drive deltas.
    /// </summary>
    /// <param name="em">Entity manager — queried for NPCs, light sources, and apertures.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused; deltas are per-tick).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        // Build per-tick lookup caches — O(sources + apertures), not O(sources × NPCs).
        RefreshLightSourceCache(em);
        RefreshApertureBeamCache(em);

        var dayPhase = _sunService.CurrentSunState.DayPhase.ToString().ToLowerInvariant();

        // Collect and sort NPCs by Entity.Id for deterministic iteration order.
        _npcBuffer.Clear();
        foreach (var entity in em.Query<NpcTag>())
            _npcBuffer.Add(entity);
        _npcBuffer.Sort(static (a, b) => a.Id.CompareTo(b.Id));

        foreach (var entity in _npcBuffer)
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip non-Alive NPCs
            if (!entity.Has<SocialDrivesComponent>()) continue;

            var roomEntity = _roomMembership.GetRoom(entity);
            if (roomEntity is null) continue;
            if (!roomEntity.Has<RoomComponent>()) continue;

            var roomComp = roomEntity.Get<RoomComponent>();
            var illum    = roomComp.Illumination;

            // Resolve dominant light source state and kind (null if no dominant source).
            string? domState = null;
            string? domKind  = null;
            if (illum.DominantSourceId != null &&
                _lightSourceById.TryGetValue(illum.DominantSourceId, out var srcEntity))
            {
                var src  = srcEntity.Get<LightSourceComponent>();
                domState = src.State.ToString().ToLowerInvariant();
                domKind  = src.Kind.ToString().ToLowerInvariant();
            }

            var ctx = new CouplingMatchContext(
                roomCategory:        roomComp.Category.ToString().ToLowerInvariant(),
                dominantSourceState: domState,
                dominantSourceKind:  domKind,
                ambientLevel:        illum.AmbientLevel,
                dayPhase:            dayPhase,
                apertureBeamPresent: roomComp.Id != null && _roomsWithBeam.Contains(roomComp.Id));

            var entry = _table.FindFirst(ctx);
            if (entry is null) continue;

            var id = entity.Id;
            foreach (var kv in entry.DeltasPerTick)
                _accumulator.AddDelta(id, kv.Key, kv.Value);

            var drives = entity.Get<SocialDrivesComponent>();
            _accumulator.FlushTo(id, ref drives);
            entity.Add(drives);
        }
    }

    private void RefreshLightSourceCache(EntityManager em)
    {
        _lightSourceById.Clear();
        foreach (var entity in em.Query<LightSourceTag>())
        {
            var src = entity.Get<LightSourceComponent>();
            if (src.Id != null)
                _lightSourceById[src.Id] = entity;
        }
    }

    private void RefreshApertureBeamCache(EntityManager em)
    {
        _roomsWithBeam.Clear();
        foreach (var entity in em.Query<LightApertureTag>())
        {
            var beam = _apertureSystem.GetBeamState(entity);
            if (beam.HasValue && beam.Value.Intensity > 0)
            {
                var aperture = entity.Get<LightApertureComponent>();
                if (aperture.RoomId != null)
                    _roomsWithBeam.Add(aperture.RoomId);
            }
        }
    }
}
