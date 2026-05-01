using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Audio;
using APIFramework.Systems.Spatial;

namespace APIFramework.Systems.Movement;

/// <summary>
/// Per-tick: for pairs of NPCs approaching head-on in a Hallway-category room,
/// applies a small perpendicular position shift to each NPC in the direction
/// matching their HandednessComponent. Runs before MovementSystem so the shift
/// is part of this tick's position update.
/// NPCs in non-hallway rooms (breakroom, open areas) are not affected.
/// </summary>
public sealed class StepAsideSystem : ISystem
{
    private readonly ISpatialIndex        _index;
    private readonly EntityRoomMembership _rooms;
    private readonly int                  _stepAsideRadius;
    private readonly float                _stepAsideShift;
    private readonly SoundTriggerBus?     _soundBus;

    private const float HeadOnCosThreshold = 0.866f; // cos(30°)

    public StepAsideSystem(ISpatialIndex index, EntityRoomMembership rooms, MovementConfig cfg, SoundTriggerBus? soundBus = null)
    {
        _index           = index;
        _rooms           = rooms;
        _stepAsideRadius = (int)MathF.Ceiling(cfg.StepAsideRadius);
        _stepAsideShift  = cfg.StepAsideShift;
        _soundBus        = soundBus;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        var processed = new HashSet<(Guid, Guid)>(); // avoid processing each pair twice

        foreach (var entity in em.Query<MovementComponent>())
        {
            if (!entity.Has<PositionComponent>()) continue;
            if (!entity.Has<HandednessComponent>()) continue;

            if (!IsInHallway(entity, em)) continue;

            var posA = entity.Get<PositionComponent>();
            int tileAX = (int)MathF.Round(posA.X);
            int tileAY = (int)MathF.Round(posA.Z);

            var (velAX, velAZ) = GetMovementDirection(entity, em);
            if (velAX == 0f && velAZ == 0f) continue; // stationary — no step-aside

            var nearby = _index.QueryRadius(tileAX, tileAY, _stepAsideRadius);

            foreach (var other in nearby)
            {
                if (other == entity) continue;
                if (!other.Has<MovementComponent>()) continue;
                if (!other.Has<PositionComponent>()) continue;
                if (!other.Has<HandednessComponent>()) continue;

                // Ensure deterministic pair key
                var pairKey = entity.Id.CompareTo(other.Id) < 0
                    ? (entity.Id, other.Id)
                    : (other.Id, entity.Id);

                if (!processed.Add(pairKey)) continue;

                var posB           = other.Get<PositionComponent>();
                var (velBX, velBZ) = GetMovementDirection(other, em);
                if (velBX == 0f && velBZ == 0f) continue;

                // Relative vector A→B
                float dABx = posB.X - posA.X;
                float dABz = posB.Z - posA.Z;
                float dist = MathF.Sqrt(dABx * dABx + dABz * dABz);
                if (dist < 1e-6f) continue;

                float invDist = 1f / dist;
                float dABxN = dABx * invDist;
                float dABzN = dABz * invDist;

                // Normalise velocities
                float sA = MathF.Sqrt(velAX * velAX + velAZ * velAZ);
                float sB = MathF.Sqrt(velBX * velBX + velBZ * velBZ);
                float avx = velAX / sA, avz = velAZ / sA;
                float bvx = velBX / sB, bvz = velBZ / sB;

                // A approaching B: dot(A_velocity, A→B) > threshold
                float dotA = avx * dABxN + avz * dABzN;
                // B approaching A: dot(B_velocity, B→A) > threshold  (B→A = -d_AB)
                float dotB = bvx * (-dABxN) + bvz * (-dABzN);

                if (dotA < HeadOnCosThreshold || dotB < HeadOnCosThreshold) continue;

                // Apply perpendicular shift to A
                ApplyShift(entity, avx, avz, entity.Get<HandednessComponent>().Side);
                // Apply perpendicular shift to B
                ApplyShift(other,  bvx, bvz, other.Get<HandednessComponent>().Side);

                // Emit ChairSqueak for each entity in the pair
                if (_soundBus != null)
                {
                    var posAfterA = entity.Get<PositionComponent>();
                    _soundBus.Emit(SoundTriggerKind.ChairSqueak, entity.Id, posAfterA.X, posAfterA.Z, 0.4f, 0L);
                    var posAfterB = other.Get<PositionComponent>();
                    _soundBus.Emit(SoundTriggerKind.ChairSqueak, other.Id, posAfterB.X, posAfterB.Z, 0.4f, 0L);
                }
            }
        }
    }

    private void ApplyShift(Entity entity, float vx, float vz, HandednessSide side)
    {
        var pos = entity.Get<PositionComponent>();

        // Perpendicular vectors in the XZ plane (viewed from above, +X=east, +Z=south):
        //   right of (vx, vz) → (-vz,  vx)   e.g. moving east (1,0) → right is south (0,1)
        //   left  of (vx, vz) → ( vz, -vx)   e.g. moving east (1,0) → left  is north (0,-1)
        float perpX = side == HandednessSide.LeftSidePass ?  vz : -vz;
        float perpZ = side == HandednessSide.LeftSidePass ? -vx :  vx;

        entity.Add(new PositionComponent
        {
            X = pos.X + perpX * _stepAsideShift,
            Y = pos.Y,
            Z = pos.Z + perpZ * _stepAsideShift,
        });
    }

    private bool IsInHallway(Entity entity, EntityManager em)
    {
        var room = _rooms.GetRoom(entity);
        if (room is null) return false;
        if (!room.Has<RoomComponent>()) return false;
        return room.Get<RoomComponent>().Category == RoomCategory.Hallway;
    }

    /// <summary>Returns the normalised movement direction for an entity this tick.</summary>
    private static (float vx, float vz) GetMovementDirection(Entity entity, EntityManager em)
    {
        // Prefer path waypoint direction (most accurate for this tick's intent)
        if (entity.Has<PathComponent>())
        {
            var path = entity.Get<PathComponent>();
            if (path.Waypoints != null && path.CurrentWaypointIndex < path.Waypoints.Count)
            {
                var wp  = path.Waypoints[path.CurrentWaypointIndex];
                var pos = entity.Get<PositionComponent>();
                float dx = wp.X - pos.X;
                float dz = wp.Y - pos.Z;
                float len = MathF.Sqrt(dx * dx + dz * dz);
                if (len > 1e-6f) return (dx / len, dz / len);
            }
        }

        // Fall back to recorded velocity from last MovementSystem tick
        var move = entity.Get<MovementComponent>();
        if (move.LastVelocityX != 0f || move.LastVelocityZ != 0f)
        {
            float len = MathF.Sqrt(move.LastVelocityX * move.LastVelocityX + move.LastVelocityZ * move.LastVelocityZ);
            return (move.LastVelocityX / len, move.LastVelocityZ / len);
        }

        return (0f, 0f);
    }
}
