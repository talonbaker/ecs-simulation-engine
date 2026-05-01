using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Animation;
using Xunit;

namespace APIFramework.Tests.Systems.Animation;

/// <summary>AT-13: Same engine state, two calls → same animator state sequence.</summary>
public class AnimationStateDeterminismTests
{
    [Fact]
    public void SameComponents_TwoCalls_ProduceSameState()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new IntendedActionComponent(IntendedActionKind.Eat, 0, DialogContextValue.None, 50));
        entity.Add(new MoodComponent { GriefLevel = 0.3f });
        entity.Add(new EnergyComponent { Energy = 80f, IsSleeping = false });

        var state1 = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);
        var state2 = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.Equal(state1, state2);
    }

    [Fact]
    public void StateChangesOnlyWhenComponentsChange()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new IntendedActionComponent(IntendedActionKind.Work, 0, DialogContextValue.None, 0));

        var state1 = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);
        Assert.Equal(NpcAnimationState.Working, state1);

        // Swap intent to Eat.
        entity.Add(new IntendedActionComponent(IntendedActionKind.Eat, 0, DialogContextValue.None, 50));
        var state2 = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);
        Assert.Equal(NpcAnimationState.Eating, state2);
    }

    [Fact]
    public void AllBaselineStates_AreReachable_WithoutRandomness()
    {
        // Verify that each new state can be reached deterministically.
        var pairs = new (NpcAnimationState expected, System.Action<EntityManager, Entity> setup)[]
        {
            (NpcAnimationState.Eating,            (em, e) => e.Add(new IntendedActionComponent(IntendedActionKind.Eat,     0, DialogContextValue.None, 0))),
            (NpcAnimationState.Drinking,          (em, e) => e.Add(new IntendedActionComponent(IntendedActionKind.Drink,   0, DialogContextValue.None, 0))),
            (NpcAnimationState.DefecatingInCubicle,(em, e) => e.Add(new IntendedActionComponent(IntendedActionKind.Defecate,0, DialogContextValue.None, 0))),
            (NpcAnimationState.Working,           (em, e) => e.Add(new IntendedActionComponent(IntendedActionKind.Work,    0, DialogContextValue.None, 0))),
            (NpcAnimationState.Crying,            (em, e) => e.Add(new MoodComponent { GriefLevel = 0.9f })),
            (NpcAnimationState.CoughingFit,       (em, e) => e.Add(new IsChokingTag())),
            (NpcAnimationState.SleepingAtDesk,    (em, e) => e.Add(new EnergyComponent { Energy = 10f, IsSleeping = false })),
        };

        foreach (var (expected, setup) in pairs)
        {
            var em     = new EntityManager();
            var entity = em.CreateEntity();
            entity.Add(new NpcTag());
            setup(em, entity);

            var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);
            Assert.Equal(expected, state);
        }
    }
}
