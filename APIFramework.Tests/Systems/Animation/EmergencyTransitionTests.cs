using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Animation;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.Animation;

/// <summary>
/// Emergency transitions: any state → Panic when acute choking; any state → Dead when Deceased.
/// These must override all lower-priority states.
/// </summary>
public class EmergencyTransitionTests
{
    [Fact]
    public void ChokingComponent_ResolvesToPanic_EvenWithWorkIntent()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new ChokingComponent());
        entity.Add(new IsChokingTag());
        entity.Add(new IntendedActionComponent(IntendedActionKind.Work, 0, DialogContextValue.None, 0));

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.Equal(NpcAnimationState.Panic, state);
    }

    [Fact]
    public void ChokingComponent_ResolvesToPanic_EvenWithHighGrief()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new ChokingComponent());
        entity.Add(new IsChokingTag());
        entity.Add(new MoodComponent { GriefLevel = 1.0f });

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.Equal(NpcAnimationState.Panic, state);
    }

    [Fact]
    public void HighPanicLevel_ResolvesToPanic_WithoutChokingComponent()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new MoodComponent { PanicLevel = 0.85f });
        entity.Add(new IntendedActionComponent(IntendedActionKind.Eat, 0, DialogContextValue.None, 0));

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.Equal(NpcAnimationState.Panic, state);
    }

    [Fact]
    public void Deceased_ResolvesToDead_EvenWithChokingComponent()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new LifeStateComponent { State = LS.Deceased });
        entity.Add(new ChokingComponent());

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.Equal(NpcAnimationState.Dead, state);
    }

    [Fact]
    public void Deceased_ResolvesToDead_EvenWithEatIntent()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new LifeStateComponent { State = LS.Deceased });
        entity.Add(new IntendedActionComponent(IntendedActionKind.Eat, 0, DialogContextValue.None, 50));

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.Equal(NpcAnimationState.Dead, state);
    }
}
