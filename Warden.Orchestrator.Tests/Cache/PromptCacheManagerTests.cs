using System.Text.Json;
using Warden.Anthropic;
using Warden.Orchestrator.Cache;
using Xunit;

namespace Warden.Orchestrator.Tests.Cache;

/// <summary>
/// Acceptance tests for PromptCacheManager and CachedPrefixSource (WP-06).
/// Each test uses isolated temp directories; cleanup happens in Dispose.
/// </summary>
public sealed class PromptCacheManagerTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* best effort */ }
        }
    }

    // -- Helpers ----------------------------------------------------------------

    private string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "WP06_" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    private static string WriteManifest(string dir, params (string FileName, string Purpose)[] entries)
    {
        var manifestEntries = entries.Select(e => new
        {
            path    = e.FileName,
            slab    = 1,
            purpose = e.Purpose,
        }).ToArray();

        var json = JsonSerializer.Serialize(
            new { version = "1.0", entries = manifestEntries },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var manifestPath = Path.Combine(dir, "manifest.json");
        File.WriteAllText(manifestPath, json);
        return manifestPath;
    }

    private static (CachedPrefixSource Source, PromptCacheManager Manager)
        Build(string manifestPath, string baseDir)
    {
        var source  = new CachedPrefixSource(manifestPath, baseDir);
        var manager = new PromptCacheManager(source);
        return (source, manager);
    }

    // -- AT-01 ------------------------------------------------------------------

    /// <summary>
    /// BuildRequest output has cache_control on exactly slabs 1–3, never on slab 4.
    /// </summary>
    [Fact]
    public void AT01_CacheControl_OnSlabs1To3_NeverOnSlab4()
    {
        var dir = NewTempDir();
        File.WriteAllText(Path.Combine(dir, "corpus.txt"), "engine docs");
        var manifestPath = WriteManifest(dir, ("corpus.txt", "corpus"));

        var (_, manager) = Build(manifestPath, dir);

        var missionSlabs = new[]
        {
            new PromptSlab("mission", "mission context", CacheDisposition.Ephemeral5m),
        };

        var req = manager.BuildRequest(ModelId.SonnetV46, "do the task", missionSlabs);

        // System has 3 blocks: role frame, corpus, one mission slab
        Assert.NotNull(req.System);
        Assert.Equal(3, req.System.Count);

        foreach (var block in req.System)
        {
            var tb = Assert.IsType<TextBlock>(block);
            Assert.NotNull(tb.CacheControl);
            Assert.Equal("ephemeral", tb.CacheControl.Type);
        }

        // User turn — no cache_control
        Assert.Single(req.Messages);
        Assert.Single(req.Messages[0].Content);
        var userBlock = Assert.IsType<TextBlock>(req.Messages[0].Content[0]);
        Assert.Null(userBlock.CacheControl);
    }

    // -- AT-02 ------------------------------------------------------------------

    /// <summary>
    /// Passing a mission slab with Uncached disposition throws ArgumentException.
    /// </summary>
    [Fact]
    public void AT02_ThrowsArgumentException_WhenMissionSlabIsUncached()
    {
        var dir = NewTempDir();
        File.WriteAllText(Path.Combine(dir, "corpus.txt"), "docs");
        var manifestPath = WriteManifest(dir, ("corpus.txt", "corpus"));

        var (_, manager) = Build(manifestPath, dir);

        var badSlabs = new[] { new PromptSlab("bad", "text", CacheDisposition.Uncached) };

        var ex = Assert.Throws<ArgumentException>(
            () => manager.BuildRequest(ModelId.SonnetV46, "task", badSlabs));

        Assert.Contains("Uncached", ex.Message);
    }

    // -- AT-03 ------------------------------------------------------------------

    /// <summary>
    /// expectedTotalLatency = null → 5-minute TTL (no ttl field);
    /// expectedTotalLatency = 10 min → 1-hour TTL.
    /// </summary>
    [Fact]
    public void AT03_TtlSelection_NullGives5m_TenMinGives1h()
    {
        var dir = NewTempDir();
        File.WriteAllText(Path.Combine(dir, "corpus.txt"), "docs");
        var manifestPath = WriteManifest(dir, ("corpus.txt", "corpus"));

        var (_, manager) = Build(manifestPath, dir);

        // null → Ephemeral5m: cache_control.type = "ephemeral", ttl = null
        var req5m = manager.BuildRequest(ModelId.SonnetV46, "task", expectedTotalLatency: null);
        var block5m = Assert.IsType<TextBlock>(req5m.System![0]);
        Assert.Equal("ephemeral", block5m.CacheControl!.Type);
        Assert.Null(block5m.CacheControl.Ttl);

        // 10 min → Ephemeral1h: cache_control.ttl = "1h"
        var req1h = manager.BuildRequest(ModelId.SonnetV46, "task",
            expectedTotalLatency: TimeSpan.FromMinutes(10));
        var block1h = Assert.IsType<TextBlock>(req1h.System![0]);
        Assert.Equal("ephemeral", block1h.CacheControl!.Type);
        Assert.Equal("1h", block1h.CacheControl.Ttl);
    }

    // -- AT-04 ------------------------------------------------------------------

    /// <summary>
    /// Editing the engine fact sheet file on disk causes the next BuildRequest call
    /// to include the new content (mtime invalidation).
    /// </summary>
    [Fact]
    public void AT04_MtimeInvalidation_UpdatedFileAppearsOnNextCall()
    {
        var dir        = NewTempDir();
        var corpusPath = Path.Combine(dir, "corpus.txt");
        File.WriteAllText(corpusPath, "initial content");
        var manifestPath = WriteManifest(dir, ("corpus.txt", "corpus"));

        var (_, manager) = Build(manifestPath, dir);

        var req1      = manager.BuildRequest(ModelId.SonnetV46, "task1");
        var corpus1   = Assert.IsType<TextBlock>(req1.System![1]).Text;
        Assert.Contains("initial content", corpus1);

        // Advance mtime explicitly so detection is OS-clock-independent.
        File.WriteAllText(corpusPath, "updated content");
        File.SetLastWriteTimeUtc(corpusPath,
            File.GetLastWriteTimeUtc(corpusPath) + TimeSpan.FromSeconds(1));

        var req2    = manager.BuildRequest(ModelId.SonnetV46, "task2");
        var corpus2 = Assert.IsType<TextBlock>(req2.System![1]).Text;
        Assert.Contains("updated content", corpus2);
        Assert.DoesNotContain("initial content", corpus2);
    }

    // -- AT-05 ------------------------------------------------------------------

    /// <summary>
    /// Two consecutive BuildRequest calls within the same TTL window produce
    /// identical slab-1/2 bytes (cache key is stable).
    /// </summary>
    [Fact]
    public void AT05_ConsecutiveCalls_IdenticalStaticSlabBytes()
    {
        var dir = NewTempDir();
        File.WriteAllText(Path.Combine(dir, "corpus.txt"), "stable corpus");
        var manifestPath = WriteManifest(dir, ("corpus.txt", "corpus"));

        var (_, manager) = Build(manifestPath, dir);

        var req1 = manager.BuildRequest(ModelId.SonnetV46, "first task");
        var req2 = manager.BuildRequest(ModelId.SonnetV46, "second task");

        // Slab 1 — role frame
        var rf1 = Assert.IsType<TextBlock>(req1.System![0]).Text;
        var rf2 = Assert.IsType<TextBlock>(req2.System![0]).Text;
        Assert.Equal(rf1, rf2);

        // Slab 2 — corpus
        var c1 = Assert.IsType<TextBlock>(req1.System![1]).Text;
        var c2 = Assert.IsType<TextBlock>(req2.System![1]).Text;
        Assert.Equal(c1, c2);

        // Slab 4 — user turns must differ
        var u1 = Assert.IsType<TextBlock>(req1.Messages[0].Content[0]).Text;
        var u2 = Assert.IsType<TextBlock>(req2.Messages[0].Content[0]).Text;
        Assert.NotEqual(u1, u2);
    }

    // -- AT-06 ------------------------------------------------------------------

    /// <summary>
    /// userTurnBody = "" throws ArgumentException.
    /// </summary>
    [Fact]
    public void AT06_ThrowsArgumentException_WhenUserTurnBodyIsEmpty()
    {
        var dir = NewTempDir();
        File.WriteAllText(Path.Combine(dir, "corpus.txt"), "docs");
        var manifestPath = WriteManifest(dir, ("corpus.txt", "corpus"));

        var (_, manager) = Build(manifestPath, dir);

        Assert.Throws<ArgumentException>(() => manager.BuildRequest(ModelId.SonnetV46, ""));
    }

    // -- AT-07 ------------------------------------------------------------------

    /// <summary>
    /// Slab 1 begins with a clearly labelled role frame and ends with the
    /// canonical boundary marker \n\n---\n\n (manual-review criterion as a structural test).
    /// </summary>
    [Fact]
    public void AT07_RoleFrame_StartsWithLabel_EndsWithBoundaryMarker()
    {
        var dir = NewTempDir();
        File.WriteAllText(Path.Combine(dir, "corpus.txt"), "docs");
        var manifestPath = WriteManifest(dir, ("corpus.txt", "corpus"));

        var (source, _) = Build(manifestPath, dir);
        var roleFrame = source.GetRoleFrameText();

        // Clearly labelled header
        Assert.True(
            roleFrame.TrimStart().StartsWith('#'),
            "Role frame must begin with a Markdown header (#).");

        // Canonical boundary marker
        Assert.True(
            roleFrame.EndsWith("\n\n---\n\n"),
            "Role frame must end with the boundary marker \\n\\n---\\n\\n.");
    }

    // -- AT-08 ------------------------------------------------------------------

    /// <summary>
    /// Adding a new file entry to the manifest causes its content to appear in
    /// the corpus on the next BuildRequest call, in manifest order, separated by
    /// boundary markers.
    /// </summary>
    [Fact]
    public void AT08_NewManifestEntry_AppearsInNextCorpusCall()
    {
        var dir = NewTempDir();
        File.WriteAllText(Path.Combine(dir, "file1.txt"), "file-one content");
        var manifestPath = WriteManifest(dir, ("file1.txt", "first file"));

        var (source, _) = Build(manifestPath, dir);

        // Initial corpus contains only file1
        var corpus1 = source.GetCorpusText();
        Assert.Contains("file-one content", corpus1);
        Assert.DoesNotContain("file-two content", corpus1);

        // Write the new file
        File.WriteAllText(Path.Combine(dir, "file2.txt"), "file-two content");

        // Rewrite the manifest to include both entries, then advance its mtime.
        var newManifestPath = WriteManifest(dir,
            ("file1.txt", "first file"),
            ("file2.txt", "second file"));
        Assert.Equal(manifestPath, newManifestPath); // same path, overwritten
        File.SetLastWriteTimeUtc(manifestPath,
            File.GetLastWriteTimeUtc(manifestPath) + TimeSpan.FromSeconds(1));

        // Next call must include both files in order, separated by ---
        var corpus2 = source.GetCorpusText();
        Assert.Contains("file-one content", corpus2);
        Assert.Contains("file-two content", corpus2);

        var idxFile1 = corpus2.IndexOf("file-one content", StringComparison.Ordinal);
        var idxFile2 = corpus2.IndexOf("file-two content", StringComparison.Ordinal);
        Assert.True(idxFile1 < idxFile2, "file1 must appear before file2 (manifest order).");

        // Boundary marker between them
        var between = corpus2.Substring(idxFile1, idxFile2 - idxFile1);
        Assert.Contains("\n\n---\n\n", between);
    }

    // -- AT-09 ------------------------------------------------------------------

    /// <summary>
    /// A manifest entry whose path does not exist on disk causes CachedPrefixSource
    /// to throw FileNotFoundException at construction time, not on first call.
    /// </summary>
    [Fact]
    public void AT09_ThrowsFileNotFoundException_AtConstruction_WhenEntryPathMissing()
    {
        var dir = NewTempDir();
        var manifest = """
            {
              "version": "1.0",
              "entries": [
                {"path": "does-not-exist.txt", "slab": 1, "purpose": "ghost file"}
              ]
            }
            """;
        var manifestPath = Path.Combine(dir, "manifest.json");
        File.WriteAllText(manifestPath, manifest);

        // Must throw at construction time — not lazily on first GetCorpusText().
        Assert.Throws<FileNotFoundException>(() => new CachedPrefixSource(manifestPath, dir));
    }
}
