using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Performance gate: full Player UI stack (WP-3.1.E) must not degrade FPS below
/// 58 FPS mean / 55 FPS min / 50 FPS p99 measured over 120 frames.
///
/// EngineHost is absent so actual NPC simulation does not run; this isolates the
/// cost of the UI layer itself. The gate is conservative — if this fails it means
/// one of the UI MonoBehaviours has an unexpectedly expensive Update/LateUpdate.
/// </summary>
[TestFixture]
public class PerformanceGate30NpcWithFullUiTests
{
    private const int   SampleCount = 120;
    private const float MinFps      = 55f;
    private const float MeanFps     = 58f;
    private const float P99Fps      = 50f;

    private readonly List<GameObject> _created = new List<GameObject>();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Instantiate the complete WP-3.1.E UI stack on a single root object.
        // No EngineHost is wired so none of the panels read live world state.
        var root = new GameObject("PerfFullUi_Root");
        _created.Add(root);

        root.AddComponent<SelectionController>();
        root.AddComponent<SelectionHaloRenderer>();
        root.AddComponent<InspectorPanel>();
        root.AddComponent<TimeHudPanel>();
        root.AddComponent<NotificationPanel>();
        root.AddComponent<SettingsPanel>();
        root.AddComponent<SaveLoadPanel>();
        root.AddComponent<ChibiEmotionPopulator>();
        root.AddComponent<ConversationStreamRenderer>();
        root.AddComponent<RoomInspectorPanel>();
        root.AddComponent<ObjectInspectorPanel>();

        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _created)
            if (go != null) Object.Destroy(go);
        _created.Clear();

        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("PerfFullUi_"))
                Object.Destroy(go);

        // Restore time scale in case any test polluted it.
        Time.timeScale = 1f;
    }

    [UnityTest]
    public IEnumerator FullUiStack_FpsGatePreserved()
    {
        // Warm-up: discard the first 10 frames to let Unity settle.
        for (int i = 0; i < 10; i++) yield return null;

        var samples = new List<float>(SampleCount);

        for (int i = 0; i < SampleCount; i++)
        {
            yield return null;
            if (Time.deltaTime > 0f)
                samples.Add(1f / Time.deltaTime);
        }

        Assert.IsNotEmpty(samples, "FPS samples must be collected.");

        float mean = samples.Average();
        float min  = samples.Min();

        // p99 = the 1st-percentile value (lowest 1 %).
        var sorted   = samples.OrderBy(x => x).ToList();
        int p99Index = Mathf.Clamp((int)(sorted.Count * 0.01f), 0, sorted.Count - 1);
        float p99    = sorted[p99Index];

        Assert.GreaterOrEqual(mean, MeanFps,
            $"Mean FPS {mean:F1} is below the gate of {MeanFps} FPS.");
        Assert.GreaterOrEqual(min, MinFps,
            $"Min FPS {min:F1} is below the gate of {MinFps} FPS.");
        Assert.GreaterOrEqual(p99, P99Fps,
            $"p99 FPS {p99:F1} is below the gate of {P99Fps} FPS.");
    }
}
