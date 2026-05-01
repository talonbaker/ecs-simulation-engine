using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Animation;
using Xunit;

namespace APIFramework.Tests.Systems.Animation;

/// <summary>AT-04: Defecate intent → animator state DefecatingInCubicle.</summary>
public class DefecatingInCubicleTransitionTests
{
    [Fact]
    public void DefecateIntent_ResolvesToDefecatingInCubicle()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new IntendedActionComponent(IntendedActionKind.Defecate, 0, DialogContextValue.None, 0));

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.Equal(NpcAnimationState.DefecatingInCubicle, state);
    }

    [Fact]
    public void NoDefecateIntent_DoesNotResolveToDefecating()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new IntendedActionComponent(IntendedActionKind.Work, 0, DialogContextValue.None, 0));

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.NotEqual(NpcAnimationState.DefecatingInCubicle, state);
    }
}
