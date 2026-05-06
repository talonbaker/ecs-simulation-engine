using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Animation;
using Xunit;

namespace APIFramework.Tests.Systems.Animation;

/// <summary>AT-03: Drink intent → animator state Drinking.</summary>
public class DrinkingStateTransitionTests
{
    [Fact]
    public void DrinkIntent_ResolvesToDrinking()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new IntendedActionComponent(IntendedActionKind.Drink, 0, DialogContextValue.None, 50));

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.Equal(NpcAnimationState.Drinking, state);
    }

    [Fact]
    public void DrinkIntent_TakesPriorityOverWork()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        // Drink has higher priority than Work in intent resolution
        entity.Add(new IntendedActionComponent(IntendedActionKind.Drink, 0, DialogContextValue.None, 50));

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.Equal(NpcAnimationState.Drinking, state);
    }
}
