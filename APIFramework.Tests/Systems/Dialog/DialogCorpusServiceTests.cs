using System.IO;
using System.Linq;
using APIFramework.Systems.Dialog;
using Xunit;

namespace APIFramework.Tests.Systems.Dialog;

/// <summary>
/// AT-01 through AT-04 — corpus loading and schema validation.
/// </summary>
public class DialogCorpusServiceTests
{
    // ── Minimal valid corpus ──────────────────────────────────────────────────

    private const string MinimalCorpus = """
        {
          "schemaVersion": "0.1.0",
          "fragments": [
            {
              "id": "casual-greeting-1",
              "text": "Hey, what's up?",
              "register": "casual",
              "context": "greeting",
              "valenceProfile": { "belonging": "mid" },
              "noteworthiness": 10
            }
          ]
        }
        """;

    private const string InvalidCorpus = """
        {
          "schemaVersion": "0.2.0",
          "fragments": []
        }
        """;

    // ── AT-01: Schema validator accepts a well-formed corpus ──────────────────

    [Fact]
    public void AT01_WellFormedCorpus_LoadsSuccessfully()
    {
        var svc = new DialogCorpusService(MinimalCorpus);
        Assert.Single(svc.AllFragments);
        Assert.Equal("casual-greeting-1", svc.AllFragments[0].Id);
    }

    // ── AT-01b: Invalid corpus is rejected ────────────────────────────────────

    [Fact]
    public void AT01b_InvalidSchemaVersion_Throws()
    {
        Assert.Throws<System.InvalidOperationException>(
            () => new DialogCorpusService(InvalidCorpus));
    }

    // ── AT-01c: QueryByRegisterAndContext returns correct fragments ────────────

    [Fact]
    public void AT01c_QueryByRegisterAndContext_ReturnsMatch()
    {
        var svc    = new DialogCorpusService(MinimalCorpus);
        var result = svc.QueryByRegisterAndContext("casual", "greeting");
        Assert.Single(result);
        Assert.Equal("casual-greeting-1", result[0].Id);
    }

    [Fact]
    public void AT01d_QueryByRegisterAndContext_MissingPair_ReturnsEmpty()
    {
        var svc    = new DialogCorpusService(MinimalCorpus);
        var result = svc.QueryByRegisterAndContext("formal", "lashOut");
        Assert.Empty(result);
    }

    // ── AT-02: Starter corpus has >= 180 fragments ────────────────────────────

    [Fact]
    public void AT02_StarterCorpus_HasAtLeast180Fragments()
    {
        var path = DialogCorpusService.FindCorpusFile(
            "docs/c2-content/dialog/corpus-starter.json");

        if (path is null)
        {
            // Running outside repo tree — skip gracefully
            return;
        }

        var svc = DialogCorpusService.LoadFromFile(path);
        Assert.True(svc.AllFragments.Count >= 180,
            $"Expected >= 180 fragments, got {svc.AllFragments.Count}.");
    }

    // ── AT-03: Starter corpus covers every register × 8 priority contexts ─────

    [Fact]
    public void AT03_StarterCorpus_CoversAllRegisterPriorityContextCombos()
    {
        var path = DialogCorpusService.FindCorpusFile(
            "docs/c2-content/dialog/corpus-starter.json");
        if (path is null) return;

        var svc = DialogCorpusService.LoadFromFile(path);

        string[] registers        = { "formal", "casual", "crass", "clipped", "academic", "folksy" };
        // All 13 corpus contexts must have >= 2 fragments per register in the starter corpus
        string[] priorityContexts =
        {
            "complaint", "flirt", "lashOut", "share",
            "greeting",  "refusal", "agreement", "deflect",
            "brushOff",  "encouragement", "thanks", "apology", "acknowledge"
        };

        foreach (var reg in registers)
        foreach (var ctx in priorityContexts)
        {
            var hits = svc.QueryByRegisterAndContext(reg, ctx);
            Assert.True(hits.Count >= 2,
                $"Expected >= 2 fragments for ({reg}, {ctx}), got {hits.Count}.");
        }
    }

    // ── AT-04: casual × lashOut has >= 3 fragments ───────────────────────────

    [Fact]
    public void AT04_StarterCorpus_CasualLashOut_HasAtLeast3Fragments()
    {
        var path = DialogCorpusService.FindCorpusFile(
            "docs/c2-content/dialog/corpus-starter.json");
        if (path is null) return;

        var svc  = DialogCorpusService.LoadFromFile(path);
        var hits = svc.QueryByRegisterAndContext("casual", "lashOut");
        Assert.True(hits.Count >= 3,
            $"Expected >= 3 casual×lashOut fragments, got {hits.Count}.");
    }
}
