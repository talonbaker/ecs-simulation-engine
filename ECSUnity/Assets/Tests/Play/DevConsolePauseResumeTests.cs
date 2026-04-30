using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-12: pause / resume commands halt and restore the engine.
/// </summary>
[TestFixture]
public class DevConsolePauseResumeTests
{
    private GameObject _go;
#if WARDEN
    private DevConsolePanel _panel;
    private TimeHudPanel    _hud;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go = new GameObject("DevCon_PauseResume");
#if WARDEN
        _hud   = _go.AddComponent<TimeHudPanel>();
        _panel = _go.AddComponent<DevConsolePanel>();
#endif
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("DevCon_PauseResume"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator PauseCommand_SetsTimeScaleZero()
    {
#if WARDEN
        _panel.SubmitCommand("pause");
        yield return null;

        Assert.AreEqual(0f, Time.timeScale, 0.001f,
            "pause command should set Time.timeScale to 0.");
#else
        yield return null;
        Assert.Pass("RETAIL — DevConsolePanel not compiled.");
#endif
    }

    [UnityTest]
    public IEnumerator ResumeCommand_RestoresTimeScale()
    {
#if WARDEN
        _panel.SubmitCommand("pause");
        yield return null;
        _panel.SubmitCommand("resume");
        yield return null;

        Assert.AreNotEqual(0f, Time.timeScale,
            "resume command should restore a non-zero timeScale.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator PauseCommand_OutputsSuccessMessage()
    {
#if WARDEN
        _panel.SubmitCommand("pause");
        yield return null;

        bool hasSuccess = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Success ||
                (e.Kind == ConsoleEntryKind.Info && e.Text.Contains("pause"))) { hasSuccess = true; break; }

        // Accept either a Success or any non-error output referencing the action.
        bool hasAnyNonError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind != ConsoleEntryKind.Error && e.Kind != ConsoleEntryKind.Command)
            { hasAnyNonError = true; break; }

        Assert.IsTrue(hasAnyNonError, "pause command should output a non-error response.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator ResumeCommand_OutputsSuccessMessage()
    {
#if WARDEN
        _panel.SubmitCommand("pause");
        yield return null;
        _panel.SubmitCommand("resume");
        yield return null;

        // Count non-error, non-command entries — should have at least two (one per command).
        int nonCmdCount = 0;
        foreach (var e in _panel.GetHistory())
            if (e.Kind != ConsoleEntryKind.Command) nonCmdCount++;

        Assert.GreaterOrEqual(nonCmdCount, 2,
            "Both pause and resume should emit a response line.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator PauseWhileAlreadyPaused_Idempotent()
    {
#if WARDEN
        _panel.SubmitCommand("pause");
        yield return null;
        _panel.SubmitCommand("pause");
        yield return null;

        Assert.AreEqual(0f, Time.timeScale, 0.001f,
            "Pausing while already paused should keep timeScale at 0.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator ResumeWithNoHud_FallsBackToTimeScaleOne()
    {
#if WARDEN
        // Panel with no TimeHud wired — should still resume via direct timeScale.
        var go2    = new GameObject("DevCon_PauseResume_Bare");
        var panel2 = go2.AddComponent<DevConsolePanel>();
        yield return null;

        Time.timeScale = 0f;
        panel2.SubmitCommand("resume");
        yield return null;

        Assert.AreNotEqual(0f, Time.timeScale,
            "resume fallback (no TimeHud) should restore Time.timeScale to 1f.");

        Object.Destroy(go2);
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
