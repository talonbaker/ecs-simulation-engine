using System;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Movement;
using Xunit;

namespace APIFramework.Tests.Systems.Movement;

/// <summary>
/// AT-11 to AT-12: IdleMovementSystem — jitter and posture shifts for stationary NPCs.
/// </summary>
public class IdleMovementSystemTests
{
    private static IdleMovementSystem MakeSystem(int seed = 0, float jitter = 0.05f, float postureProb = 0.005f)
    {
        var rng = new SeededRandom(seed);
        var cfg = new MovementConfig { IdleJitterTiles = jitter, IdlePostureShiftProb = postureProb };
        return new IdleMovementSystem(rng, cfg);
    }

    // AT-11: Idle NPC receives position jitter each tick
    [Fact]
    public void IdleNpc_ReceivesPositionJitter()
    {
        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc.Add(new MovementComponent { Speed = 1f });

        var sys = MakeSystem(seed: 42, jitter: 0.05f);

        float initX = npc.Get<PositionComponent>().X;
        float initZ = npc.Get<PositionComponent>().Z;

        // Run several ticks — at least one should move the NPC
        bool anyMoved = false;
        for (int i = 0; i < 20; i++)
        {
            sys.Update(em, 1f);
            var pos = npc.Get<PositionComponent>();
            if (pos.X != initX || pos.Z != initZ) { anyMoved = true; break; }
        }

        Assert.True(anyMoved, "Idle NPC should receive jitter that moves its position");
    }

    // AT-11: Jitter magnitude is bounded by idleJitterTiles
    [Fact]
    public void IdleNpc_JitterBoundedByConfig()
    {
        const float jitter = 0.05f;
        const int   ticks  = 500;

        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new PositionComponent { X = 10f, Y = 0f, Z = 10f });
        npc.Add(new MovementComponent { Speed = 1f });

        var sys = MakeSystem(seed: 77, jitter: jitter);

        float prevX = 10f, prevZ = 10f;
        for (int i = 0; i < ticks; i++)
        {
            sys.Update(em, 1f);
            var pos = npc.Get<PositionComponent>();

            float dX = MathF.Abs(pos.X - prevX);
            float dZ = MathF.Abs(pos.Z - prevZ);

            Assert.True(dX <= jitter + 1e-5f, $"X jitter {dX} exceeded limit {jitter}");
            Assert.True(dZ <= jitter + 1e-5f, $"Z jitter {dZ} exceeded limit {jitter}");

            prevX = pos.X;
            prevZ = pos.Z;
        }
    }

    // AT-12: NPC with active movement target receives zero jitter
    [Fact]
    public void ActiveTarget_NoJitter()
    {
        var em  = new EntityManager();

        var target = em.CreateEntity();
        target.Add(new PositionComponent { X = 20f, Y = 0f, Z = 20f });

        var npc = em.CreateEntity();
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc.Add(new MovementComponent { Speed = 1f });
        npc.Add(new MovementTargetComponent { TargetEntityId = target.Id, Label = "target" });

        var sys = MakeSystem(seed: 0, jitter: 0.05f);

        float initX = npc.Get<PositionComponent>().X;
        float initZ = npc.Get<PositionComponent>().Z;

        for (int i = 0; i < 50; i++)
            sys.Update(em, 1f);

        var finalPos = npc.Get<PositionComponent>();
        Assert.Equal(initX, finalPos.X);
        Assert.Equal(initZ, finalPos.Z);
    }

    // AT-11: Posture shifts occur at expected probability
    [Fact]
    public void PostureShifts_OccurAtExpectedProbability()
    {
        const float prob       = 1.0f; // 100% to guarantee shifts
        const int   ticks      = 10;

        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc.Add(new MovementComponent { Speed = 1f });
        npc.Add(new FacingComponent { DirectionDeg = 0f, Source = FacingSource.Idle });

        var rng = new SeededRandom(0);
        var cfg = new MovementConfig { IdleJitterTiles = 0f, IdlePostureShiftProb = prob };
        var sys = new IdleMovementSystem(rng, cfg);

        int shiftCount = 0;
        float lastDir  = npc.Get<FacingComponent>().DirectionDeg;

        for (int i = 0; i < ticks; i++)
        {
            sys.Update(em, 1f);
            float newDir = npc.Get<FacingComponent>().DirectionDeg;
            if (newDir != lastDir) shiftCount++;
            lastDir = newDir;
        }

        Assert.True(shiftCount > 0, "With prob=1.0, posture shifts should occur every tick");
    }

    // Posture shift stays within ±90° of previous facing
    [Fact]
    public void PostureShift_StaysWithin90Degrees()
    {
        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc.Add(new MovementComponent { Speed = 1f });
        npc.Add(new FacingComponent { DirectionDeg = 90f, Source = FacingSource.Idle });

        var rng = new SeededRandom(1);
        var cfg = new MovementConfig { IdleJitterTiles = 0f, IdlePostureShiftProb = 1.0f };
        var sys = new IdleMovementSystem(rng, cfg);

        // Run once and verify the facing changed by at most 90° from 90°
        sys.Update(em, 1f);
        float dir = npc.Get<FacingComponent>().DirectionDeg;

        // Allowed range: [0°, 180°] with wrap-around considered
        // delta = |newDir - 90| (considering circular wrap)
        float delta = MathF.Abs(dir - 90f);
        if (delta > 180f) delta = 360f - delta;
        Assert.True(delta <= 91f, $"Posture shift delta {delta}° exceeds 90°");
    }
}
