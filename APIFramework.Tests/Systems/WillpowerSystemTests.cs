using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// Tests for WillpowerSystem: suppression depletion, rest recovery, clamp behaviour,
/// and sleep-regen integration.
/// </summary>
public class WillpowerSystemTests
{
    private static SocialSystemConfig Cfg(int sleepRegen = 1) => new()
    {
        WillpowerSleepRegenPerTick = sleepRegen
    };

    private static (EntityManager em, Entity entity, WillpowerEventQueue queue)
        BuildNpc(int current = 80, int baseline = 80)
    {
        var em    = new EntityManager();
        var e     = em.CreateEntity();
        e.Add(new NpcTag());
        e.Add(new WillpowerComponent(current, baseline));
        var queue = new WillpowerEventQueue();
        return (em, e, queue);
    }

    // AT-05: suppression event reduces Current by magnitude, clamped at 0
    [Fact]
    public void SuppressionTick_ReducesCurrent()
    {
        var (em, entity, queue) = BuildNpc(current: 50);
        int entityId = WillpowerSystem.EntityIntId(entity);

        queue.Enqueue(new WillpowerEventSignal(entityId, WillpowerEventKind.SuppressionTick, 5));

        new WillpowerSystem(Cfg(), queue).Update(em, 1f);

        Assert.Equal(45, entity.Get<WillpowerComponent>().Current);
    }

    [Fact]
    public void SuppressionTick_ClampsAt0()
    {
        var (em, entity, queue) = BuildNpc(current: 3);
        int entityId = WillpowerSystem.EntityIntId(entity);

        queue.Enqueue(new WillpowerEventSignal(entityId, WillpowerEventKind.SuppressionTick, 10));

        new WillpowerSystem(Cfg(), queue).Update(em, 1f);

        Assert.Equal(0, entity.Get<WillpowerComponent>().Current);
    }

    // AT-06: SleepingTag triggers RestTick; over N ticks Current rises by N * regenPerTick
    [Fact]
    public void SleepingTag_RegensWillpowerPerTick()
    {
        const int startWp = 40;
        const int regen   = 1;
        const int ticks   = 10;

        var (em, entity, queue) = BuildNpc(current: startWp);
        entity.Add(new SleepingTag());

        var sys = new WillpowerSystem(Cfg(regen), queue);
        for (int i = 0; i < ticks; i++)
            sys.Update(em, 1f);

        int expected = System.Math.Clamp(startWp + ticks * regen, 0, 100);
        Assert.Equal(expected, entity.Get<WillpowerComponent>().Current);
    }

    [Fact]
    public void RestTick_ClampsAt100()
    {
        var (em, entity, queue) = BuildNpc(current: 98);
        int entityId = WillpowerSystem.EntityIntId(entity);

        queue.Enqueue(new WillpowerEventSignal(entityId, WillpowerEventKind.RestTick, 10));

        new WillpowerSystem(Cfg(), queue).Update(em, 1f);

        Assert.Equal(100, entity.Get<WillpowerComponent>().Current);
    }

    [Fact]
    public void SignalForUnknownEntity_Ignored()
    {
        var (em, entity, queue) = BuildNpc(current: 50);
        // Signal for entity ID 9999 — doesn't exist
        queue.Enqueue(new WillpowerEventSignal(9999, WillpowerEventKind.SuppressionTick, 5));

        new WillpowerSystem(Cfg(), queue).Update(em, 1f);

        // Entity should be unaffected
        Assert.Equal(50, entity.Get<WillpowerComponent>().Current);
    }

    [Fact]
    public void MultipleSignals_AppliedInOrder()
    {
        var (em, entity, queue) = BuildNpc(current: 60);
        int entityId = WillpowerSystem.EntityIntId(entity);

        queue.Enqueue(new WillpowerEventSignal(entityId, WillpowerEventKind.SuppressionTick, 10));
        queue.Enqueue(new WillpowerEventSignal(entityId, WillpowerEventKind.RestTick,        5));

        new WillpowerSystem(Cfg(), queue).Update(em, 1f);

        // 60 - 10 + 5 = 55
        Assert.Equal(55, entity.Get<WillpowerComponent>().Current);
    }

    [Fact]
    public void NonSleepingNpc_NoAutoRegen()
    {
        var (em, entity, queue) = BuildNpc(current: 40);
        // No SleepingTag

        var sys = new WillpowerSystem(Cfg(), queue);
        sys.Update(em, 1f);

        Assert.Equal(40, entity.Get<WillpowerComponent>().Current);
    }
}
