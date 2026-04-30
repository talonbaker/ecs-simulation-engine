using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Spatial;

namespace APIFramework.Systems.Movement;

/// <summary>
/// Per-tick: updates FacingComponent based on movement velocity.
/// For moving NPCs, facing follows velocity; if the NPC is in conversation range
/// with a partner, facing overrides to point at that partner.
/// Stationary NPC facing is owned by IdleMovementSystem.
/// </summary>
/// <remarks>
/// Phase: World (60). Reads <c>MovementComponent</c>, <c>PositionComponent</c>; writes
/// <c>FacingComponent</c>. Single-writer rule for <c>FacingComponent</c> when an NPC is
/// moving; <see cref="IdleMovementSystem"/> writes facing only when stationary.
/// Skips non-Alive NPCs.
/// </remarks>
public sealed class FacingSystem : ISystem
{
    private readonly Dictionary<Entity, Entity> _partners = new();

    /// <summary>
    /// Subscribes to conversation-range entry/exit events to maintain the per-observer
    /// partner map used to override facing direction.
    /// </summary>
    /// <param name="bus">Proximity event bus that fires conversation-range events.</param>
    public FacingSystem(ProximityEventBus bus)
    {
        bus.OnEnteredConversationRange += e => _partners[e.Observer] = e.Target;
        bus.OnLeftConversationRange    += e =>
        {
            if (_partners.TryGetValue(e.Observer, out var t) && t == e.Target)
                _partners.Remove(e.Observer);
        };
    }

    /// <summary>
    /// Per-tick entry point. Updates <c>FacingComponent</c> for every moving NPC.
    /// </summary>
    /// <param name="em">Entity manager — queried for facing-capable entities.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<FacingComponent>())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip non-Alive NPCs
            if (!entity.Has<MovementComponent>()) continue;

            var move = entity.Get<MovementComponent>();
            float vx = move.LastVelocityX;
            float vz = move.LastVelocityZ;

            if (MathF.Abs(vx) < 1e-6f && MathF.Abs(vz) < 1e-6f)
                continue; // stationary — IdleMovementSystem owns facing

            // Conversation-partner override
            if (_partners.TryGetValue(entity, out var partner) && partner.Has<PositionComponent>())
            {
                var myPos      = entity.Get<PositionComponent>();
                var partnerPos = partner.Get<PositionComponent>();
                float pdx = partnerPos.X - myPos.X;
                float pdz = partnerPos.Z - myPos.Z;

                entity.Add(new FacingComponent
                {
                    DirectionDeg = VectorToAngle(pdx, pdz),
                    Source       = FacingSource.ConversationPartner,
                });
                continue;
            }

            entity.Add(new FacingComponent
            {
                DirectionDeg = VectorToAngle(vx, vz),
                Source       = FacingSource.MovementVelocity,
            });
        }
    }

    /// <summary>
    /// Converts a direction vector (dx, dz) to degrees using the 0=north, 90=east convention.
    /// </summary>
    /// <param name="dx">X-axis component of the direction vector.</param>
    /// <param name="dz">Z-axis component of the direction vector.</param>
    /// <returns>The corresponding compass angle in [0, 360) degrees.</returns>
    public static float VectorToAngle(float dx, float dz)
    {
        float deg = MathF.Atan2(dx, -dz) * (180f / MathF.PI);
        return deg < 0f ? deg + 360f : deg;
    }
}
