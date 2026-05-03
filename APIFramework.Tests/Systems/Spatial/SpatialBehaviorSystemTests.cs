using System;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Spatial;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.Spatial;

/// <summary>
/// AT-01: Two NPCs at the same position separate within 30 ticks.
/// AT-02: Two NPCs on a collision course maintain minimum distance.
/// AT-03: Hermit NPCs maintain larger spacing than Vent NPCs.
/// AT-04: Rescuer NPC can reach distance 0 (rescue bypass).
/// AT-05: Incapacitated NPCs are skipped.
/// AT-08: 30 NPCs in a cluster reach steady-state non-overlap within 60 ticks.
/// </summary>
public class SpatialBehaviorSystemTests
{
    private const float DefaultRadius   = 0.6f;
    private const float DefaultStrength = 0.3f;

    private static Entity MakeAliveNpc(EntityManager em, float x, float z,
        float radius = DefaultRadius, float strength = DefaultStrength)
    {
        var e = em.CreateEntity();
        e.Add(new NpcTag());
        e.Add(new PositionComponent { X = x, Z = z });
        e.Add(new PersonalSpaceComponent { RadiusMeters = radius, RepulsionStrength = strength });
        e.Add(new LifeStateComponent { State = LS.Alive });
        return e;
    }

    private static float XZDistance(Entity a, Entity b)
    {
        var pa = a.Get<PositionComponent>();
        var pb = b.Get<PositionComponent>();
        float dx = pa.X - pb.X, dz = pa.Z - pb.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    // ── AT-01: NPCs at same position separate ────────────────────────────────

    [Fact]
    public void AT01_TwoNpcsAtSamePosition_SeparateWithin30Ticks()
    {
        var em  = new EntityManager();
        var sys = new SpatialBehaviorSystem();
        var a   = MakeAliveNpc(em, 5f, 5f);
        var b   = MakeAliveNpc(em, 5f, 5f);

        // Soft repulsion converges asymptotically; accept >= 90% of minDist within 30 ticks.
        float threshold = DefaultRadius * 2f * 0.9f;
        bool separated = false;
        for (int t = 0; t < 30; t++)
        {
            sys.Update(em, 1f);
            if (XZDistance(a, b) >= threshold)
            {
                separated = true;
                break;
            }
        }
        Assert.True(separated, $"NPCs at same position should reach >= {threshold:F3} separation within 30 ticks.");
    }

    // ── AT-02: NPCs on collision course maintain minimum distance ─────────────

    [Fact]
    public void AT02_NpcsWalkingToward_MaintainMinimumSeparation()
    {
        var em  = new EntityManager();
        var sys = new SpatialBehaviorSystem();
        var a   = MakeAliveNpc(em, 0f, 0f);
        var b   = MakeAliveNpc(em, 2.0f, 0f);

        float step    = 0.005f;
        float minDist = DefaultRadius * 2f;

        for (int t = 0; t < 60; t++)
        {
            // Move A toward B, B toward A.
            var posA = a.Get<PositionComponent>();
            var posB = b.Get<PositionComponent>();
            if (posA.X < posB.X - 0.01f)
                a.Add(new PositionComponent { X = posA.X + step, Y = posA.Y, Z = posA.Z });
            if (posB.X > posA.X + 0.01f)
                b.Add(new PositionComponent { X = posB.X - step, Y = posB.Y, Z = posB.Z });

            sys.Update(em, 1f);
        }

        Assert.True(XZDistance(a, b) >= minDist * 0.95f,
            $"Minimum separation {minDist * 0.95f:F3} not maintained; actual: {XZDistance(a, b):F3}");
    }

    // ── AT-03: Archetype bias — hermits wider than vents ─────────────────────

    [Fact]
    public void AT03_HermitSpacing_LargerThan_VentSpacing()
    {
        // Hermit: radius 0.6 * 1.40 = 0.84; Vent: radius 0.6 * 0.80 = 0.48
        float hermitRadius = DefaultRadius * 1.40f;
        float ventRadius   = DefaultRadius * 0.80f;

        var em  = new EntityManager();
        var sys = new SpatialBehaviorSystem();
        var h1  = MakeAliveNpc(em, 5f, 5f, hermitRadius, DefaultStrength * 1.20f);
        var h2  = MakeAliveNpc(em, 5f, 5f, hermitRadius, DefaultStrength * 1.20f);
        var v1  = MakeAliveNpc(em, 5f, 5f, ventRadius,   DefaultStrength * 0.80f);
        var v2  = MakeAliveNpc(em, 5f, 5f, ventRadius,   DefaultStrength * 0.80f);

        for (int t = 0; t < 60; t++)
            sys.Update(em, 1f);

        float hermitDist = XZDistance(h1, h2);
        float ventDist   = XZDistance(v1, v2);

        Assert.True(hermitDist > ventDist,
            $"Hermit steady-state spacing ({hermitDist:F3}) should exceed Vent ({ventDist:F3}).");
    }

    // ── AT-04: Rescue bypass — rescuer can reach distance 0 ──────────────────

    [Fact]
    public void AT04_RescuerNpc_CanReachDistanceZero()
    {
        var em  = new EntityManager();
        var sys = new SpatialBehaviorSystem();

        var rescuer = MakeAliveNpc(em, 0f, 0f);
        // Give the rescuer a Rescue intent.
        rescuer.Add(new IntendedActionComponent(IntendedActionKind.Rescue, 0, DialogContextValue.None, 80));

        var victim = em.CreateEntity();
        victim.Add(new NpcTag());
        victim.Add(new PositionComponent { X = 0f, Z = 0f });
        victim.Add(new PersonalSpaceComponent { RadiusMeters = DefaultRadius, RepulsionStrength = DefaultStrength });
        victim.Add(new LifeStateComponent { State = LS.Incapacitated });

        // After ticks, the pair should NOT be pushed apart (victim is Incapacitated so
        // also excluded from the eligible list; both NPCs stay at distance 0).
        sys.Update(em, 1f);

        Assert.Equal(0f, XZDistance(rescuer, victim), precision: 3);
    }

    [Fact]
    public void AT04_TwoAliveNpcs_RescuerBypassed_NoRepulsion()
    {
        var em  = new EntityManager();
        var sys = new SpatialBehaviorSystem();

        var rescuer  = MakeAliveNpc(em, 5f, 5f);
        var patient  = MakeAliveNpc(em, 5f, 5f);

        // Mark rescuer as rescuing — spatial system must skip this pair.
        rescuer.Add(new IntendedActionComponent(IntendedActionKind.Rescue, 0, DialogContextValue.None, 80));

        sys.Update(em, 1f);

        // No nudge: both still at 5, 5.
        Assert.Equal(0f, XZDistance(rescuer, patient), precision: 3);
    }

    // ── AT-05: Incapacitated NPCs are skipped ────────────────────────────────

    [Fact]
    public void AT05_IncapacitatedNpc_NotPushed()
    {
        var em  = new EntityManager();
        var sys = new SpatialBehaviorSystem();

        var alive = MakeAliveNpc(em, 5f, 5f);

        var inCap = em.CreateEntity();
        inCap.Add(new NpcTag());
        inCap.Add(new PositionComponent { X = 5f, Z = 5f });
        inCap.Add(new PersonalSpaceComponent { RadiusMeters = DefaultRadius, RepulsionStrength = DefaultStrength });
        inCap.Add(new LifeStateComponent { State = LS.Incapacitated });

        sys.Update(em, 1f);

        // Incapacitated NPC must not have been nudged.
        Assert.Equal(5f, inCap.Get<PositionComponent>().X, precision: 3);
        Assert.Equal(5f, inCap.Get<PositionComponent>().Z, precision: 3);
    }

    [Fact]
    public void AT05_DeceasedNpc_NotPushed()
    {
        var em  = new EntityManager();
        var sys = new SpatialBehaviorSystem();

        var alive = MakeAliveNpc(em, 5f, 5f);

        var dead = em.CreateEntity();
        dead.Add(new NpcTag());
        dead.Add(new PositionComponent { X = 5f, Z = 5f });
        dead.Add(new PersonalSpaceComponent { RadiusMeters = DefaultRadius, RepulsionStrength = DefaultStrength });
        dead.Add(new LifeStateComponent { State = LS.Deceased });

        sys.Update(em, 1f);

        Assert.Equal(5f, dead.Get<PositionComponent>().X, precision: 3);
        Assert.Equal(5f, dead.Get<PositionComponent>().Z, precision: 3);
    }

    // ── AT-08: 30 NPCs cluster reach steady state within 60 ticks ────────────

    [Fact]
    public void AT08_ThirtyNpcs_ClusteredAtOrigin_NoneOverlapping_Within60Ticks()
    {
        var em  = new EntityManager();
        var sys = new SpatialBehaviorSystem();

        var rng = new Random(42);
        for (int i = 0; i < 30; i++)
            MakeAliveNpc(em, (float)(rng.NextDouble() * 5), (float)(rng.NextDouble() * 5));

        float minDist = DefaultRadius * 2f;
        bool allClear = false;

        for (int t = 0; t < 60; t++)
        {
            sys.Update(em, 1f);

            var positions = new System.Collections.Generic.List<PositionComponent>();
            foreach (var e in em.Query<PersonalSpaceComponent>())
                positions.Add(e.Get<PositionComponent>());

            bool anyOverlap = false;
            for (int i = 0; i < positions.Count - 1 && !anyOverlap; i++)
                for (int j = i + 1; j < positions.Count && !anyOverlap; j++)
                {
                    float dx = positions[i].X - positions[j].X;
                    float dz = positions[i].Z - positions[j].Z;
                    if (MathF.Sqrt(dx * dx + dz * dz) < minDist * 0.95f)
                        anyOverlap = true;
                }

            if (!anyOverlap) { allClear = true; break; }
        }

        Assert.True(allClear, "30 NPCs should reach non-overlapping steady state within 60 ticks.");
    }

    // ── No PersonalSpaceComponent → NPC is ignored by system ─────────────────

    [Fact]
    public void NpcWithoutPersonalSpace_NotAffected()
    {
        var em  = new EntityManager();
        var sys = new SpatialBehaviorSystem();

        var withPS = MakeAliveNpc(em, 5f, 5f);

        var noPS = em.CreateEntity();
        noPS.Add(new NpcTag());
        noPS.Add(new PositionComponent { X = 5f, Z = 5f });
        noPS.Add(new LifeStateComponent { State = LS.Alive });

        sys.Update(em, 1f);

        // Only withPS has the component — only it can move.
        // noPS has no PersonalSpaceComponent, stays at 5, 5.
        Assert.Equal(5f, noPS.Get<PositionComponent>().X, precision: 3);
    }
}
