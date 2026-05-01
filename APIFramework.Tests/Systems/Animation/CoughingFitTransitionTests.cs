using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Animation;
using Xunit;

namespace APIFramework.Tests.Systems.Animation;

/// <summary>AT-08: IsChokingTag (without ChokingComponent) → CoughingFit.</summary>
public class CoughingFitTransitionTests
{
    [Fact]
    public void IsChokingTagOnly_ResolvesToCoughingFit()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new IsChokingTag());
        // Deliberately NOT adding ChokingComponent — simulates early/mild phase.

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.Equal(NpcAnimationState.CoughingFit, state);
    }

    [Fact]
    public void IsChokingTagAndChokingComponent_ResolvesToPanic_NotCoughingFit()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new IsChokingTag());
        entity.Add(new ChokingComponent());

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        // ChokingComponent → Panic takes priority over IsChokingTag → CoughingFit.
        Assert.Equal(NpcAnimationState.Panic, state);
    }

    [Fact]
    public void NoChokingTag_DoesNotResolveTosCoughingFit()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new IntendedActionComponent(IntendedActionKind.Idle, 0, DialogContextValue.None, 0));

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.NotEqual(NpcAnimationState.CoughingFit, state);
    }
}
