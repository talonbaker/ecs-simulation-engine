using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Dialog;
using Xunit;

namespace APIFramework.Tests.Systems.Dialog;

/// <summary>
/// AT-07, AT-09 — calcification and decalcification mechanics.
/// </summary>
public class DialogCalcifySystemTests
{
    private static DialogConfig DefaultCfg() => new()
    {
        CalcifyThreshold           = 8,
        CalcifyContextDominanceMin = 0.70,
        DecalcifyTimeoutDays       = 30,
    };

    private static Entity SpawnNpc(EntityManager em, DialogHistoryComponent hist)
    {
        var e = em.CreateEntity();
        e.Add(new NpcTag());
        e.Add(hist);
        return e;
    }

    // ── AT-07: Fragment calcifies when threshold and dominance are met ────────

    [Fact]
    public void AT07_Fragment_CalcifiesWhenThresholdAndDominanceMet()
    {
        var em  = new EntityManager();
        var cfg = DefaultCfg();
        var sys = new DialogCalcifySystem(cfg);

        var hist = new DialogHistoryComponent();
        hist.UsesByFragmentId["frag-A"] = new FragmentUseRecord
        {
            UseCount           = 8,    // == CalcifyThreshold
            LastUseTick        = 1,
            LastUseGameTimeSec = 0.0,
            DominantContext    = "lashOut",
            Calcified          = false,
            // ContextCounts: 6 of 8 uses in lashOut = 75% > 70%
            ContextCounts      = { ["lashOut"] = 6, ["greeting"] = 2 },
        };

        SpawnNpc(em, hist);

        sys.Update(em, 1f);

        Assert.True(hist.UsesByFragmentId["frag-A"].Calcified,
            "Fragment should be calcified once threshold and dominance are met.");
    }

    // ── AT-07b: Fragment does NOT calcify if dominance is below minimum ───────

    [Fact]
    public void AT07b_Fragment_DoesNotCalcify_WhenDominanceTooLow()
    {
        var em  = new EntityManager();
        var cfg = DefaultCfg();
        var sys = new DialogCalcifySystem(cfg);

        var hist = new DialogHistoryComponent();
        hist.UsesByFragmentId["frag-B"] = new FragmentUseRecord
        {
            UseCount           = 8,
            LastUseTick        = 1,
            LastUseGameTimeSec = 0.0,
            DominantContext    = "lashOut",
            Calcified          = false,
            // Only 5 of 8 uses in lashOut = 62.5% < 70%
            ContextCounts      = { ["lashOut"] = 5, ["greeting"] = 3 },
        };

        SpawnNpc(em, hist);
        sys.Update(em, 1f);

        Assert.False(hist.UsesByFragmentId["frag-B"].Calcified,
            "Fragment should NOT calcify when dominance fraction < CalcifyContextDominanceMin.");
    }

    // ── AT-07c: Fragment does NOT calcify below use count threshold ───────────

    [Fact]
    public void AT07c_Fragment_DoesNotCalcify_BelowUseCountThreshold()
    {
        var em  = new EntityManager();
        var cfg = DefaultCfg();
        var sys = new DialogCalcifySystem(cfg);

        var hist = new DialogHistoryComponent();
        hist.UsesByFragmentId["frag-C"] = new FragmentUseRecord
        {
            UseCount           = 7, // one below threshold
            LastUseTick        = 1,
            LastUseGameTimeSec = 0.0,
            DominantContext    = "lashOut",
            Calcified          = false,
            ContextCounts      = { ["lashOut"] = 7 },
        };

        SpawnNpc(em, hist);
        sys.Update(em, 1f);

        Assert.False(hist.UsesByFragmentId["frag-C"].Calcified,
            "Fragment should NOT calcify when UseCount < CalcifyThreshold.");
    }

    // ── AT-09: Calcified fragment decalcifies after timeout ───────────────────

    [Fact]
    public void AT09_Calcified_Fragment_Decalcifies_AfterTimeout()
    {
        var em  = new EntityManager();
        var cfg = DefaultCfg(); // DecalcifyTimeoutDays = 30

        // 30 game-days in game-seconds = 30 * 86400
        const double thirtyDays = 30.0 * 86400.0;

        var sys = new DialogCalcifySystem(cfg);

        var hist = new DialogHistoryComponent();
        hist.UsesByFragmentId["frag-D"] = new FragmentUseRecord
        {
            UseCount           = 10,
            LastUseTick        = 1,
            LastUseGameTimeSec = 0.0,   // last used at t=0
            DominantContext    = "lashOut",
            Calcified          = true,
            ContextCounts      = { ["lashOut"] = 10 },
        };

        SpawnNpc(em, hist);

        // Advance past the decalcify window in one large tick
        sys.Update(em, (float)(thirtyDays + 1.0));

        Assert.False(hist.UsesByFragmentId["frag-D"].Calcified,
            "Calcified fragment should decalcify after DecalcifyTimeoutDays of disuse.");
    }

    // ── AT-09b: Calcified fragment STAYS calcified before timeout ─────────────

    [Fact]
    public void AT09b_Calcified_Fragment_StaysCalcified_BeforeTimeout()
    {
        var em  = new EntityManager();
        var cfg = DefaultCfg();
        var sys = new DialogCalcifySystem(cfg);

        double halfWindow = (30.0 * 86400.0) / 2.0; // 15 game-days

        var hist = new DialogHistoryComponent();
        hist.UsesByFragmentId["frag-E"] = new FragmentUseRecord
        {
            UseCount           = 10,
            LastUseTick        = 1,
            LastUseGameTimeSec = 0.0,
            DominantContext    = "lashOut",
            Calcified          = true,
            ContextCounts      = { ["lashOut"] = 10 },
        };

        SpawnNpc(em, hist);
        sys.Update(em, (float)halfWindow);

        Assert.True(hist.UsesByFragmentId["frag-E"].Calcified,
            "Calcified fragment should remain calcified before DecalcifyTimeoutDays.");
    }
}
