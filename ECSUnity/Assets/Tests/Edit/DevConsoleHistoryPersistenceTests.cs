using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

/// <summary>
/// AT-16: Command history is persisted to Logs/console-history.txt and reloaded
/// correctly across sessions.
///
/// Tests DevConsoleHistoryPersister directly (no MonoBehaviour, no scene load)
/// so these can run as Edit-mode tests with fast turnaround.
///
/// All files are written to a temp path under Application.temporaryCachePath
/// (resolved at runtime) or to System.IO.Path.GetTempPath() in edit mode, and
/// are cleaned up in TearDown.
/// </summary>
[TestFixture]
public class DevConsoleHistoryPersistenceTests
{
    private string _tempDir;
    private string _historyFile;

    [SetUp]
    public void SetUp()
    {
        // Use the OS temp directory so these tests don't pollute the project.
        _tempDir     = Path.Combine(Path.GetTempPath(), "WP31H_HistoryTests_" + System.Guid.NewGuid().ToString("N"));
        _historyFile = Path.Combine(_tempDir, "console-history.txt");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

#if WARDEN

    // ── Round-trip ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Saving a list of commands and loading them back produces an identical list.
    /// </summary>
    [Test]
    public void SaveLoad_RoundTrip_PreservesCommands()
    {
        var persister = new DevConsoleHistoryPersister();
        var commands  = new List<string> { "help", "inspect donna", "pause", "resume" };

        persister.Save(commands, _historyFile, maxEntries: 100);
        var loaded = persister.Load(_historyFile);

        Assert.AreEqual(commands.Count, loaded.Count,
            "Loaded count must match saved count.");
        for (int i = 0; i < commands.Count; i++)
            Assert.AreEqual(commands[i], loaded[i],
                $"Entry [{i}] mismatch: expected '{commands[i]}', got '{loaded[i]}'.");
    }

    /// <summary>
    /// Order is preserved: oldest command first, newest last.
    /// </summary>
    [Test]
    public void Save_PreservesInsertionOrder()
    {
        var persister = new DevConsoleHistoryPersister();
        var commands  = new List<string> { "first-cmd", "second-cmd", "third-cmd" };

        persister.Save(commands, _historyFile, maxEntries: 100);
        var loaded = persister.Load(_historyFile);

        Assert.AreEqual("first-cmd",  loaded[0], "First command should be first.");
        Assert.AreEqual("second-cmd", loaded[1], "Second command should be second.");
        Assert.AreEqual("third-cmd",  loaded[2], "Third command should be last.");
    }

    // ── Cap behaviour ─────────────────────────────────────────────────────────

    /// <summary>
    /// When the command list exceeds maxEntries, only the newest N are persisted.
    /// </summary>
    [Test]
    public void Save_ExceedsCap_RetainsOnlyNewest()
    {
        var persister = new DevConsoleHistoryPersister();
        var commands  = new List<string>();
        for (int i = 0; i < 150; i++)
            commands.Add($"cmd-{i:D3}");

        persister.Save(commands, _historyFile, maxEntries: 100);
        var loaded = persister.Load(_historyFile);

        Assert.AreEqual(100, loaded.Count,
            "Should retain exactly 100 entries when 150 are saved with cap 100.");

        // The retained entries should be the last 100: cmd-050 through cmd-149.
        Assert.AreEqual("cmd-050", loaded[0],
            "First retained entry should be the 51st original entry.");
        Assert.AreEqual("cmd-149", loaded[99],
            "Last retained entry should be the very last original entry.");
    }

    // ── Empty / missing file ───────────────────────────────────────────────────

    /// <summary>
    /// Loading from a path that does not exist returns an empty list without throwing.
    /// </summary>
    [Test]
    public void Load_FileMissing_ReturnsEmptyList()
    {
        var persister = new DevConsoleHistoryPersister();
        string nonExistent = Path.Combine(_tempDir, "ghost-history.txt");

        List<string> result = null;
        Assert.DoesNotThrow(
            () => result = persister.Load(nonExistent),
            "Loading a non-existent file must not throw.");

        Assert.IsNotNull(result, "Result must not be null.");
        Assert.AreEqual(0, result.Count, "Result must be empty for a missing file.");
    }

    /// <summary>
    /// Saving an empty list creates or truncates the file; loading returns empty.
    /// </summary>
    [Test]
    public void SaveLoad_EmptyList_RoundTrip()
    {
        var persister = new DevConsoleHistoryPersister();

        persister.Save(new List<string>(), _historyFile, maxEntries: 100);
        var loaded = persister.Load(_historyFile);

        Assert.AreEqual(0, loaded.Count, "Loading after saving empty list must return empty.");
    }

    // ── Blank-line filtering ───────────────────────────────────────────────────

    /// <summary>
    /// Blank lines in the file (e.g. trailing newlines) are ignored on load.
    /// </summary>
    [Test]
    public void Load_BlankLinesInFile_AreIgnored()
    {
        // Write the file directly with blank lines.
        Directory.CreateDirectory(Path.GetDirectoryName(_historyFile)!);
        File.WriteAllText(_historyFile, "cmd-alpha\n\ncmd-beta\n   \ncmd-gamma\n");

        var persister = new DevConsoleHistoryPersister();
        var loaded    = persister.Load(_historyFile);

        Assert.AreEqual(3, loaded.Count,
            "Blank/whitespace lines should be ignored; only 3 real commands.");
        Assert.AreEqual("cmd-alpha", loaded[0]);
        Assert.AreEqual("cmd-beta",  loaded[1]);
        Assert.AreEqual("cmd-gamma", loaded[2]);
    }

    // ── Directory creation ─────────────────────────────────────────────────────

    /// <summary>
    /// Saving to a path whose parent directory does not yet exist creates the
    /// directory automatically.
    /// </summary>
    [Test]
    public void Save_ParentDirectoryMissing_CreatesDirectory()
    {
        string deepPath = Path.Combine(_tempDir, "nested", "path", "history.txt");

        var persister = new DevConsoleHistoryPersister();
        Assert.DoesNotThrow(
            () => persister.Save(new List<string> { "cmd-deep" }, deepPath, 100),
            "Save to a new nested directory must not throw.");

        Assert.IsTrue(File.Exists(deepPath),
            "History file must exist after Save to a previously-missing directory.");

        var loaded = persister.Load(deepPath);
        Assert.AreEqual(1, loaded.Count);
        Assert.AreEqual("cmd-deep", loaded[0]);
    }

    // ── Overwrite ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A second Save call overwrites the previous file (does not append).
    /// </summary>
    [Test]
    public void Save_Twice_OverwritesPreviousFile()
    {
        var persister = new DevConsoleHistoryPersister();

        persister.Save(new List<string> { "first-save" },  _historyFile, 100);
        persister.Save(new List<string> { "second-save" }, _historyFile, 100);

        var loaded = persister.Load(_historyFile);

        Assert.AreEqual(1, loaded.Count,
            "Second Save should overwrite; only one entry expected.");
        Assert.AreEqual("second-save", loaded[0],
            "Loaded entry must be from the second Save call.");
    }

#else
    // ── RETAIL build: persister type absent ──────────────────────────────────

    [Test]
    public void RetailBuild_DevConsoleHistoryPersister_NotPresent()
    {
        // In RETAIL builds DevConsoleHistoryPersister is compiled out.
        // Referencing it here (without a guard) would be a compile error,
        // which is the desired outcome — this test documents that expectation.
        Assert.Pass("RETAIL build — DevConsoleHistoryPersister is stripped as expected.");
    }
#endif
}
