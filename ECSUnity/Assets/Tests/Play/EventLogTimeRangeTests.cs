using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-06: Time-range filter — only chronicle entries whose tick falls within
/// the last N game-days relative to the current simulation tick are shown.
///
/// Time window formula:
///   windowStart = currentTick - (timeRangeDays * ticksPerDay)
///   entry passes if entry.Tick >= windowStart
///
/// TimeRangeDays == 0 is the special "AllTime" sentinel — no lower bound.
///
/// Test parameters:
///   ticksPerDay = 1200
///   currentTick = 14500
///   Chronicle ticks:
///     t1 =   100   (day  ~0.1 — very old)
///     t2 =  1300   (day  ~1.1)
///     t3 =  8500   (day  ~7.1)
///     t4 = 14000   (day ~11.7 — recent)
///
/// Window calculations:
///   last 1 day:  14500 - 1200       = 13300 → only t4 (14000 >= 13300)   = 1 entry
///   last 7 days: 14500 - 7*1200     = 6100  → t3 (8500) + t4 (14000)    = 2 entries
///   AllTime:     no lower bound                                           = 4 entries
/// </summary>
[TestFixture]
public class EventLogTimeRangeTests
{
    private GameObject         _go;
    private EventLogAggregator _agg;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go  = new GameObject("EvLogTime_Panel");
        _agg = new EventLogAggregator();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("EvLogTime_"))
                Object.Destroy(go);
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Four entries spanning a wide tick range. See class summary for details.
    /// </summary>
    private static WorldStateDto MakeWorldState()
    {
        return new WorldStateDto
        {
            Chronicle = new List<ChronicleEntryDto>
            {
                new ChronicleEntryDto
                {
                    Id           = "t1",
                    Kind         = ChronicleEventKind.Other,
                    Tick         =   100,
                    Participants = new List<string>(),
                    Description  = "Early event",
                },
                new ChronicleEntryDto
                {
                    Id           = "t2",
                    Kind         = ChronicleEventKind.Other,
                    Tick         =  1300,
                    Participants = new List<string>(),
                    Description  = "Day-2 event",
                },
                new ChronicleEntryDto
                {
                    Id           = "t3",
                    Kind         = ChronicleEventKind.Other,
                    Tick         =  8500,
                    Participants = new List<string>(),
                    Description  = "Day-8 event",
                },
                new ChronicleEntryDto
                {
                    Id           = "t4",
                    Kind         = ChronicleEventKind.Other,
                    Tick         = 14000,
                    Participants = new List<string>(),
                    Description  = "Day-12 event",
                },
            }
        };
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// windowStart = 14500 - 1200 = 13300.
    /// Only t4 (tick 14000) is >= 13300.  Expected count: 1.
    /// </summary>
    [UnityTest]
    public IEnumerator LastOneDay_CurrentTick14500_OnlyRecentEvent()
    {
        long currentTick = 14500L;
        long ticksPerDay = 1200L;
        var  filters     = EventLogFilters.AllTime.WithTimeRangeDays(1);
        var  ws          = MakeWorldState();

        var entries = _agg.Aggregate(ws, filters, currentTick, ticksPerDay);
        yield return null;

        Assert.AreEqual(1, entries.Count,
            "Last-1-day filter at tick 14500 should return only the tick-14000 event.");
    }

    /// <summary>
    /// windowStart = 14500 - 7*1200 = 14500 - 8400 = 6100.
    /// t3 (8500) and t4 (14000) are both >= 6100.  Expected count: 2.
    /// </summary>
    [UnityTest]
    public IEnumerator LastSevenDays_CurrentTick14500_TwoEvents()
    {
        long currentTick = 14500L;
        long ticksPerDay = 1200L;
        var  filters     = EventLogFilters.AllTime.WithTimeRangeDays(7);
        var  ws          = MakeWorldState();

        var entries = _agg.Aggregate(ws, filters, currentTick, ticksPerDay);
        yield return null;

        Assert.AreEqual(2, entries.Count,
            "Last-7-days filter at tick 14500 should return 2 events (ticks 8500 and 14000).");
    }

    /// <summary>
    /// AllTime has no lower-bound window. All 4 entries must be returned regardless
    /// of what currentTick / ticksPerDay are passed.
    /// </summary>
    [UnityTest]
    public IEnumerator AllTime_FourEvents()
    {
        var entries = _agg.Aggregate(MakeWorldState(), EventLogFilters.AllTime, 0L, 0L);
        yield return null;

        Assert.AreEqual(4, entries.Count,
            "AllTime filter should return all 4 events.");
    }
}
