using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Dialog;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Dialog;

/// <summary>
/// AT-03 — Affair-archetype statistical test: per-listener fragment-use distributions diverge
/// across 50 dialog moments. Chi-square p &lt; 0.01 (df=1, threshold 6.635) verifies that
/// listener identity meaningfully predicts which fragment an NPC reaches for.
/// </summary>
public class DialogCodeSwitchingScenarioTests
{
    // Two fragments with equal base scores and no valence profile.
    // "a-paired-fragment" wins alphabetical tie-breaks; "z-paired-fragment" loses them.
    private const string CorpusJson = """
        {
          "schemaVersion": "0.1.0",
          "fragments": [
            {
              "id": "z-paired-fragment",
              "text": "You get me.",
              "register": "casual",
              "context": "flirt",
              "valenceProfile": {},
              "noteworthiness": 30
            },
            {
              "id": "a-paired-fragment",
              "text": "Sure.",
              "register": "casual",
              "context": "flirt",
              "valenceProfile": {},
              "noteworthiness": 10
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
        RecencyWindowSeconds     = 0,   // no recency penalty — keeps scoring clean
        TicRecognitionThreshold  = 5,
        DriveContextThreshold    = 60,
        DialogAttemptProbability = 1.0,
    };

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

    /// <summary>
    /// Chi-square statistic for a 2×2 contingency table.
    /// |  obs_11  obs_12  |   row 1 = L1
    /// |  obs_21  obs_22  |   row 2 = L2
    /// col 1 = target fragment, col 2 = other fragments.
    /// </summary>
    private static double ChiSquare2x2(int obs11, int obs12, int obs21, int obs22)
    {
        int n    = obs11 + obs12 + obs21 + obs22;
        int row1 = obs11 + obs12;
        int row2 = obs21 + obs22;
        int col1 = obs11 + obs21;
        int col2 = obs12 + obs22;

        if (n == 0 || row1 == 0 || row2 == 0 || col1 == 0 || col2 == 0)
            return 0.0;

        double e11 = (double)(row1 * col1) / n;
        double e12 = (double)(row1 * col2) / n;
        double e21 = (double)(row2 * col1) / n;
        double e22 = (double)(row2 * col2) / n;

        return
            (obs11 - e11) * (obs11 - e11) / e11 +
            (obs12 - e12) * (obs12 - e12) / e12 +
            (obs21 - e21) * (obs21 - e21) / e21 +
            (obs22 - e22) * (obs22 - e22) / e22;
    }

    // AT-03: Per-listener distributions diverge significantly (chi-square p < 0.01).
    [Fact]
    public void AT03_AffairArchetype_PerListenerDistributions_DivergeSiginificantly()
    {
        const int MomentsPerListener = 25; // 50 total
        const double ChiSquareP001Df1 = 6.635;

        var em     = new EntityManager();
        var bus    = new ProximityEventBus();
        var queue  = new PendingDialogQueue();
        var corpus = new DialogCorpusService(CorpusJson);
        var cfg    = DefaultCfg();
        var sys    = new DialogFragmentRetrievalSystem(queue, corpus, bus, cfg);

        var speaker    = SpawnNpc(em);
        var partnerL1  = SpawnNpc(em);  // affair partner — "z-paired-fragment" biased here
        var colleagueL2 = SpawnNpc(em); // public context — "a-paired-fragment" biased here

        int l1Id = EntityIntId(partnerL1);
        int l2Id = EntityIntId(colleagueL2);

        // Pre-seed per-listener history to simulate established per-pair phrasings.
        // z-paired-fragment biased with L1 (the affair partner).
        // a-paired-fragment biased with L2 (public colleagues).
        var hist = speaker.Get<DialogHistoryComponent>();
        hist.UsesByListenerAndFragmentId[l1Id] = new Dictionary<string, int>
        {
            ["z-paired-fragment"] = 8,
        };
        hist.UsesByListenerAndFragmentId[l2Id] = new Dictionary<string, int>
        {
            ["a-paired-fragment"] = 8,
        };

        var l1Counts = new Dictionary<string, int>();
        var l2Counts = new Dictionary<string, int>();

        bus.OnSpokenFragment += evt =>
        {
            bool isL1 = evt.ListenerId.Id == partnerL1.Id;
            var bucket = isL1 ? l1Counts : l2Counts;
            bucket.TryGetValue(evt.FragmentId, out int c);
            bucket[evt.FragmentId] = c + 1;
        };

        // 25 moments with partnerL1, 25 with colleagueL2 (interleaved to avoid recency bias)
        for (int i = 0; i < MomentsPerListener; i++)
        {
            queue.Enqueue(speaker, partnerL1,   "flirt");
            queue.Enqueue(speaker, colleagueL2, "flirt");
            sys.Update(em, 1f);
            queue.Clear();
        }

        // Count how many times "z-paired-fragment" was used with each listener
        l1Counts.TryGetValue("z-paired-fragment", out int zForL1);
        l2Counts.TryGetValue("z-paired-fragment", out int zForL2);
        int notZForL1 = MomentsPerListener - zForL1;
        int notZForL2 = MomentsPerListener - zForL2;

        double chiSq = ChiSquare2x2(zForL1, notZForL1, zForL2, notZForL2);

        Assert.True(chiSq > ChiSquareP001Df1,
            $"Chi-square {chiSq:F3} should exceed {ChiSquareP001Df1} (p<0.01, df=1). " +
            $"z-for-L1={zForL1}, z-for-L2={zForL2} (of {MomentsPerListener} each).");
    }
}
