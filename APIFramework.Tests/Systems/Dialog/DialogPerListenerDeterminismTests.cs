using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Dialog;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Dialog;

/// <summary>
/// AT-05 — determinism: two runs with identical setup produce byte-identical
/// DialogHistoryComponent state, including the new UsesByListenerAndFragmentId field.
/// </summary>
public class DialogPerListenerDeterminismTests
{
    private const string Corpus = """
        {
          "schemaVersion": "0.1.0",
          "fragments": [
            { "id": "casual-flirt-1", "text": "Hey.", "register": "casual", "context": "flirt", "valenceProfile": {}, "noteworthiness": 10 },
            { "id": "casual-flirt-2", "text": "Hi.", "register": "casual", "context": "flirt", "valenceProfile": { "affection": "mid" }, "noteworthiness": 15 },
            { "id": "casual-flirt-3", "text": "Morning.", "register": "casual", "context": "flirt", "valenceProfile": { "affection": "high" }, "noteworthiness": 20 }
          ]
        }
        """;

    private static DialogConfig Cfg() => new()
    {
        ValenceMatchScore        = 5,
        RecencyPenalty           = -10,
        CalcifyBiasScore         = 3,
        PerListenerBiasScore     = 2,
        ValenceLowMaxValue       = 33,
        ValenceMidMaxValue       = 66,
        RecencyWindowSeconds     = 300,
        TicRecognitionThreshold  = 5,
        DriveContextThreshold    = 60,
        DialogAttemptProbability = 1.0,
    };

    private static int EntityIntId(Entity e)
    {
        var b = e.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    /// <summary>
    /// Captures a snapshot of UsesByListenerAndFragmentId as a sorted string
    /// so two runs can be compared for byte-identical equality.
    /// </summary>
    private static string Snapshot(Dictionary<int, Dictionary<string, int>> map)
    {
        var lines = new List<string>();
        var sortedListeners = new List<int>(map.Keys);
        sortedListeners.Sort();
        foreach (var lid in sortedListeners)
        {
            var perFrag = map[lid];
            var sortedFrags = new List<string>(perFrag.Keys);
            sortedFrags.Sort(StringComparer.Ordinal);
            foreach (var fid in sortedFrags)
                lines.Add($"{lid}:{fid}={perFrag[fid]}");
        }
        return string.Join("|", lines);
    }

    private static (string globalSnapshot, string perListenerSnapshot) RunSimulation(int ticks)
    {
        var em      = new EntityManager();
        var bus     = new ProximityEventBus();
        var queue   = new PendingDialogQueue();
        var corpus  = new DialogCorpusService(Corpus);
        var rng     = new SeededRandom(seed: 42);
        var cfg     = Cfg();

        var decisionSys  = new DialogContextDecisionSystem(queue, bus, cfg, rng);
        var retrievalSys = new DialogFragmentRetrievalSystem(queue, corpus, bus, cfg);

        var speaker = em.CreateEntity();
        speaker.Add(new NpcTag());
        speaker.Add(new SocialDrivesComponent
        {
            Affection = new DriveValue { Current = 70 }, // drives flirt context
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

        bus.RaiseEnteredConversationRange(
            new ProximityEnteredConversationRange(speaker, listener, 0));

        for (int i = 0; i < ticks; i++)
        {
            decisionSys.Update(em, 1f);
            retrievalSys.Update(em, 1f);
        }

        var hist = speaker.Get<DialogHistoryComponent>();

        // Global snapshot
        var globalLines = new List<string>();
        var sortedFrags = new List<string>(hist.UsesByFragmentId.Keys);
        sortedFrags.Sort(StringComparer.Ordinal);
        foreach (var fid in sortedFrags)
        {
            var rec = hist.UsesByFragmentId[fid];
            globalLines.Add($"{fid}={rec.UseCount},{rec.Calcified}");
        }
        string globalSnap = string.Join("|", globalLines);

        return (globalSnap, Snapshot(hist.UsesByListenerAndFragmentId));
    }

    // AT-05: Two identical 5000-tick runs produce the same global history.
    [Fact]
    public void AT05_SameSetup_5000Ticks_GlobalHistory_IsIdentical()
    {
        var (snap1, _) = RunSimulation(5000);
        var (snap2, _) = RunSimulation(5000);

        Assert.Equal(snap1, snap2);
    }

    // AT-05b: Per-listener field is also byte-identical across two runs.
    [Fact]
    public void AT05b_SameSetup_5000Ticks_PerListenerHistory_IsIdentical()
    {
        var (_, perListener1) = RunSimulation(5000);
        var (_, perListener2) = RunSimulation(5000);

        Assert.Equal(perListener1, perListener2);
    }
}
