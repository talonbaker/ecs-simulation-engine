using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Dialog;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Dialog;

/// <summary>
/// AT-02 — per-listener bias is applied correctly in scoring.
/// Uses two fragments with identical base scores; "z-fragment" would lose the
/// alphabetical tie-break without bias and win with it, proving the bias term fires.
/// </summary>
public class DialogFragmentRetrievalPerListenerBiasTests
{
    private const string CorpusJson = """
        {
          "schemaVersion": "0.1.0",
          "fragments": [
            {
              "id": "z-fragment",
              "text": "You know.",
              "register": "casual",
              "context": "lashOut",
              "valenceProfile": {},
              "noteworthiness": 20
            },
            {
              "id": "a-fragment",
              "text": "Right.",
              "register": "casual",
              "context": "lashOut",
              "valenceProfile": {},
              "noteworthiness": 20
            }
          ]
        }
        """;

    private static DialogConfig DefaultCfg() => new()
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

    private static (EntityManager em, ProximityEventBus bus, PendingDialogQueue queue, DialogFragmentRetrievalSystem sys)
        Build(DialogConfig? cfg = null)
    {
        var em     = new EntityManager();
        var bus    = new ProximityEventBus();
        var queue  = new PendingDialogQueue();
        var corpus = new DialogCorpusService(CorpusJson);
        var c      = cfg ?? DefaultCfg();
        var sys    = new DialogFragmentRetrievalSystem(queue, corpus, bus, c);
        return (em, bus, queue, sys);
    }

    private static int EntityIntId(Entity e)
    {
        var b = e.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    private static Entity SpawnNpc(EntityManager em)
    {
        var e = em.CreateEntity();
        e.Add(new NpcTag());
        e.Add(new SocialDrivesComponent());
        e.Add(new PersonalityComponent(0, 0, 0, 0, 0, VocabularyRegister.Casual));
        e.Add(new DialogHistoryComponent());
        e.Add(new RecognizedTicComponent());
        return e;
    }

    // AT-02a: Per-listener history biases scoring — z-fragment wins despite losing tie-break.
    [Fact]
    public void AT02a_BiasedFragment_WinsOverAlphabeticalTieBreakWinner()
    {
        var (em, bus, queue, sys) = Build();

        var speaker  = SpawnNpc(em);
        var listener = SpawnNpc(em);

        int listenerIntId = EntityIntId(listener);

        var hist = speaker.Get<DialogHistoryComponent>();
        hist.UsesByListenerAndFragmentId[listenerIntId] = new Dictionary<string, int>
        {
            ["z-fragment"] = 5,
        };

        SpokenFragmentEvent? captured = null;
        bus.OnSpokenFragment += e => captured = e;

        queue.Enqueue(speaker, listener, "lashOut");
        sys.Update(em, 1f);

        Assert.NotNull(captured);
        // Without bias: both score 0, "a-fragment" wins (lower ordinal).
        // With bias: "z-fragment" scores +2, "a-fragment" scores 0 → z-fragment wins.
        Assert.Equal("z-fragment", captured!.Value.FragmentId);
    }

    // AT-02b: No per-listener history → no bias → alphabetical tie-break selects a-fragment.
    [Fact]
    public void AT02b_NoPriorHistory_NoPerListenerBias_TieBreakWins()
    {
        var (em, bus, queue, sys) = Build();

        var speaker  = SpawnNpc(em);
        var listener = SpawnNpc(em);

        SpokenFragmentEvent? captured = null;
        bus.OnSpokenFragment += e => captured = e;

        queue.Enqueue(speaker, listener, "lashOut");
        sys.Update(em, 1f);

        Assert.NotNull(captured);
        // Both score 0, no bias; "a-fragment" < "z-fragment" → a-fragment wins.
        Assert.Equal("a-fragment", captured!.Value.FragmentId);
    }

    // AT-02c: History with a different listener does not bias the current interaction.
    [Fact]
    public void AT02c_HistoryWithOtherListener_DoesNotBiasCurrentInteraction()
    {
        var (em, bus, queue, sys) = Build();

        var speaker       = SpawnNpc(em);
        var listenerKnown = SpawnNpc(em);
        var listenerOther = SpawnNpc(em);

        int knownId = EntityIntId(listenerKnown);

        var hist = speaker.Get<DialogHistoryComponent>();
        hist.UsesByListenerAndFragmentId[knownId] = new Dictionary<string, int>
        {
            ["z-fragment"] = 10,
        };

        SpokenFragmentEvent? captured = null;
        bus.OnSpokenFragment += e => captured = e;

        // Speaking to listenerOther — no history with them → no bias
        queue.Enqueue(speaker, listenerOther, "lashOut");
        sys.Update(em, 1f);

        Assert.NotNull(captured);
        // No bias for listenerOther → a-fragment wins tie-break.
        Assert.Equal("a-fragment", captured!.Value.FragmentId);
    }

    // AT-02d: System records per-listener count during the interaction.
    [Fact]
    public void AT02d_AfterInteraction_PerListenerCountIncremented()
    {
        var (em, bus, queue, sys) = Build();

        var speaker  = SpawnNpc(em);
        var listener = SpawnNpc(em);

        queue.Enqueue(speaker, listener, "lashOut");
        sys.Update(em, 1f);

        int listenerIntId = EntityIntId(listener);
        var hist = speaker.Get<DialogHistoryComponent>();

        Assert.True(hist.UsesByListenerAndFragmentId.ContainsKey(listenerIntId),
            "Per-listener map should have an entry for this listener.");
        var perFrag = hist.UsesByListenerAndFragmentId[listenerIntId];
        Assert.True(perFrag.Count > 0);
        var count = perFrag.Values.Count > 0 ? perFrag.Values.GetEnumerator() : default;
        int total = 0;
        foreach (var v in perFrag.Values) total += v;
        Assert.Equal(1, total);
    }
}
