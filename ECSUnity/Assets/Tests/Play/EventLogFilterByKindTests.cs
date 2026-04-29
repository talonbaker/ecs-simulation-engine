using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-05: Kind filter — the player can restrict the log to one or more
/// ChronicleEventKind values using a checkbox panel or a dropdown.
///
/// An empty KindFilter (null or empty HashSet) means "show all kinds" — this
/// is the same semantic as EventLogFilters.AllTime with respect to kind.
///
/// Test data:
///   k1 — PublicArgument tick 100
///   k2 — DeathOrLeaving tick 200
///   k3 — Betrayal       tick 300
///   k4 — PublicArgument tick 400  (second PublicArgument)
///
/// Requirements covered:
///   - Single-kind filter (PublicArgument) → 2 entries.
///   - Single-kind filter (DeathOrLeaving) → 1 entry.
///   - Single-kind filter for a kind not present (Promotion) → 0 entries.
///   - No kind filter (AllTime) → all 4 entries.
/// </summary>
[TestFixture]
public class EventLogFilterByKindTests
{
    private GameObject    _go;
    private EventLogPanel _panel;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go    = new GameObject("EvLogKind_Panel");
        _panel = _go.AddComponent<EventLogPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("EvLogKind_"))
                Object.Destroy(go);
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Canonical four-entry world state used by all tests in this fixture.
    /// Two of the four entries share the PublicArgument kind.
    /// </summary>
    private static WorldStateDto MakeWorldState()
    {
        return new WorldStateDto
        {
            Chronicle = new List<ChronicleEntryDto>
            {
                new ChronicleEntryDto
                {
                    Id           = "k1",
                    Kind         = ChronicleEventKind.PublicArgument,
                    Tick         = 100,
                    Participants = new List<string>(),
                    Description  = "Argument",
                },
                new ChronicleEntryDto
                {
                    Id           = "k2",
                    Kind         = ChronicleEventKind.DeathOrLeaving,
                    Tick         = 200,
                    Participants = new List<string>(),
                    Description  = "Death",
                },
                new ChronicleEntryDto
                {
                    Id           = "k3",
                    Kind         = ChronicleEventKind.Betrayal,
                    Tick         = 300,
                    Participants = new List<string>(),
                    Description  = "Betrayal",
                },
                new ChronicleEntryDto
                {
                    Id           = "k4",
                    Kind         = ChronicleEventKind.PublicArgument,
                    Tick         = 400,
                    Participants = new List<string>(),
                    Description  = "Argument 2",
                },
            }
        };
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Only PublicArgument — k1 and k4 qualify (2 entries).
    /// </summary>
    [UnityTest]
    public IEnumerator FilterPublicArgument_TwoEntries()
    {
        _panel.SetFilters(EventLogFilters.AllTime.WithKinds(
            new HashSet<ChronicleEventKind> { ChronicleEventKind.PublicArgument }));
        _panel.InjectWorldStateForTest(MakeWorldState());
        yield return null;
        Assert.AreEqual(2, _panel.DisplayedEntryCount,
            "Filtering by PublicArgument should return 2 entries.");
    }

    /// <summary>
    /// Only DeathOrLeaving — only k2 qualifies (1 entry).
    /// </summary>
    [UnityTest]
    public IEnumerator FilterDeathOrLeaving_OneEntry()
    {
        _panel.SetFilters(EventLogFilters.AllTime.WithKinds(
            new HashSet<ChronicleEventKind> { ChronicleEventKind.DeathOrLeaving }));
        _panel.InjectWorldStateForTest(MakeWorldState());
        yield return null;
        Assert.AreEqual(1, _panel.DisplayedEntryCount,
            "Filtering by DeathOrLeaving should return 1 entry.");
    }

    /// <summary>
    /// Promotion is not present in the test data at all — result must be 0, not a
    /// fallback to "show everything".
    /// </summary>
    [UnityTest]
    public IEnumerator FilterPromotion_ZeroEntries()
    {
        _panel.SetFilters(EventLogFilters.AllTime.WithKinds(
            new HashSet<ChronicleEventKind> { ChronicleEventKind.Promotion }));
        _panel.InjectWorldStateForTest(MakeWorldState());
        yield return null;
        Assert.AreEqual(0, _panel.DisplayedEntryCount,
            "Filtering by Promotion should return 0 entries (none in test data).");
    }

    /// <summary>
    /// Null / empty kind set (expressed via AllTime) → all four entries visible.
    /// </summary>
    [UnityTest]
    public IEnumerator EmptyKindFilter_AllFourShown()
    {
        _panel.SetFilters(EventLogFilters.AllTime);
        _panel.InjectWorldStateForTest(MakeWorldState());
        yield return null;
        Assert.AreEqual(4, _panel.DisplayedEntryCount,
            "Empty kind filter should show all 4 entries.");
    }
}
