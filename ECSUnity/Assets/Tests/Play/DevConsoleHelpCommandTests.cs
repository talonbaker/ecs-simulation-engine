using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-02: 'help' lists >= 15 commands.
/// </summary>
[TestFixture]
public class DevConsoleHelpCommandTests
{
    private GameObject _go;
#if WARDEN
    private DevConsolePanel _panel;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go = new GameObject("DevCon_Help");
#if WARDEN
        _panel = _go.AddComponent<DevConsolePanel>();
#endif
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("DevCon_Help"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator HelpCommand_OutputsAtLeast15Commands()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("help");
        yield return null;

        var history = _panel.GetHistory();
        // Expect help to list >= 15 command entries.
        // The help output has multiple lines; count entries with indented text.
        int lineCount = 0;
        foreach (var entry in history)
            if (entry.Text.Contains("  ")) lineCount++;  // indented = command entry

        Assert.GreaterOrEqual(lineCount, 15,
            "help should list at least 15 registered commands.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator HelpCommand_HistoryContainsHelpEntry()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("help");
        yield return null;

        bool found = false;
        foreach (var entry in _panel.GetHistory())
            if (entry.Text.Contains("help")) { found = true; break; }

        Assert.IsTrue(found, "Help output must mention 'help' command.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
