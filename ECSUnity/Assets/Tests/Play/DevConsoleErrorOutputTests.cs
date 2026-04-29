using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-18: Invalid or error-producing commands output red error messages.
///
/// Verifies:
///  - An unrecognised command produces a ConsoleEntry with Kind == Error.
///  - The error text is non-empty.
///  - Valid commands do NOT produce an error entry.
///  - The "ERROR:" prefix stripping works: the stored entry text does NOT contain
///    the literal string "ERROR:" (it is stripped by DevConsolePanel.SubmitCommand).
///
/// Relies only on DevConsolePanel + DevConsoleCommandDispatcher; no EngineHost
/// required because error detection happens at the dispatcher level before any
/// engine interaction.
/// </summary>
[TestFixture]
public class DevConsoleErrorOutputTests
{
    private GameObject _go;
#if WARDEN
    private DevConsolePanel _panel;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go = new GameObject("DevCon_Err");
#if WARDEN
        _panel = _go.AddComponent<DevConsolePanel>();
#endif
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("DevCon_Err"))
                Object.Destroy(go);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Submitting an unrecognised command (e.g. "xyzzy") must produce at least
    /// one history entry with Kind == Error.
    /// </summary>
    [UnityTest]
    public IEnumerator UnknownCommand_ProducesErrorEntry()
    {
#if WARDEN
        yield return null;

        _panel.SubmitCommand("xyzzy-not-a-real-command");

        var history = _panel.GetHistory();
        bool hasError = false;
        foreach (var entry in history)
            if (entry.Kind == ConsoleEntryKind.Error)
                hasError = true;

        Assert.IsTrue(hasError,
            "An unknown command must produce at least one history entry with Kind == Error.");
#else
        yield return null;
        Assert.Pass("RETAIL — DevConsolePanel not compiled.");
#endif
    }

    /// <summary>
    /// The error entry text must be non-empty (a useful message, not a blank line).
    /// </summary>
    [UnityTest]
    public IEnumerator UnknownCommand_ErrorEntry_TextIsNonEmpty()
    {
#if WARDEN
        yield return null;

        _panel.SubmitCommand("definitely-not-a-command-12345");

        var history = _panel.GetHistory();
        foreach (var entry in history)
        {
            if (entry.Kind == ConsoleEntryKind.Error)
            {
                Assert.IsNotEmpty(entry.Text,
                    "Error entry text must not be empty.");
                Assert.Pass("Error entry found with non-empty text.");
            }
        }

        Assert.Fail("No Error entry found in history after unknown command.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    /// <summary>
    /// The panel strips the "ERROR:" prefix before storing the entry, so stored
    /// text must not literally start with "ERROR:".
    /// </summary>
    [UnityTest]
    public IEnumerator ErrorEntry_DoesNotContainRawPrefix()
    {
#if WARDEN
        yield return null;

        _panel.SubmitCommand("bad-command-prefix-test");

        var history = _panel.GetHistory();
        foreach (var entry in history)
        {
            if (entry.Kind == ConsoleEntryKind.Error)
            {
                Assert.IsFalse(entry.Text.TrimStart().StartsWith("ERROR:"),
                    $"Stored error text should have 'ERROR:' stripped, got: '{entry.Text}'");
            }
        }
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    /// <summary>
    /// The "help" command is a valid command and must NOT produce an Error entry.
    /// </summary>
    [UnityTest]
    public IEnumerator ValidCommand_Help_ProducesNoErrorEntry()
    {
#if WARDEN
        yield return null;

        _panel.ClearHistory();
        _panel.SubmitCommand("help");

        var history = _panel.GetHistory();
        foreach (var entry in history)
        {
            Assert.AreNotEqual(ConsoleEntryKind.Error, entry.Kind,
                $"'help' command must not produce an error entry. Got: '{entry.Text}'");
        }
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    /// <summary>
    /// "clear" is a valid built-in command; it must not produce an error entry.
    /// </summary>
    [UnityTest]
    public IEnumerator ValidCommand_Clear_ProducesNoErrorEntry()
    {
#if WARDEN
        yield return null;

        // Prime history with something so clear has work to do.
        _panel.SubmitCommand("help");

        // Now clear — should not throw or produce an error.
        Assert.DoesNotThrow(
            () => _panel.SubmitCommand("clear"),
            "'clear' command should not throw.");

        // After clear the history may be empty or contain only the "clear" echo;
        // either way there must be no Error entry.
        var history = _panel.GetHistory();
        foreach (var entry in history)
        {
            Assert.AreNotEqual(ConsoleEntryKind.Error, entry.Kind,
                $"'clear' command must not produce an error entry. Got: '{entry.Text}'");
        }
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    /// <summary>
    /// Submitting blank / whitespace-only input must produce no entry at all
    /// (the panel short-circuits on whitespace before dispatching).
    /// </summary>
    [UnityTest]
    public IEnumerator BlankInput_ProducesNoEntry()
    {
#if WARDEN
        yield return null;

        _panel.ClearHistory();
        _panel.SubmitCommand("   ");

        Assert.AreEqual(0, _panel.GetHistory().Count,
            "Whitespace-only input must produce no console entry.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    /// <summary>
    /// Multiple unknown commands accumulate one Error entry each.
    /// </summary>
    [UnityTest]
    public IEnumerator MultipleUnknownCommands_AccumulateErrorEntries()
    {
#if WARDEN
        yield return null;

        _panel.ClearHistory();
        _panel.SubmitCommand("bad-one");
        _panel.SubmitCommand("bad-two");
        _panel.SubmitCommand("bad-three");

        int errorCount = 0;
        foreach (var entry in _panel.GetHistory())
            if (entry.Kind == ConsoleEntryKind.Error)
                errorCount++;

        Assert.GreaterOrEqual(errorCount, 3,
            "Three unknown commands should produce at least three Error entries.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
