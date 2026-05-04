using System.IO;
using Xunit;

namespace APIFramework.Tests.Documentation;

/// <summary>
/// WP-4.0.L doc-presence tests. Trivial assertions that catch regressions in
/// the audit-trail discipline — if these go red, someone deleted (rather
/// than refined) a load-bearing ledger entry or doc.
/// </summary>
public class AuthoringLoopDocsTests
{
    [Fact]
    public void FutureFeatures_ContainsFF016Entry()
    {
        var path    = LocateRepoRelative("docs/future-features.md");
        var content = File.ReadAllText(path);
        Assert.Contains("FF-016",                 content);
        Assert.Contains("In-game scene authoring", content);
        Assert.Contains("Shipped",                 content);   // FF-016 records its source-packet ship lineage
    }

    [Fact]
    public void ModApiCandidates_ContainsMac015AndMac016()
    {
        var path    = LocateRepoRelative("docs/c2-infrastructure/MOD-API-CANDIDATES.md");
        var content = File.ReadAllText(path);
        Assert.Contains("MAC-015", content);
        Assert.Contains("MAC-016", content);
        Assert.Contains("MAC-017", content);
        Assert.Contains("WP-4.0.J", content);
        Assert.Contains("WP-4.0.I", content);
        Assert.Contains("WP-4.0.M", content);
    }

    [Fact]
    public void WorldDefinitionsReadme_Exists()
    {
        var path = LocateRepoRelative("docs/c2-content/world-definitions/README.md");
        Assert.True(File.Exists(path), $"Expected README at {path}");
        var content = File.ReadAllText(path);
        Assert.Contains("Quickstart for level designers", content);
        Assert.Contains("Ctrl+Shift+A",                   content);
        Assert.Contains("AuthorModeController",           content);
    }

    [Fact]
    public void Phase4KickoffBrief_ContainsWave4Subsection()
    {
        var path    = LocateRepoRelative("docs/PHASE-4-KICKOFF-BRIEF.md");
        var content = File.ReadAllText(path);
        Assert.Contains("Wave 4 — Authoring Loop", content);
        Assert.Contains("WP-4.0.M",                content);
        Assert.Contains("WP-4.0.I",                content);
        Assert.Contains("WP-4.0.J",                content);
        Assert.Contains("WP-4.0.K",                content);
    }

    private static string LocateRepoRelative(string relPath)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 8; i++)
        {
            if (dir is null) break;
            var candidate = Path.Combine(dir.FullName, relPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"Could not locate {relPath} from CWD or any ancestor.");
    }
}
