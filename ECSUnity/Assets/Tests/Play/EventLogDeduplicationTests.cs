using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-07: Deduplication — a single narrative event (identified by its Id) must
/// appear exactly once in the Event Log, even when the engine places copies of
/// the same entry in multiple memory streams.
///
/// Background: The simulation engine stores chronicle entries in several places
/// (global chronicle, per-NPC personal memories, per-relationship link memories).
/// All these streams are flattened into WorldStateDto.Chronicle before reaching
/// the client. Without deduplication, the player would see "Donna died" three or
/// more times for the same event — once per NPC who remembers it.
///
/// Deduplication key: ChronicleEntryDto.Id (string, case-sensitive).
/// When multiple entries share the same Id, the aggregator keeps only the first
/// occurrence in source order (stable dedup — no arbitrary re-ordering).
///
/// Requirements covered:
///   - Three entries with identical Id → 1 entry in output.
///   - Three entries with unique Ids → 3 entries in output.
///   - Mix of one duplicate pair and one unique Id → 2 entries in output.
/// </summary>
[TestFixture]
public class EventLogDeduplicationTests
{
    private GameObject         _go;
    private EventLogAggregator _agg;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go  = new GameObject("EvLogDedup_Panel");
        _agg = new EventLogAggregator();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("EvLogDedup_"))
                Object.Destroy(go);
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// The same "death-001" event appears three times (global chronicle,
    /// personal memory, relationship memory). Only one copy must surface.
    /// Description differs per row to confirm the aggregator is not relying on
    /// Description equality — Id is the canonical dedup key.
    /// </summary>
    [UnityTest]
    public IEnumerator DuplicateId_AppearsOnce()
    {
        var ws = new WorldStateDto
        {
            Chronicle = new List<ChronicleEntryDto>
            {
                new ChronicleEntryDto
                {
                    Id           = "death-001",
                    Kind         = ChronicleEventKind.DeathOrLeaving,
                    Tick         = 500,
                    Participants = new List<string> { "npc-a" },
                    Description  = "Global chronicle",
                },
                new ChronicleEntryDto
                {
                    Id           = "death-001",
                    Kind         = ChronicleEventKind.DeathOrLeaving,
                    Tick         = 500,
                    Participants = new List<string> { "npc-a" },
                    Description  = "Personal memory",
                },
                new ChronicleEntryDto
                {
                    Id           = "death-001",
                    Kind         = ChronicleEventKind.DeathOrLeaving,
                    Tick         = 500,
                    Participants = new List<string> { "npc-a" },
                    Description  = "Relationship memory",
                },
            }
        };

        var entries = _agg.Aggregate(ws, EventLogFilters.AllTime, 0L, 0L);
        yield return null;

        Assert.AreEqual(1, entries.Count,
            "Three entries with the same Id should be deduplicated to one entry.");
    }

    /// <summary>
    /// Three genuinely distinct events — all three must pass through without any
    /// accidental dedup triggered by matching kinds or ticks.
    /// </summary>
    [UnityTest]
    public IEnumerator UniqueIds_AllPreserved()
    {
        var ws = new WorldStateDto
        {
            Chronicle = new List<ChronicleEntryDto>
            {
                new ChronicleEntryDto
                {
                    Id           = "ev-001",
                    Kind         = ChronicleEventKind.Betrayal,
                    Tick         = 100,
                    Participants = new List<string>(),
                    Description  = "A",
                },
                new ChronicleEntryDto
                {
                    Id           = "ev-002",
                    Kind         = ChronicleEventKind.KindnessInCrisis,
                    Tick         = 200,
                    Participants = new List<string>(),
                    Description  = "B",
                },
                new ChronicleEntryDto
                {
                    Id           = "ev-003",
                    Kind         = ChronicleEventKind.Promotion,
                    Tick         = 300,
                    Participants = new List<string>(),
                    Description  = "C",
                },
            }
        };

        var entries = _agg.Aggregate(ws, EventLogFilters.AllTime, 0L, 0L);
        yield return null;

        Assert.AreEqual(3, entries.Count,
            "Three entries with unique Ids should all be preserved.");
    }

    /// <summary>
    /// One duplicate pair (Id "dup") + one genuinely unique entry (Id "uni").
    /// Expected output: 2 distinct entries — not 3 (no dedup), not 1 (over-dedup).
    /// </summary>
    [UnityTest]
    public IEnumerator MixedDuplicatesAndUnique_CorrectCount()
    {
        var ws = new WorldStateDto
        {
            Chronicle = new List<ChronicleEntryDto>
            {
                new ChronicleEntryDto
                {
                    Id           = "dup",
                    Kind         = ChronicleEventKind.AffairRevealed,
                    Tick         = 100,
                    Participants = new List<string>(),
                    Description  = "Dup 1",
                },
                new ChronicleEntryDto
                {
                    Id           = "dup",
                    Kind         = ChronicleEventKind.AffairRevealed,
                    Tick         = 100,
                    Participants = new List<string>(),
                    Description  = "Dup 2",
                },
                new ChronicleEntryDto
                {
                    Id           = "uni",
                    Kind         = ChronicleEventKind.Firing,
                    Tick         = 200,
                    Participants = new List<string>(),
                    Description  = "Unique",
                },
            }
        };

        var entries = _agg.Aggregate(ws, EventLogFilters.AllTime, 0L, 0L);
        yield return null;

        Assert.AreEqual(2, entries.Count,
            "2 distinct Ids (1 deduped pair + 1 unique) should produce 2 entries.");
    }
}
