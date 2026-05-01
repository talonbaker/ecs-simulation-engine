using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Animation;
using Xunit;

namespace APIFramework.Tests.Systems.Animation;

/// <summary>AT-05: Energy &lt; 25 + not officially sleeping → SleepingAtDesk.</summary>
public class SleepingAtDeskTransitionTests
{
    [Fact]
    public void LowEnergy_NotSleeping_NoLocomotion_ResolvesToSleepingAtDesk()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new EnergyComponent { Energy = 20f, IsSleeping = false });
        entity.Add(new IntendedActionComponent(IntendedActionKind.Idle, 0, DialogContextValue.None, 0));

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.Equal(NpcAnimationState.SleepingAtDesk, state);
    }

    [Fact]
    public void LowEnergy_OfficiallyAsleep_ResolvesToSleep_NotSleepingAtDesk()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new EnergyComponent { Energy = 20f, IsSleeping = true });
        entity.Add(new IntendedActionComponent(IntendedActionKind.Idle, 0, DialogContextValue.None, 0));

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: true, em);

        Assert.Equal(NpcAnimationState.Sleep, state);
    }

    [Fact]
    public void HighEnergy_DoesNotResolveToSleepingAtDesk()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new EnergyComponent { Energy = 80f, IsSleeping = false });
        entity.Add(new IntendedActionComponent(IntendedActionKind.Idle, 0, DialogContextValue.None, 0));

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.NotEqual(NpcAnimationState.SleepingAtDesk, state);
    }

    [Fact]
    public void LowEnergy_ActiveLocomotion_DoesNotResolveToSleepingAtDesk()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new EnergyComponent { Energy = 20f, IsSleeping = false });
        entity.Add(new IntendedActionComponent(IntendedActionKind.Approach, 0, DialogContextValue.None, 0));

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: true, isDtoSleeping: false, em);

        Assert.NotEqual(NpcAnimationState.SleepingAtDesk, state);
    }
}
