using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Animation;
using Xunit;

namespace APIFramework.Tests.Systems.Animation;

/// <summary>AT-02: Eat intent → animator state Eating.</summary>
public class EatingStateTransitionTests
{
    [Fact]
    public void EatIntent_ResolvesToEating()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new IntendedActionComponent(IntendedActionKind.Eat, 0, DialogContextValue.None, 50));

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.Equal(NpcAnimationState.Eating, state);
    }

    [Fact]
    public void EatIntent_TakesPriorityOverWalk()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new IntendedActionComponent(IntendedActionKind.Eat, 0, DialogContextValue.None, 50));

        // isDtoMoving = true should not override Eating intent
        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: true, isDtoSleeping: false, em);

        Assert.Equal(NpcAnimationState.Eating, state);
    }

    [Fact]
    public void NoIntent_DoesNotResolveToEating()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new IntendedActionComponent(IntendedActionKind.Idle, 0, DialogContextValue.None, 0));

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.NotEqual(NpcAnimationState.Eating, state);
    }
}
