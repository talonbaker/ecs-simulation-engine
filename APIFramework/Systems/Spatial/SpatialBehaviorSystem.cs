using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems.Spatial;

/// <summary>
/// Cleanup phase (90). Soft per-tick positional nudge that keeps NPCs from overlapping
/// each other. For each pair of alive NPCs whose personal-space bubbles intersect,
/// both NPCs are nudged apart by a fraction of the overlap this tick.
///
/// ALGORITHM
/// ─────────
/// 1. Collect all eligible NPCs: NpcTag + PositionComponent + PersonalSpaceComponent,
///    State == Alive (Incapacitated/Deceased excluded per spec).
/// 2. For each unique pair (a, b):
///    - Skip the pair if either NPC has an active Rescue intent
///      (rescuers must be able to reach incapacitated patients).
///    - Compute XZ-plane distance.
///    - If distance &lt; (a.Radius + b.Radius): nudge both apart.
/// 3. Edge case — exact overlap (distance ≈ 0): use entity-ID-parity to pick a
///    deterministic push direction (no jitter / oscillation).
///
/// Runs O(N²) over alive NPCs. At 30 NPCs that is 435 pairs per tick — well inside
/// the 60 FPS / 1000+ ticks-per-second gate for the test runner.
/// </summary>
public sealed class SpatialBehaviorSystem : ISystem
{
    private const float ZeroDistanceThreshold = 1e-4f;

    /// <summary>Per-tick update — applies soft repulsion to all overlapping NPC pairs.</summary>
    public void Update(EntityManager em, float deltaTime)
    {
        // Collect eligible entities once per tick.
        var npcs = new List<Entity>(32);
        foreach (var e in em.Query<NpcTag>())
        {
            if (!e.Has<PositionComponent>())        continue;
            if (!e.Has<PersonalSpaceComponent>())   continue;
            if (!LifeStateGuard.IsAlive(e))         continue;
            npcs.Add(e);
        }

        int count = npcs.Count;
        for (int i = 0; i < count - 1; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                var a = npcs[i];
                var b = npcs[j];

                // Rescue bypass: skip the pair in both directions.
                if (IsRescuing(a) || IsRescuing(b)) continue;

                var posA = a.Get<PositionComponent>();
                var posB = b.Get<PositionComponent>();
                var psA  = a.Get<PersonalSpaceComponent>();
                var psB  = b.Get<PersonalSpaceComponent>();

                float dx   = posA.X - posB.X;
                float dz   = posA.Z - posB.Z;
                float dist = MathF.Sqrt(dx * dx + dz * dz);

                float minDist = psA.RadiusMeters + psB.RadiusMeters;
                if (dist >= minDist) continue;

                float overlap = minDist - dist;

                float pushX, pushZ;
                if (dist < ZeroDistanceThreshold)
                {
                    // Deterministic escape: use entity-ID parity so the two NPCs
                    // push in opposite directions and do not oscillate.
                    int parity = (a.Id.GetHashCode() & 1) == 0 ? 1 : -1;
                    pushX = parity;
                    pushZ = 0f;
                }
                else
                {
                    float inv = 1f / dist;
                    pushX = dx * inv;
                    pushZ = dz * inv;
                }

                // Each NPC receives half the overlap nudge, damped by its own RepulsionStrength.
                float nudgeA = overlap * psA.RepulsionStrength * 0.5f;
                float nudgeB = overlap * psB.RepulsionStrength * 0.5f;

                a.Add(new PositionComponent { X = posA.X + pushX * nudgeA, Y = posA.Y, Z = posA.Z + pushZ * nudgeA });
                b.Add(new PositionComponent { X = posB.X - pushX * nudgeB, Y = posB.Y, Z = posB.Z - pushZ * nudgeB });
            }
        }
    }

    private static bool IsRescuing(Entity e)
    {
        if (!e.Has<IntendedActionComponent>()) return false;
        return e.Get<IntendedActionComponent>().Kind == IntendedActionKind.Rescue;
    }
}
