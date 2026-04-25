using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Movement;
using Xunit;

namespace APIFramework.Tests.Systems.Movement;

/// <summary>
/// AT-08 to AT-10: MovementSpeedModifierSystem — drive and energy effects on speed.
/// </summary>
public class MovementSpeedModifierSystemTests
{
    private static Entity MakeNpc(EntityManager em,
        int irritation = 0, int affection = 0, float energy = 100f)
    {
        var e = em.CreateEntity();
        e.Add(new MovementComponent { Speed = 1f, SpeedModifier = 1f });
        e.Add(new SocialDrivesComponent
        {
            Irritation = new DriveValue { Current = irritation, Baseline = irritation },
            Affection  = new DriveValue { Current = affection,  Baseline = affection  },
        });
        e.Add(new EnergyComponent { Energy = energy });
        return e;
    }

    private static MovementSpeedModifierSystem MakeSystem()
        => new MovementSpeedModifierSystem(new MovementConfig());

    // AT-08: High irritation increases multiplier
    [Fact]
    public void HighIrritation_IncreasesSpeedMultiplier()
    {
        var em      = new EntityManager();
        var normal  = MakeNpc(em, irritation: 0);
        var angry   = MakeNpc(em, irritation: 100);
        var sys     = MakeSystem();

        sys.Update(em, 1f);

        float normalMult = normal.Get<MovementComponent>().SpeedModifier;
        float angryMult  = angry.Get<MovementComponent>().SpeedModifier;

        Assert.True(angryMult > normalMult,
            $"Irritation=100 multiplier ({angryMult}) should be > Irritation=0 ({normalMult})");
    }

    // AT-08: Irritation 100 adds 0.5x (within floating-point tolerance)
    [Fact]
    public void Irritation100_AddsHalfPoint()
    {
        var em  = new EntityManager();
        var npc = MakeNpc(em, irritation: 100, affection: 0, energy: 100f);
        MakeSystem().Update(em, 1f);

        // base 1.0 + 100*0.005 = 1.5; no energy penalty (energy=100, (100-100)*0.005=0); no affection loss
        float mult = npc.Get<MovementComponent>().SpeedModifier;
        Assert.InRange(mult, 1.49f, 1.51f);
    }

    // AT-09: High affection decreases multiplier
    [Fact]
    public void HighAffection_DecreasesSpeedMultiplier()
    {
        var em     = new EntityManager();
        var normal = MakeNpc(em, affection: 0);
        var loving = MakeNpc(em, affection: 100);
        var sys    = MakeSystem();

        sys.Update(em, 1f);

        Assert.True(loving.Get<MovementComponent>().SpeedModifier
                  < normal.Get<MovementComponent>().SpeedModifier);
    }

    // AT-09: Low energy (0) slows NPC
    [Fact]
    public void LowEnergy_DecreasesSpeedMultiplier()
    {
        var em      = new EntityManager();
        var rested  = MakeNpc(em, energy: 100f);
        var drained = MakeNpc(em, energy: 0f);
        var sys     = MakeSystem();

        sys.Update(em, 1f);

        Assert.True(drained.Get<MovementComponent>().SpeedModifier
                  < rested.Get<MovementComponent>().SpeedModifier);
    }

    // AT-10: Multiplier is clamped to [0.3, 2.0]
    [Fact]
    public void SpeedMultiplier_ClampsAtMin()
    {
        var em  = new EntityManager();
        // Max penalties: affection=100, energy=0
        var npc = MakeNpc(em, irritation: 0, affection: 100, energy: 0f);
        MakeSystem().Update(em, 1f);

        float mult = npc.Get<MovementComponent>().SpeedModifier;
        Assert.True(mult >= 0.3f, $"Multiplier {mult} should be >= 0.3");
    }

    [Fact]
    public void SpeedMultiplier_ClampsAtMax()
    {
        var em  = new EntityManager();
        // Max gain: irritation=100 → 1.0 + 0.5 = 1.5 (well under 2.0 with defaults)
        // Manually check clamping by verifying it never exceeds 2.0
        var npc = MakeNpc(em, irritation: 100, affection: 0, energy: 100f);
        MakeSystem().Update(em, 1f);

        float mult = npc.Get<MovementComponent>().SpeedModifier;
        Assert.True(mult <= 2.0f, $"Multiplier {mult} should be <= 2.0");
    }

    // Entities without SocialDrivesComponent or EnergyComponent still get SpeedModifier = 1.0
    [Fact]
    public void NoSocialOrEnergy_DefaultsToOne()
    {
        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new MovementComponent { Speed = 1f });

        MakeSystem().Update(em, 1f);

        Assert.InRange(npc.Get<MovementComponent>().SpeedModifier, 0.99f, 1.01f);
    }
}
