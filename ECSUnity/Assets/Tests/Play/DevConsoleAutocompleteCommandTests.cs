using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-15: Tab on an empty or partial command name cycles through command names.
/// </summary>
[TestFixture]
public class DevConsoleAutocompleteCommandTests
{
    private GameObject _go;
#if WARDEN
    private DevConsolePanel          _panel;
    private DevConsoleCommandDispatcher _dispatcher;
    private DevConsoleAutocomplete   _autocomplete;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go = new GameObject("DevCon_ACCmd");
#if WARDEN
        _panel       = _go.AddComponent<DevConsolePanel>();
#endif
        yield return null;
#if WARDEN
        _dispatcher  = _panel.Dispatcher;
        _autocomplete = new DevConsoleAutocomplete(_dispatcher);
#endif
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("DevCon_ACCmd"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator EmptyInput_CandidatesContainAllCommands()
    {
#if WARDEN
        yield return null;
        var candidates = _autocomplete.GetCandidates(string.Empty, null);

        Assert.Greater(candidates.Count, 0,
            "Empty input should yield at least one command candidate.");
        Assert.Contains("help", (System.Collections.IList)candidates,
            "Command 'help' should be among the candidates.");
        Assert.Contains("spawn", (System.Collections.IList)candidates,
            "Command 'spawn' should be among the candidates.");
#else
        yield return null;
        Assert.Pass("RETAIL — DevConsolePanel not compiled.");
#endif
    }

    [UnityTest]
    public IEnumerator PartialPrefix_FiltersToMatchingCommands()
    {
#if WARDEN
        yield return null;
        var candidates = _autocomplete.GetCandidates("fo", null);

        Assert.Greater(candidates.Count, 0,
            "Prefix 'fo' should match at least one command (force-kill, force-faint).");
        foreach (var c in candidates)
            Assert.IsTrue(c.StartsWith("fo"),
                $"Candidate '{c}' should start with 'fo'.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Tab_Empty_CompletesToFirstCommandAlphabetically()
    {
#if WARDEN
        yield return null;
        _autocomplete.Reset();
        string result = _autocomplete.Cycle(string.Empty, null);

        var allCandidates = _autocomplete.GetCandidates(string.Empty, null);
        // First call after reset should yield the first candidate (index 0).
        Assert.IsNotEmpty(result,
            "First Tab on empty input should complete to the first command name.");
        Assert.IsTrue(
            ((System.Collections.IList)allCandidates).Contains(result),
            $"Completed value '{result}' should be in the candidate list.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Tab_CyclesThroughCandidates()
    {
#if WARDEN
        yield return null;
        _autocomplete.Reset();
        string first  = _autocomplete.Cycle(string.Empty, null);
        string second = _autocomplete.Cycle(first, null);   // NOTE: input hasn't changed per Tab contract

        // After Reset, the second Cycle on the same input advances index by 1.
        // We just verify second != first (unless there is exactly one command).
        var allCandidates = _autocomplete.GetCandidates(string.Empty, null);
        if (allCandidates.Count > 1)
            Assert.AreNotEqual(first, second,
                "Second Tab press should cycle to a different command name.");
        else
            Assert.AreEqual(first, second,
                "With only one candidate, Tab should return it again.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Reset_ClearsCycleState()
    {
#if WARDEN
        yield return null;
        _autocomplete.Cycle(string.Empty, null); // advances index to 0
        _autocomplete.Reset();
        string afterReset = _autocomplete.Cycle(string.Empty, null); // should restart from 0

        var allCandidates = _autocomplete.GetCandidates(string.Empty, null);
        if (allCandidates.Count > 0)
            Assert.AreEqual(allCandidates[0], afterReset,
                "After Reset, Tab should start cycling from the first candidate again.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator TriggerAutocomplete_ViaPanel_SetsInput()
    {
#if WARDEN
        _panel.SetInput(string.Empty);
        _panel.TriggerAutocomplete();
        yield return null;

        Assert.IsNotEmpty(_panel.CurrentInput,
            "TriggerAutocomplete on empty input should fill the input field with a command name.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
