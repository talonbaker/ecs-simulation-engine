using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-03: Entries in the Event Log panel must be presented in reverse
/// chronological order — most recent (highest tick) first.
///
/// Justification: The player wants to see what just happened at the top of
/// the list, not scroll past dozens of old events to find the latest gossip.
///
/// Design note: EventLogAggregator.Aggregate() is responsible for sorting;
/// EventLogPanel itself only binds whatever list the aggregator returns.
/// Testing the aggregator directly keeps this test free of UI binding concerns.
///
/// Requirements covered:
///   - Three entries given out of order → returned sorted highest-tick first.
///   - Five entries given already sorted descending → order preserved correctly.
/// </summary>
[TestFixture]
public class EventLogReverseChronTests
{
    private GameObject         _go;
    private EventLogAggregator _agg;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // EventLogAggregator is a plain C# class (no MonoBehaviour dependency).
        // We still create a named GameObject so the TearDown prefix pattern
        // covers any future helper objects that might be added.
        _go  = new GameObject("EvLogChron_Panel");
        _agg = new EventLogAggregator();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("EvLogChron_"))
                Object.Destroy(go);
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a WorldStateDto containing one entry per provided tick value.
    /// Participants are empty so no NPC filter is needed.
    /// </summary>
    private static WorldStateDto MakeWithTicks(params long[] ticks)
    {
        var chronicle = new List<ChronicleEntryDto>();
        for (int i = 0; i < ticks.Length; i++)
        {
            chronicle.Add(new ChronicleEntryDto
            {
                Id           = $"entry-{i}",
                Kind         = ChronicleEventKind.Other,
                Tick         = ticks[i],
                Participants = new List<string>(),
                Description  = $"Event at tick {ticks[i]}",
            });
        }
        return new WorldStateDto { Chronicle = chronicle };
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Three entries provided as 100, 300, 200 — must come back as 300, 200, 100.
    /// Assert uses > so the test catches ties or equal values too.
    /// </summary>
    [UnityTest]
    public IEnumerator ThreeEntries_SortedNewestFirst()
    {
        var ws      = MakeWithTicks(100, 300, 200);
        var entries = _agg.Aggregate(ws, EventLogFilters.AllTime, currentTick: 0, ticksPerDay: 0);
        yield return null;

        Assert.AreEqual(3, entries.Count);
        Assert.Greater(entries[0].Tick, entries[1].Tick,
            "First entry must have a higher tick than the second (newest first).");
        Assert.Greater(entries[1].Tick, entries[2].Tick,
            "Second entry must have a higher tick than the third.");
    }

    /// <summary>
    /// Five entries already in descending order — verifies that the aggregator
    /// does not accidentally reverse an already-sorted list or apply an unstable
    /// sort that would scramble equal-tick entries.
    /// </summary>
    [UnityTest]
    public IEnumerator AlreadySorted_StillNewestFirst()
    {
        var ws      = MakeWithTicks(500, 400, 300, 200, 100);
        var entries = _agg.Aggregate(ws, EventLogFilters.AllTime, currentTick: 0, ticksPerDay: 0);
        yield return null;

        Assert.AreEqual(500, entries[0].Tick,
            "Highest tick (500) must appear first in the sorted list.");
        Assert.AreEqual(100, entries[entries.Count - 1].Tick,
            "Lowest tick (100) must appear last.");
    }
}
