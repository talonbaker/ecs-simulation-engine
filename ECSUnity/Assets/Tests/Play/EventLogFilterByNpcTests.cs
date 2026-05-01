using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-04: NPC filter — when the player selects a specific NPC in the filter
/// dropdown, only events where that NPC is listed as a participant must appear.
///
/// Participant matching is case-sensitive and exact (no substring search here;
/// that is a separate free-text feature deferred to a later work packet).
///
/// Test data:
///   e1 — PublicArgument, participants: donna + frank  (both match)
///   e2 — Promotion,      participants: donna only     (donna matches)
///   e3 — Firing,         participants: frank only     (frank matches)
///
/// Requirements covered:
///   - No NPC filter (AllTime) → all 3 entries shown.
///   - Filter "donna" → 2 entries (e1 + e2).
///   - Filter "frank" → 2 entries (e1 + e3).
///   - Filter unknown NPC → 0 entries.
/// </summary>
[TestFixture]
public class EventLogFilterByNpcTests
{
    private GameObject    _go;
    private EventLogPanel _panel;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go    = new GameObject("EvLogNpc_Panel");
        _panel = _go.AddComponent<EventLogPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("EvLogNpc_"))
                Object.Destroy(go);
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds the canonical three-entry world state used across all tests in
    /// this fixture. Defined once to keep individual tests readable.
    /// </summary>
    private static WorldStateDto MakeWorldState()
    {
        // Three entries: two involving donna, two involving frank (one shared).
        return new WorldStateDto
        {
            Chronicle = new List<ChronicleEntryDto>
            {
                new ChronicleEntryDto
                {
                    Id           = "e1",
                    Kind         = ChronicleEventKind.PublicArgument,
                    Tick         = 100,
                    Participants = new List<string> { "donna", "frank" },
                    Description  = "Argument",
                },
                new ChronicleEntryDto
                {
                    Id           = "e2",
                    Kind         = ChronicleEventKind.Promotion,
                    Tick         = 200,
                    Participants = new List<string> { "donna" },
                    Description  = "Promotion",
                },
                new ChronicleEntryDto
                {
                    Id           = "e3",
                    Kind         = ChronicleEventKind.Firing,
                    Tick         = 300,
                    Participants = new List<string> { "frank" },
                    Description  = "Fired",
                },
            }
        };
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Baseline: no NPC filter applied means all chronicle entries pass through.
    /// </summary>
    [UnityTest]
    public IEnumerator NoFilter_AllThreeShown()
    {
        _panel.SetFilters(EventLogFilters.AllTime);
        _panel.InjectWorldStateForTest(MakeWorldState());
        yield return null;
        Assert.AreEqual(3, _panel.DisplayedEntryCount,
            "With no NPC filter, all 3 entries should be shown.");
    }

    /// <summary>
    /// Donna appears in e1 (shared) and e2 (solo) — exactly 2 entries expected.
    /// </summary>
    [UnityTest]
    public IEnumerator FilterDonna_TwoEntriesShown()
    {
        _panel.SetFilters(EventLogFilters.AllTime.WithNpc("donna"));
        _panel.InjectWorldStateForTest(MakeWorldState());
        yield return null;
        Assert.AreEqual(2, _panel.DisplayedEntryCount,
            "Filtering by 'donna' should show 2 entries (the argument and the promotion).");
    }

    /// <summary>
    /// Frank appears in e1 (shared) and e3 (solo) — exactly 2 entries expected.
    /// </summary>
    [UnityTest]
    public IEnumerator FilterFrank_TwoEntriesShown()
    {
        _panel.SetFilters(EventLogFilters.AllTime.WithNpc("frank"));
        _panel.InjectWorldStateForTest(MakeWorldState());
        yield return null;
        Assert.AreEqual(2, _panel.DisplayedEntryCount,
            "Filtering by 'frank' should show 2 entries (the argument and the firing).");
    }

    /// <summary>
    /// Filtering by an NPC that is not present in any entry must yield zero results,
    /// not throw or fall back to showing all entries.
    /// </summary>
    [UnityTest]
    public IEnumerator FilterUnknownNpc_ZeroEntries()
    {
        _panel.SetFilters(EventLogFilters.AllTime.WithNpc("nobody"));
        _panel.InjectWorldStateForTest(MakeWorldState());
        yield return null;
        Assert.AreEqual(0, _panel.DisplayedEntryCount,
            "Filtering by a non-existent NPC should show 0 entries.");
    }
}
