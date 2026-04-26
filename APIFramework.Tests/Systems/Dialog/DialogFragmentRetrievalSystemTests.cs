using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Dialog;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Dialog;

/// <summary>
/// AT-05 through AT-08 — fragment scoring and selection.
/// </summary>
public class DialogFragmentRetrievalSystemTests
{
    // ── Corpus with two fragments in casual×lashOut ───────────────────────────

    private const string TwoFragmentCorpus = """
        {
          "schemaVersion": "0.1.0",
          "fragments": [
            {
              "id": "casual-lashOut-A",
              "text": "Oh, give it a rest.",
              "register": "casual",
              "context": "lashOut",
              "valenceProfile": { "irritation": "high" },
              "noteworthiness": 30
            },
            {
              "id": "casual-lashOut-B",
              "text": "Can you just not?",
              "register": "casual",
              "context": "lashOut",
              "valenceProfile": { "irritation": "mid" },
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
        ValenceLowMaxValue       = 33,
        ValenceMidMaxValue       = 66,
        RecencyWindowSeconds     = 300,
        TicRecognitionThreshold  = 5,
        DriveContextThreshold    = 60,
        DialogAttemptProbability = 1.0, // always attempt in tests
    };

    private static (EntityManager em,
                    ProximityEventBus bus,
                    PendingDialogQueue queue,
                    DialogFragmentRetrievalSystem sys,
                    DialogCorpusService corpus)
        Build(string corpusJson, DialogConfig? cfg = null)
    {
        var em     = new EntityManager();
        var bus    = new ProximityEventBus();
        var queue  = new PendingDialogQueue();
        var corpus = new DialogCorpusService(corpusJson);
        var c      = cfg ?? DefaultCfg();
        var sys    = new DialogFragmentRetrievalSystem(queue, corpus, bus, c);
        return (em, bus, queue, sys, corpus);
    }

    private static Entity SpawnNpc(EntityManager em,
                                   SocialDrivesComponent? drives = null,
                                   VocabularyRegister register   = VocabularyRegister.Casual)
    {
        var e = em.CreateEntity();
        e.Add(new NpcTag());
        e.Add(drives ?? new SocialDrivesComponent());
        e.Add(new PersonalityComponent(0, 0, 0, 0, 0, register));
        e.Add(new DialogHistoryComponent());
        e.Add(new RecognizedTicComponent());
        return e;
    }

    // ── AT-05: Fragment is selected and event is emitted ─────────────────────

    [Fact]
    public void AT05_SpokenFragmentEvent_EmittedForPendingDialog()
    {
        var (em, bus, queue, sys, _) = Build(TwoFragmentCorpus);

        SpokenFragmentEvent? captured = null;
        bus.OnSpokenFragment += e => captured = e;

        var speaker  = SpawnNpc(em);
        var listener = SpawnNpc(em);
        queue.Enqueue(speaker, listener, "lashOut");

        sys.Update(em, 1f);

        Assert.NotNull(captured);
        Assert.Equal(speaker,  captured!.Value.SpeakerId);
        Assert.Equal(listener, captured!.Value.ListenerId);
    }

    // ── AT-06: Valence scoring selects the irritation-high fragment ───────────

    [Fact]
    public void AT06_HighIrritationSpeaker_SelectsIrritationHighFragment()
    {
        var (em, bus, queue, sys, _) = Build(TwoFragmentCorpus);

        SpokenFragmentEvent? captured = null;
        bus.OnSpokenFragment += e => captured = e;

        // Irritation = 90 → maps to "high" ordinal
        var drives = new SocialDrivesComponent { Irritation = new DriveValue { Current = 90 } };
        var speaker  = SpawnNpc(em, drives);
        var listener = SpawnNpc(em);
        queue.Enqueue(speaker, listener, "lashOut");

        sys.Update(em, 1f);

        Assert.NotNull(captured);
        // casual-lashOut-A has irritation:high → wins +5 valence score over lashOut-B (+0)
        Assert.Equal("casual-lashOut-A", captured!.Value.FragmentId);
    }

    // ── AT-07: Recency penalty suppresses recently-used fragment ─────────────

    [Fact]
    public void AT07_RecentlyUsedFragment_GetsRecencyPenalty()
    {
        var cfg = DefaultCfg();
        cfg.RecencyWindowSeconds = 300;

        var (em, bus, queue, sys, _) = Build(TwoFragmentCorpus, cfg);

        // Seed history so casual-lashOut-A was used 5 game-seconds ago (within 300-s window).
        var speaker  = SpawnNpc(em);
        var listener = SpawnNpc(em);

        var hist = speaker.Get<DialogHistoryComponent>();
        hist.UsesByFragmentId["casual-lashOut-A"] = new FragmentUseRecord
        {
            UseCount           = 1,
            FirstUseTick       = 1,
            LastUseTick        = 1,
            LastUseGameTimeSec = 0.0, // sys._gameTimeSec starts at 0; 1f deltaTime → 1.0
            DominantContext    = "lashOut",
        };

        // Drives: irritation = 90 → would normally select A (+5), but A has recency (-10) → net -5
        //         B has no valence match → 0, which beats -5.
        var drives = new SocialDrivesComponent { Irritation = new DriveValue { Current = 90 } };
        // We need to re-add the entity with updated drives
        var speakerWithDrives = em.CreateEntity();
        speakerWithDrives.Add(new NpcTag());
        speakerWithDrives.Add(drives);
        speakerWithDrives.Add(new PersonalityComponent(0, 0, 0, 0, 0, VocabularyRegister.Casual));
        speakerWithDrives.Add(hist);
        speakerWithDrives.Add(new RecognizedTicComponent());

        SpokenFragmentEvent? captured = null;
        bus.OnSpokenFragment += e => captured = e;

        queue.Enqueue(speakerWithDrives, listener, "lashOut");
        sys.Update(em, 1f); // gameTimeSec = 1.0; lastUse = 0.0; window = 300 → inside window

        Assert.NotNull(captured);
        // A: +5 (valence) - 10 (recency) = -5; B: 0 → B wins
        Assert.Equal("casual-lashOut-B", captured!.Value.FragmentId);
    }

    // ── AT-08: CalcifyBiasScore boosts a calcified fragment ──────────────────

    [Fact]
    public void AT08_CalcifiedFragment_ReceivesBiasBonus()
    {
        var cfg = DefaultCfg();
        cfg.CalcifyBiasScore = 10; // large enough to dominate

        var (em, bus, queue, sys, _) = Build(TwoFragmentCorpus, cfg);

        SpokenFragmentEvent? captured = null;
        bus.OnSpokenFragment += e => captured = e;

        // Speaker has no elevated irritation; without bias A and B score equally at 0.
        // We calcify B so it gets +10 → B should win (A scores 0, B scores 10).
        var speaker  = em.CreateEntity();
        speaker.Add(new NpcTag());
        speaker.Add(new SocialDrivesComponent());
        speaker.Add(new PersonalityComponent(0, 0, 0, 0, 0, VocabularyRegister.Casual));
        var hist = new DialogHistoryComponent();
        hist.UsesByFragmentId["casual-lashOut-B"] = new FragmentUseRecord
        {
            UseCount           = 10,
            FirstUseTick       = 1,
            LastUseTick        = 1,
            LastUseGameTimeSec = -999.0, // far in the past — no recency penalty
            DominantContext    = "lashOut",
            Calcified          = true,
        };
        speaker.Add(hist);
        speaker.Add(new RecognizedTicComponent());

        var listener = SpawnNpc(em);
        queue.Enqueue(speaker, listener, "lashOut");

        sys.Update(em, 1f);

        Assert.NotNull(captured);
        Assert.Equal("casual-lashOut-B", captured!.Value.FragmentId);
    }

    // ── AT-05b: History is updated after selection ────────────────────────────

    [Fact]
    public void AT05b_AfterSelection_HistoryUseCountIncremented()
    {
        var (em, bus, queue, sys, _) = Build(TwoFragmentCorpus);

        var speaker  = SpawnNpc(em);
        var listener = SpawnNpc(em);
        queue.Enqueue(speaker, listener, "lashOut");

        sys.Update(em, 1f);

        var hist = speaker.Get<DialogHistoryComponent>();
        Assert.NotEmpty(hist.UsesByFragmentId);
        var rec = hist.UsesByFragmentId.Values.First();
        Assert.Equal(1, rec.UseCount);
    }

    // ── AT-05c: Tic hearing count incremented on listener ────────────────────

    [Fact]
    public void AT05c_AfterSelection_ListenerHearingCountIncremented()
    {
        var (em, bus, queue, sys, _) = Build(TwoFragmentCorpus);

        var speaker  = SpawnNpc(em);
        var listener = SpawnNpc(em);
        queue.Enqueue(speaker, listener, "lashOut");

        SpokenFragmentEvent? captured = null;
        bus.OnSpokenFragment += e => captured = e;

        sys.Update(em, 1f);

        Assert.NotNull(captured);
        var tic    = listener.Get<RecognizedTicComponent>();
        int speakerId = captured!.Value.SpeakerId.Id.ToByteArray()[0]; // just check something was recorded
        Assert.True(tic.HearingCounts.Count > 0);
    }
}
