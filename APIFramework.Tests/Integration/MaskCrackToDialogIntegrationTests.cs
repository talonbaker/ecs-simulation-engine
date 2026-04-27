using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Dialog;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Integration;

/// <summary>
/// AT-08: A cracked NPC's IntendedAction(Dialog, MaskSlip) flows through
/// DialogContextDecisionSystem Path 1 and enqueues a PendingDialog with context "maskSlip".
/// </summary>
public class MaskCrackToDialogIntegrationTests
{
    [Fact]
    public void AT08_MaskSlipIntent_NoTargetId_EnqueuesAnyInRangeListener()
    {
        var em      = new EntityManager();
        var proxBus = new ProximityEventBus();
        var pending = new PendingDialogQueue();

        // System must be created before RaiseEnteredConversationRange so its subscription is live.
        var sys = new DialogContextDecisionSystem(pending, proxBus, new DialogConfig(), new SeededRandom(1));

        var speaker = em.CreateEntity();
        speaker.Add(new NpcTag());
        speaker.Add(new IntendedActionComponent(
            IntendedActionKind.Dialog, 0, DialogContextValue.MaskSlip, 90));

        var listener = em.CreateEntity();
        listener.Add(new NpcTag());

        proxBus.RaiseEnteredConversationRange(
            new ProximityEnteredConversationRange(speaker, listener, 0));

        sys.Update(em, 1f);

        Assert.Single(pending.Items);
        Assert.Equal(speaker,    pending.Items[0].Speaker);
        Assert.Equal(listener,   pending.Items[0].Listener);
        Assert.Equal("maskSlip", pending.Items[0].Context);
    }

    [Fact]
    public void AT08_MaskSlipIntent_WithSpecificTargetId_OnlyMatchingListenerEnqueued()
    {
        var em      = new EntityManager();
        var proxBus = new ProximityEventBus();
        var pending = new PendingDialogQueue();

        var sys = new DialogContextDecisionSystem(pending, proxBus, new DialogConfig(), new SeededRandom(1));

        var speaker       = em.CreateEntity();
        speaker.Add(new NpcTag());

        var wrongListener = em.CreateEntity();
        wrongListener.Add(new NpcTag());

        var rightListener = em.CreateEntity();
        rightListener.Add(new NpcTag());

        int rightId = WillpowerSystem.EntityIntId(rightListener);
        speaker.Add(new IntendedActionComponent(
            IntendedActionKind.Dialog, rightId, DialogContextValue.MaskSlip, 90));

        proxBus.RaiseEnteredConversationRange(
            new ProximityEnteredConversationRange(speaker, wrongListener, 0));
        proxBus.RaiseEnteredConversationRange(
            new ProximityEnteredConversationRange(speaker, rightListener, 0));

        sys.Update(em, 1f);

        Assert.Single(pending.Items);
        Assert.Equal(rightListener, pending.Items[0].Listener);
        Assert.Equal("maskSlip",    pending.Items[0].Context);
    }

    [Fact]
    public void AT08_MaskSlipIntent_SpeakerProcessedOnce_EvenIfMultipleListeners()
    {
        var em      = new EntityManager();
        var proxBus = new ProximityEventBus();
        var pending = new PendingDialogQueue();

        var sys = new DialogContextDecisionSystem(pending, proxBus, new DialogConfig(), new SeededRandom(1));

        var speaker = em.CreateEntity();
        speaker.Add(new NpcTag());
        speaker.Add(new IntendedActionComponent(
            IntendedActionKind.Dialog, 0, DialogContextValue.MaskSlip, 90));

        var listenerA = em.CreateEntity();
        listenerA.Add(new NpcTag());

        var listenerB = em.CreateEntity();
        listenerB.Add(new NpcTag());

        proxBus.RaiseEnteredConversationRange(
            new ProximityEnteredConversationRange(speaker, listenerA, 0));
        proxBus.RaiseEnteredConversationRange(
            new ProximityEnteredConversationRange(speaker, listenerB, 0));

        sys.Update(em, 1f);

        // processedSpeakers guard ensures speaker emits exactly once
        Assert.Single(pending.Items);
        Assert.Equal("maskSlip", pending.Items[0].Context);
    }
}
