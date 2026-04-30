using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-13: Chibi overlay shows SleepZ when NPC is sleeping or energy is very low (&lt; 0.25).
///
/// Sleep state takes precedence over high urgency.
/// </summary>
[TestFixture]
public class ChibiEmotionIrritationTests
{
    private GameObject            _go;
    private ChibiEmotionPopulator _pop;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go  = new GameObject("ChibiIrrit_Pop");
        _pop = _go.AddComponent<ChibiEmotionPopulator>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("ChibiIrrit_"))
                UnityEngine.Object.Destroy(go);
    }

    // The schema (v0.4) exposes per-drive urgencies (EatUrgency, DrinkUrgency, ...),
    // not a generic "Urgency"; PhysiologyStateDto has IsSleeping (not HasSleeping);
    // and DominantDrive has no Rest value. ChibiEmotionPopulator's SleepZ path is
    // entity.Physiology.IsSleeping || energy < 15 || energy < 25.
    // Energy is on a 0..100 scale (engine SimConfig defaults like energyStart=90.0).
    private static EntityStateDto MakeEntity(float urgency, float energy, bool sleeping)
    {
        return new EntityStateDto
        {
            Id   = "test-irrit-1",
            Name = "SleepyNpc",
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
    public IEnumerator Sleeping_ShowsSleepZ()
    {
        var entity = MakeEntity(urgency: 0.1f, energy: 10f, sleeping: true);
        yield return null;

        var icon = _pop.TestComputeIcon(entity);

        Assert.AreEqual(IconKind.SleepZ, icon,
            "IsSleeping=true should produce a SleepZ icon.");
    }

    [UnityTest]
    public IEnumerator LowEnergy_ShowsSleepZ()
    {
        // Energy below 25 triggers the SleepZ icon even when not explicitly sleeping.
        var entity = MakeEntity(urgency: 0.1f, energy: 10f, sleeping: false);
        yield return null;

        var icon = _pop.TestComputeIcon(entity);

        Assert.AreEqual(IconKind.SleepZ, icon,
            "Energy below 25 should produce a SleepZ icon.");
    }

    [UnityTest]
    public IEnumerator NormalEnergy_NoSleepZ()
    {
        var entity = MakeEntity(urgency: 0.1f, energy: 80f, sleeping: false);
        yield return null;

        var icon = _pop.TestComputeIcon(entity);

        Assert.AreNotEqual(IconKind.SleepZ, icon,
            "Normal energy (80) with no sleeping flag should not produce SleepZ.");
    }

    [UnityTest]
    public IEnumerator BothSleepingAndHighUrgency_SleepZTakesPrecedence()
    {
        // Sleep state wins over urgency — the NPC is asleep and therefore not panicking.
        var entity = MakeEntity(urgency: 0.9f, energy: 10f, sleeping: true);
        yield return null;

        var icon = _pop.TestComputeIcon(entity);

        Assert.AreEqual(IconKind.SleepZ, icon,
            "SleepZ should take precedence over Sweat when the NPC is sleeping.");
    }
}
