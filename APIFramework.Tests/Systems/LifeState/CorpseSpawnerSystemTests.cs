using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.LifeState;

/// <summary>
/// AT-01: Death narrative event → CorpseTag attached to the deceased entity.
/// AT-02: Death narrative event → CorpseComponent attached with correct OriginalNpcEntityId.
/// AT-03: Re-emitting the same death event is idempotent (no duplicate tag/component).
/// AT-04: Non-death narrative events are ignored.
/// AT-05: CauseOfDeathComponent.LocationRoomId is mirrored into CorpseComponent when present.
/// </summary>
public class CorpseSpawnerSystemTests
{
    // -- Helpers ---------------------------------------------------------------

    private static int EntityIntId(Entity e)
    {
        var b = e.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    private static (EntityManager em, NarrativeEventBus bus, Entity deceased) Build()
    {
        var em  = new EntityManager();
        var bus = new NarrativeEventBus();

        var deceased = em.CreateEntity();
        deceased.Add(new NpcTag());
        deceased.Add(new LifeStateComponent { State = LS.Deceased });

        return (em, bus, deceased);
    }

    private static NarrativeEventCandidate DeathEvent(Entity deceased, long tick = 1)
        => new(
            Tick:           tick,
            Kind:           NarrativeEventKind.Choked,
            ParticipantIds: new[] { EntityIntId(deceased) },
            RoomId:         null,
            Detail:         "test death");

    // -- AT-01: CorpseTag attached ---------------------------------------------

    [Fact]
    public void AT01_DeathEvent_AttachesCorpseTag()
    {
        var (em, bus, deceased) = Build();
        _ = new CorpseSpawnerSystem(bus, em);

        bus.RaiseCandidate(DeathEvent(deceased));

        Assert.True(deceased.Has<CorpseTag>());
    }

    // -- AT-02: CorpseComponent attached with correct metadata -----------------

    [Fact]
    public void AT02_DeathEvent_AttachesCorpseComponent_WithCorrectEntityId()
    {
        var (em, bus, deceased) = Build();
        _ = new CorpseSpawnerSystem(bus, em);

        bus.RaiseCandidate(DeathEvent(deceased, tick: 42));

        Assert.True(deceased.Has<CorpseComponent>());
        var c = deceased.Get<CorpseComponent>();
        Assert.Equal(deceased.Id, c.OriginalNpcEntityId);
        Assert.Equal(42L, c.DeathTick);
    }

    // -- AT-03: Idempotent — re-emitting same event changes nothing ------------

    [Fact]
    public void AT03_ReEmitDeathEvent_IsIdempotent_NoSecondAttach()
    {
        var (em, bus, deceased) = Build();
        _ = new CorpseSpawnerSystem(bus, em);

        var ev = DeathEvent(deceased);
        bus.RaiseCandidate(ev);
        bus.RaiseCandidate(ev); // second emit — should be a no-op

        // Still exactly one CorpseTag (no exception from Add<CorpseTag> twice)
        Assert.True(deceased.Has<CorpseTag>());

        // CorpseComponent.HasBeenMoved should still be its initial value (false)
        Assert.False(deceased.Get<CorpseComponent>().HasBeenMoved);
    }

    // -- AT-04: Non-death events are ignored -----------------------------------

    [Fact]
    public void AT04_NonDeathEvent_DoesNotAttachCorpseTag()
    {
        var (em, bus, deceased) = Build();
        _ = new CorpseSpawnerSystem(bus, em);

        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           1,
            Kind:           NarrativeEventKind.MaskSlip,
            ParticipantIds: new[] { EntityIntId(deceased) },
            RoomId:         null,
            Detail:         "not a death"));

        Assert.False(deceased.Has<CorpseTag>());
    }

    // -- AT-05: CauseOfDeathComponent.LocationRoomId mirrored -----------------

    [Fact]
    public void AT05_CauseOfDeathComponent_LocationRoomId_MirroredToCorpseComponent()
    {
        var (em, bus, deceased) = Build();
        _ = new CorpseSpawnerSystem(bus, em);

        var roomId = Guid.NewGuid();
        deceased.Add(new CauseOfDeathComponent
        {
            Cause        = CauseOfDeath.Choked,
            DeathTick    = 99,
            LocationRoomId = roomId,
        });

        bus.RaiseCandidate(DeathEvent(deceased));

        var c = deceased.Get<CorpseComponent>();
        Assert.Equal(roomId.ToString(), c.LocationRoomId);
        Assert.Equal(99L, c.DeathTick);
    }
}
