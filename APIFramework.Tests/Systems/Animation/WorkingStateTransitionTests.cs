using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Animation;
using Xunit;

namespace APIFramework.Tests.Systems.Animation;

/// <summary>AT-06: Work intent + at desk → Working (replaces old Sit mapping).</summary>
public class WorkingStateTransitionTests
{
    [Fact]
    public void WorkIntent_ResolvesToWorking()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new IntendedActionComponent(IntendedActionKind.Work, 0, DialogContextValue.None, 0));

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.Equal(NpcAnimationState.Working, state);
    }

    [Fact]
    public void WorkIntent_IsNotSit()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new IntendedActionComponent(IntendedActionKind.Work, 0, DialogContextValue.None, 0));

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.NotEqual(NpcAnimationState.Sit, state);
    }
}
