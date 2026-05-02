using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Animation;
using Xunit;

namespace APIFramework.Tests.Systems.Animation;

/// <summary>AT-07: High grief → Crying.</summary>
public class CryingStateTransitionTests
{
    [Fact]
    public void HighGriefLevel_ResolvesToCrying()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new MoodComponent { GriefLevel = 0.75f });

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.Equal(NpcAnimationState.Crying, state);
    }

    [Fact]
    public void GriefAtThreshold_ResolvesToCrying()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new MoodComponent { GriefLevel = 0.70f });

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.Equal(NpcAnimationState.Crying, state);
    }

    [Fact]
    public void LowGriefLevel_DoesNotResolveTocryin()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new MoodComponent { GriefLevel = 0.50f });

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.NotEqual(NpcAnimationState.Crying, state);
    }

    [Fact]
    public void HighGrief_ButPanicking_DoesNotOverridePanic()
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new NpcTag());
        entity.Add(new MoodComponent { GriefLevel = 0.90f, PanicLevel = 0.80f });

        var state = AnimationStateResolver.Resolve(entity, isDtoMoving: false, isDtoSleeping: false, em);

        Assert.Equal(NpcAnimationState.Panic, state);
    }
}
