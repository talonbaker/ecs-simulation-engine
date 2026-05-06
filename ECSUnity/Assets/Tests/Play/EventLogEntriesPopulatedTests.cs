using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-02: Acceptance test that the Event Log panel is populated with the correct
/// number of chronicle entries drawn from a WorldStateDto.
///
/// In production the panel receives world state via EngineHost callbacks.
/// Here we bypass that path entirely with InjectWorldStateForTest(), which
/// lets these tests run without a live simulation or EngineHost MonoBehaviour.
///
/// Requirements covered:
///   - Zero entries in the chronicle → DisplayedEntryCount == 0.
///   - N entries with AllTime filter → DisplayedEntryCount == N.
///   - Null WorldStateDto is handled gracefully (no throw, count stays 0).
/// </summary>
[TestFixture]
public class EventLogEntriesPopulatedTests
{
    private GameObject    _go;
    private EventLogPanel _panel;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go    = new GameObject("EvLogPop_Panel");
        _panel = _go.AddComponent<EventLogPanel>();
        yield return null; // allow Awake / OnEnable
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("EvLogPop_"))
                Object.Destroy(go);
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal WorldStateDto whose Chronicle contains <paramref name="entryCount"/>
    /// entries. All entries use PublicArgument so they pass any kind-based default filter.
    /// </summary>
    private static WorldStateDto MakeWorldState(int entryCount)
    {
        var chronicle = new List<ChronicleEntryDto>();
        for (int i = 0; i < entryCount; i++)
        {
            chronicle.Add(new ChronicleEntryDto
            {
                Id           = $"entry-{i:D4}",
                Kind         = ChronicleEventKind.PublicArgument,
                Tick         = i * 100L,
                Participants = new List<string> { "npc-01" },
                Description  = $"Event {i}",
                Persistent   = true,
            });
        }
        return new WorldStateDto { Chronicle = chronicle };
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// An empty chronicle list must not crash and must report zero displayed entries.
    /// </summary>
    [UnityTest]
    public IEnumerator ZeroEntries_DisplayedCountZero()
    {
        _panel.SetFilters(EventLogFilters.AllTime);
        _panel.InjectWorldStateForTest(MakeWorldState(0));
        yield return null;
        Assert.AreEqual(0, _panel.DisplayedEntryCount,
            "Zero chronicle entries should produce DisplayedEntryCount == 0.");
    }

    /// <summary>
    /// Five entries with AllTime filter — all five must surface in the panel.
    /// </summary>
    [UnityTest]
    public IEnumerator FiveEntries_DisplayedCountFive()
    {
        _panel.SetFilters(EventLogFilters.AllTime);
        _panel.InjectWorldStateForTest(MakeWorldState(5));
        yield return null;
        Assert.AreEqual(5, _panel.DisplayedEntryCount,
            "Five chronicle entries should produce DisplayedEntryCount == 5.");
    }

    /// <summary>
    /// Twenty entries — verifies no off-by-one error at a slightly larger count
    /// and confirms AllTime truly means "no time limit".
    /// </summary>
    [UnityTest]
    public IEnumerator TwentyEntries_AllDisplayed()
    {
        _panel.SetFilters(EventLogFilters.AllTime);
        _panel.InjectWorldStateForTest(MakeWorldState(20));
        yield return null;
        Assert.AreEqual(20, _panel.DisplayedEntryCount,
            "All 20 entries should be displayed with AllTime filter.");
    }

    /// <summary>
    /// Passing null is a plausible scenario on first frame before the engine
    /// has produced any state. The panel must not throw and must show 0 entries.
    /// </summary>
    [UnityTest]
    public IEnumerator NullWorldState_DisplayedCountZero()
    {
        _panel.SetFilters(EventLogFilters.AllTime);
        _panel.InjectWorldStateForTest(null);
        yield return null;
        Assert.AreEqual(0, _panel.DisplayedEntryCount,
            "Null WorldStateDto should produce zero displayed entries.");
    }
}
