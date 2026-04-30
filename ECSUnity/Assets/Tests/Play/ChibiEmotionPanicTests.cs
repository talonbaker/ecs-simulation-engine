using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-12: Chibi overlay shows Sweat icon when NPC urgency >= 0.8.
///
/// ChibiEmotionPopulator.TestComputeIcon(EntityStateDto) is a public test
/// hook that returns the icon that would be displayed for the given entity
/// without requiring a live EngineHost.
/// </summary>
[TestFixture]
public class ChibiEmotionPanicTests
{
    private GameObject            _go;
    private ChibiEmotionPopulator _pop;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go  = new GameObject("ChibiPanic_Pop");
        _pop = _go.AddComponent<ChibiEmotionPopulator>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("ChibiPanic_"))
                UnityEngine.Object.Destroy(go);
    }

    // Helper: build a minimal EntityStateDto for the populator to evaluate.
    // The schema (v0.4) exposes per-drive urgencies (EatUrgency, DrinkUrgency, ...),
    // not a generic "Urgency"; PhysiologyStateDto has IsSleeping (not HasSleeping);
    // and DominantDrive has no Work/Rest values. ChibiEmotionPopulator's Sweat path
    // is keyed on EatUrgency >= 0.8 || DrinkUrgency >= 0.8.
    // Energy is on a 0..100 scale (matches engine SimConfig defaults like
    // energyStart=90.0); the SleepZ thresholds in the impl are raw 15 and 25.
    private static EntityStateDto MakeEntity(float urgency, float energy, bool sleeping)
    {
        return new EntityStateDto
        {
            Id   = "test-panic-1",
            Name = "TestNpc",
            Drives = new DrivesStateDto
            {
                EatUrgency = urgency,
            },
            Physiology = new PhysiologyStateDto
            {
                Energy     = energy,
                IsSleeping = sleeping,
            },
        };
    }

    [UnityTest]
    public IEnumerator LowUrgency_NoSweat()
    {
        var entity = MakeEntity(urgency: 0.3f, energy: 80f, sleeping: false);
        yield return null;

        var icon = _pop.TestComputeIcon(entity);

        Assert.AreNotEqual(IconKind.Sweat, icon,
            "Low urgency (0.3) should not produce a Sweat icon.");
    }

    [UnityTest]
    public IEnumerator HighUrgency_ShowsSweat()
    {
        var entity = MakeEntity(urgency: 0.9f, energy: 80f, sleeping: false);
        yield return null;

        var icon = _pop.TestComputeIcon(entity);

        Assert.AreEqual(IconKind.Sweat, icon,
            "High urgency (0.9) should produce a Sweat icon.");
    }

    [UnityTest]
    public IEnumerator ExactThreshold_0_8_ShowsSweat()
    {
        var entity = MakeEntity(urgency: 0.8f, energy: 80f, sleeping: false);
        yield return null;

        var icon = _pop.TestComputeIcon(entity);

        Assert.AreEqual(IconKind.Sweat, icon,
            "Urgency at exactly the 0.8 threshold should produce Sweat.");
    }

    [UnityTest]
    public IEnumerator JustBelow_0_8_NoSweat()
    {
        var entity = MakeEntity(urgency: 0.79f, energy: 80f, sleeping: false);
        yield return null;

        var icon = _pop.TestComputeIcon(entity);

        Assert.AreNotEqual(IconKind.Sweat, icon,
            "Urgency just below 0.8 (0.79) must not produce a Sweat icon.");
    }
}
