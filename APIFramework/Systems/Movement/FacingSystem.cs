using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Spatial;

namespace APIFramework.Systems.Movement;

/// <summary>
/// Per-tick: updates FacingComponent based on movement velocity.
/// For moving NPCs, facing follows velocity; if the NPC is in conversation range
/// with a partner, facing overrides to point at that partner.
/// Stationary NPC facing is owned by IdleMovementSystem.
/// </summary>
public sealed class FacingSystem : ISystem
{
    private readonly Dictionary<Entity, Entity> _partners = new();

    public FacingSystem(ProximityEventBus bus)
    {
        bus.OnEnteredConversationRange += e => _partners[e.Observer] = e.Target;
        bus.OnLeftConversationRange    += e =>
        {
            if (_partners.TryGetValue(e.Observer, out var t) && t == e.Target)
                _partners.Remove(e.Observer);
        };
    }

    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<FacingComponent>())
        {
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
    public static float VectorToAngle(float dx, float dz)
    {
        float deg = MathF.Atan2(dx, -dz) * (180f / MathF.PI);
        return deg < 0f ? deg + 360f : deg;
    }
}
