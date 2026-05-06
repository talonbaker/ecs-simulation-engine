using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-14: Up / Down arrow keys navigate command history.
/// </summary>
[TestFixture]
public class DevConsoleHistoryNavigationTests
{
    private GameObject _go;
#if WARDEN
    private DevConsolePanel _panel;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go = new GameObject("DevCon_HistNav");
#if WARDEN
        _panel = _go.AddComponent<DevConsolePanel>();
#endif
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("DevCon_HistNav"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator EmptyHistory_UpDoesNothing()
    {
#if WARDEN
        // No commands submitted; NavigateHistoryUp on empty history should be a no-op.
        _panel.SetInput("draft");
        _panel.NavigateHistoryUp();
        yield return null;

        // Nothing to navigate to; panel should still have the original input (or empty).
        Assert.IsNotNull(_panel.CurrentInput, "CurrentInput should never be null.");
#else
        yield return null;
        Assert.Pass("RETAIL — DevConsolePanel not compiled.");
#endif
    }

    [UnityTest]
    public IEnumerator SingleCommand_UpRecalls()
    {
#if WARDEN
        _panel.SubmitCommand("help");
        yield return null;

        _panel.SetInput(string.Empty);
        _panel.NavigateHistoryUp();
        yield return null;

        Assert.AreEqual("help", _panel.CurrentInput,
            "Up arrow should recall the last submitted command.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator MultipleCommands_UpCyclesOldest()
    {
#if WARDEN
        _panel.SubmitCommand("help");
        yield return null;
        _panel.SubmitCommand("pause");
        yield return null;
        _panel.SubmitCommand("resume");
        yield return null;

        _panel.SetInput(string.Empty);
        _panel.NavigateHistoryUp();  // "resume"
        yield return null;
        Assert.AreEqual("resume", _panel.CurrentInput,
            "First Up should recall the most recent command.");

        _panel.NavigateHistoryUp();  // "pause"
        yield return null;
        Assert.AreEqual("pause", _panel.CurrentInput,
            "Second Up should recall the second-most-recent command.");

        _panel.NavigateHistoryUp();  // "help"
        yield return null;
        Assert.AreEqual("help", _panel.CurrentInput,
            "Third Up should recall the oldest command.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator UpThenDown_RestoresOriginalInput()
    {
#if WARDEN
        _panel.SubmitCommand("help");
        yield return null;

        _panel.SetInput("in-progress-text");
        _panel.NavigateHistoryUp();  // saves "in-progress-text", shows "help"
        yield return null;
        Assert.AreEqual("help", _panel.CurrentInput);

        _panel.NavigateHistoryDown();  // restores "in-progress-text"
        yield return null;
        Assert.AreEqual("in-progress-text", _panel.CurrentInput,
            "Down after Up should restore the pre-navigation input.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator DownAtBottom_IsNoOp()
    {
#if WARDEN
        // With history index at -1 (not navigating), Down should be a no-op.
        _panel.SubmitCommand("help");
        yield return null;
        _panel.SetInput("current");
        _panel.NavigateHistoryDown();  // idx is -1, should be a no-op
        yield return null;

        // Input should not have changed.
        Assert.AreEqual("current", _panel.CurrentInput,
            "Down when not navigating history should leave input unchanged.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator HistoryCount_IncreasesAfterSubmit()
    {
#if WARDEN
        int before = _panel.CmdHistoryCount;

        _panel.SubmitCommand("help");
        yield return null;
        _panel.SubmitCommand("pause");
        yield return null;

        Assert.AreEqual(before + 2, _panel.CmdHistoryCount,
            "CmdHistoryCount should increase by one for each submitted command.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
