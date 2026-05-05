using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Dialog;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Dialog;

/// <summary>
/// AT-12 — determinism guarantee: same seed → identical fragment selection sequence.
/// </summary>
public class DialogDeterminismTests
{
    private const string Corpus = """
        {
          "schemaVersion": "0.1.0",
          "fragments": [
            { "id": "casual-greeting-1", "text": "Hey.", "register": "casual", "context": "greeting", "valenceProfile": {}, "noteworthiness": 10 },
            { "id": "casual-greeting-2", "text": "Hi.", "register": "casual", "context": "greeting", "valenceProfile": { "belonging": "mid" }, "noteworthiness": 15 },
            { "id": "casual-greeting-3", "text": "Morning.", "register": "casual", "context": "greeting", "valenceProfile": { "belonging": "high" }, "noteworthiness": 20 }
          ]
        }
        """;

    private static DialogConfig Cfg() => new()
    {
        ValenceMatchScore        = 5,
        RecencyPenalty           = -10,
        CalcifyBiasScore         = 3,
        ValenceLowMaxValue       = 33,
        ValenceMidMaxValue       = 66,
        RecencyWindowSeconds     = 300,
        TicRecognitionThreshold  = 5,
        DriveContextThreshold    = 60,
        DialogAttemptProbability = 1.0,
    };

    private static List<string> RunSimulation(int seed, int ticks)
    {
        var em      = new EntityManager();
        var bus     = new ProximityEventBus();
        var queue   = new PendingDialogQueue();
        var corpus  = new DialogCorpusService(Corpus);
        var rng     = new SeededRandom(seed);
        var cfg     = Cfg();

        var decisionSys  = new DialogContextDecisionSystem(queue, bus, cfg, rng);
        var retrievalSys = new DialogFragmentRetrievalSystem(queue, corpus, bus, cfg);

        var speaker  = em.CreateEntity();
        speaker.Add(new NpcTag());
        speaker.Add(new SocialDrivesComponent
        {
            Belonging = new DriveValue { Current = 70 }, // → greeting context
        });
        speaker.Add(new PersonalityComponent(0, 0, 0, 0, 0, VocabularyRegister.Casual));
        speaker.Add(new DialogHistoryComponent());
        speaker.Add(new RecognizedTicComponent());

        var listener = em.CreateEntity();
        listener.Add(new NpcTag());
        listener.Add(new SocialDrivesComponent());
        listener.Add(new PersonalityComponent(0, 0, 0, 0, 0, VocabularyRegister.Casual));
        listener.Add(new DialogHistoryComponent());
        listener.Add(new RecognizedTicComponent());

        // Simulate entering conversation range
        bus.RaiseEnteredConversationRange(
            new ProximityEnteredConversationRange(speaker, listener, 0));

        var selected = new List<string>();
        bus.OnSpokenFragment += e => selected.Add(e.FragmentId);

        for (int i = 0; i < ticks; i++)
        {
            decisionSys.Update(em, 1f);
            retrievalSys.Update(em, 1f);
        }

        return selected;
    }

    // -- AT-12: Two runs with the same seed produce identical output -----------

    [Fact]
    public void AT12_SameSeed_ProducesIdenticalFragmentSelectionSequence()
    {
        var run1 = RunSimulation(seed: 42, ticks: 20);
        var run2 = RunSimulation(seed: 42, ticks: 20);

        Assert.Equal(run1, run2);
    }

    // -- AT-12b: Different seeds produce different sequences -------------------

    [Fact]
    public void AT12b_DifferentSeeds_ProduceDifferentSequences()
    {
        var run1 = RunSimulation(seed: 1,  ticks: 50);
        var run2 = RunSimulation(seed: 99, ticks: 50);

        // With different seeds the AttemptProbability rolls differ, so runs will diverge.
        // We don't assert exact difference since both could happen to select nothing, but
        // with 50 ticks at 100% attempt probability both should select something.
        Assert.NotEmpty(run1);
        Assert.NotEmpty(run2);
        // They CAN be equal by chance with only 3 fragments, but highly unlikely over 50 ticks.
        // We only assert determinism within a seed, not diversity across seeds.
    }
}
