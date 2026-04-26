using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Dialog;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Integration;

/// <summary>
/// AT-08: NPC with high Irritation produces IntendedAction(Dialog, LashOut/Complain).
/// DialogContextDecisionSystem reads the intent via Path 1 and enqueues a PendingDialog.
/// DialogFragmentRetrievalSystem selects a register-appropriate fragment and fires SpokenFragmentEvent.
/// </summary>
public class ActionSelectionToDialogTests
{
    // Minimal schema-valid corpus with casual lashOut + complaint fragments.
    private const string MinimalCorpusJson = """
        {
          "schemaVersion": "0.1.0",
          "fragments": [
            {
              "id": "lo-casual-001",
              "text": "Back off.",
              "register": "casual",
              "context": "lashOut",
              "valenceProfile": { "irritation": "high" },
              "noteworthiness": 1
            },
            {
              "id": "cp-casual-001",
              "text": "This is really frustrating.",
              "register": "casual",
              "context": "complaint",
              "valenceProfile": { "irritation": "high" },
              "noteworthiness": 1
            }
          ]
        }
        """;

    private static ActionSelectionConfig DefaultActionCfg() => new()
    {
        DriveCandidateThreshold        = 60,
        IdleScoreFloor                 = 0.20,
        InversionStakeThreshold        = 0.55,
        InversionInhibitionThreshold   = 0.50,
        SuppressionGiveUpFactor        = 0.30,
        SuppressionEpsilon             = 0.10,
        SuppressionEventMagnitudeScale = 5,
        PersonalityTieBreakWeight      = 0.05,
        MaxCandidatesPerTick           = 32,
        AvoidStandoffDistance          = 4
    };

    private static GridSpatialIndex MakeSpatial() =>
        new(new SpatialConfig { CellSizeTiles = 4, WorldSize = new() { Width = 64, Height = 64 } });

    [Fact]
    public void AT08_HighIrritation_LashOutOrComplain_FlowsThrough_DialogPipeline()
    {
        var em      = new EntityManager();
        var spatial = MakeSpatial();
        var wq      = new WillpowerEventQueue();
        var bus     = new ProximityEventBus();
        var pending = new PendingDialogQueue();

        // Listener / target — must be an NPC in proximity
        var coworker = em.CreateEntity();
        coworker.Add(new NpcTag());
        coworker.Add(new PositionComponent { X = 6f, Y = 0f, Z = 5f });
        spatial.Register(coworker, 6, 5);

        // Speaker — high Irritation, no inhibitions, Casual register
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        npc.Add(new WillpowerComponent(50, 50));
        npc.Add(new SocialDrivesComponent
        {
            Irritation = new DriveValue { Current = 80, Baseline = 80 }
        });
        npc.Add(new InhibitionsComponent(Array.Empty<Inhibition>()));
        npc.Add(new PersonalityComponent { VocabularyRegister = VocabularyRegister.Casual });
        spatial.Register(npc, 5, 5);

        var corpus = new DialogCorpusService(MinimalCorpusJson);
        var actionSys   = new ActionSelectionSystem(
            spatial, new EntityRoomMembership(), wq,
            new SeededRandom(42), DefaultActionCfg(), em);
        var dialogDecSys = new DialogContextDecisionSystem(pending, bus, new DialogConfig(), new SeededRandom(99));
        var fragRetrSys  = new DialogFragmentRetrievalSystem(pending, corpus, bus, new DialogConfig());

        // Prime the in-range set so DialogContextDecisionSystem processes this pair.
        bus.RaiseEnteredConversationRange(new ProximityEnteredConversationRange(npc, coworker, 0));

        var spoken = new List<SpokenFragmentEvent>();
        bus.OnSpokenFragment += e => spoken.Add(e);

        bool emitted = false;
        for (int t = 0; t < 50 && !emitted; t++)
        {
            actionSys.Update(em, 1f);
            dialogDecSys.Update(em, 1f);
            fragRetrSys.Update(em, 1f);

            if (spoken.Count > 0) emitted = true;
        }

        Assert.True(emitted,
            "Expected a SpokenFragmentEvent from the LashOut/Complain dialog chain within 50 ticks.");

        // Speaker and listener should match the NPC pair.
        Assert.Equal(npc,      spoken[0].SpeakerId);
        Assert.Equal(coworker, spoken[0].ListenerId);

        // Fragment must come from the corpus (lashOut or complaint context).
        Assert.True(
            spoken[0].FragmentId == "lo-casual-001" || spoken[0].FragmentId == "cp-casual-001",
            $"Unexpected fragment id: {spoken[0].FragmentId}");
    }
}
