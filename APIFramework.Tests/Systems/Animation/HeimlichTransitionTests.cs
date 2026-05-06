using System;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Animation;
using Xunit;

namespace APIFramework.Tests.Systems.Animation;

/// <summary>AT-09: Rescue intent + victim has IsChokingTag → Heimlich on rescuer.</summary>
public class HeimlichTransitionTests
{
    [Fact]
    public void RescueIntent_VictimChoking_ResolvesToHeimlick()
    {
        var em = new EntityManager();

        // Spawn a choking victim first so its ID has lower int-bytes.
        var victim = em.CreateEntity();
        victim.Add(new NpcTag());
        victim.Add(new IsChokingTag());
        victim.Add(new ChokingComponent());

        // Rescuer has Rescue intent targeting the victim.
        var rescuer = em.CreateEntity();
        rescuer.Add(new NpcTag());
        int victimIntId = IntIdOf(victim);
        rescuer.Add(new IntendedActionComponent(IntendedActionKind.Rescue, victimIntId, DialogContextValue.None, 80));

        var state = AnimationStateResolver.Resolve(rescuer, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.Equal(NpcAnimationState.Heimlich, state);
    }

    [Fact]
    public void RescueIntent_VictimNotChoking_DoesNotResolveToHeimlick()
    {
        var em = new EntityManager();

        var victim = em.CreateEntity();
        victim.Add(new NpcTag());
        // Victim is incapacitated but not choking → CPR scenario, not Heimlich.

        var rescuer = em.CreateEntity();
        rescuer.Add(new NpcTag());
        int victimIntId = IntIdOf(victim);
        rescuer.Add(new IntendedActionComponent(IntendedActionKind.Rescue, victimIntId, DialogContextValue.None, 80));

        var state = AnimationStateResolver.Resolve(rescuer, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.NotEqual(NpcAnimationState.Heimlich, state);
    }

    [Fact]
    public void RescueIntent_TargetIdZero_DoesNotResolveToHeimlick()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new IntendedActionComponent(IntendedActionKind.Rescue, 0, DialogContextValue.None, 80));

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.NotEqual(NpcAnimationState.Heimlich, state);
    }

    private static int IntIdOf(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return BitConverter.ToInt32(b, 0);
    }
}
