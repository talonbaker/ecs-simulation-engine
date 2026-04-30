using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-10: Virtual scroll performance — the panel must handle 1000 chronicle
/// entries without crashing, without freezing, and while keeping frame time
/// within the 200 ms CI gate.
///
/// Virtual scrolling background:
///   UI Toolkit's ListView implements virtual scroll out of the box.
///   Only the rows that are currently in the visible viewport are instantiated
///   as VisualElement tree nodes. Rows outside the viewport are recycled via
///   makeItem / bindItem callbacks. As a result, binding 1000 data items does
///   NOT create 1000 VisualElement row objects — typically only 10–20 rows
///   exist in the DOM at once regardless of list length.
///
///   These tests therefore do NOT assert that the row-element count is bounded
///   (that is an internal implementation detail of ListView). Instead they verify:
///     (a) DisplayedEntryCount equals the full entry count (all data is tracked).
///     (b) Frame time over 10 consecutive frames stays under 200 ms.
///         200 ms is a conservative CI gate; the target for production is 16 ms
///         (60 FPS). CI build machines can be significantly slower than player
///         hardware, hence the generous gate.
///
/// Requirements covered:
///   - DisplayedEntryCount == 1000 after injecting 1000 entries with AllTime filter.
///   - Max frame time over 10 post-injection frames < 200 ms.
/// </summary>
[TestFixture]
public class EventLogVirtualScrollPerformanceTests
{
    private GameObject    _go;
    private EventLogPanel _panel;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go    = new GameObject("EvLogVScroll_Panel");
        _panel = _go.AddComponent<EventLogPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("EvLogVScroll_"))
                Object.Destroy(go);
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Generates 1000 unique entries whose Kind cycles through all 11
    /// ChronicleEventKind values (modulo 11), and whose Participants cycle
    /// through 30 NPCs. This provides realistic variety without requiring
    /// any external data source.
    /// </summary>
    private static WorldStateDto Make1000Entries()
    {
        var chronicle = new List<ChronicleEntryDto>(1000);
        for (int i = 0; i < 1000; i++)
        {
            chronicle.Add(new ChronicleEntryDto
            {
                Id           = $"perf-{i:D4}",
                Kind         = (ChronicleEventKind)(i % 11),
                Tick         = i * 50L,
                Participants = new List<string> { $"npc-{i % 30:D2}" },
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
    /// After injecting 1000 entries with AllTime filter, every entry must be
    /// counted — virtual scroll must not silently drop entries outside the
    /// initial viewport.
    /// </summary>
    [UnityTest]
    public IEnumerator Load1000Entries_DisplayedCountCorrect()
    {
        _panel.SetFilters(EventLogFilters.AllTime);
        _panel.InjectWorldStateForTest(Make1000Entries());
        yield return null;

        Assert.AreEqual(1000, _panel.DisplayedEntryCount,
            "With AllTime filter and 1000 entries, all 1000 should be tracked.");
    }

    /// <summary>
    /// Measures the maximum frame time over 10 frames after the list is bound.
    /// The 200 ms gate is intentionally loose for CI. If this test fails on a
    /// developer machine it almost certainly indicates an O(n) layout or a
    /// missing virtual scroll setup (e.g. makeItem/bindItem not wired).
    /// </summary>
    [UnityTest]
    public IEnumerator Load1000Entries_FrameTimeAcceptable()
    {
        _panel.SetFilters(EventLogFilters.AllTime);
        _panel.InjectWorldStateForTest(Make1000Entries());

        // Measure frame time after injection. Collect the worst frame over 10
        // consecutive frames to catch any deferred layout passes.
        float maxMs = 0f;
        for (int i = 0; i < 10; i++)
        {
            yield return null;
            float ms = Time.deltaTime * 1000f;
            if (ms > maxMs) maxMs = ms;
        }

        Assert.Less(maxMs, 200f,
            $"Frame time {maxMs:F1}ms exceeded 200ms gate with 1000 event log entries.");
    }
}
