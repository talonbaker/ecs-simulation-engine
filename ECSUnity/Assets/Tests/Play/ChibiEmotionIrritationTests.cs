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
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("ChibiIrrit_"))
                Object.Destroy(go);
    }

    private static EntityStateDto MakeEntity(float urgency, float energy, bool sleeping)
    {
        return new EntityStateDto
        {
            Id   = "test-irrit-1",
            Name = "SleepyNpc",
            Drives = new DrivesStateDto
            {
                Dominant = DominantDrive.Rest,
                Urgency  = urgency,
            },
            Physiology = new PhysiologyStateDto
            {
                Energy     = energy,
                HasSleeping = sleeping,
            },
        };
    }

    [UnityTest]
    public IEnumerator Sleeping_ShowsSleepZ()
    {
        var entity = MakeEntity(urgency: 0.1f, energy: 0.1f, sleeping: true);
        yield return null;

        var icon = _pop.TestComputeIcon(entity);

        Assert.AreEqual(IconKind.SleepZ, icon,
            "HasSleeping=true should produce a SleepZ icon.");
    }

    [UnityTest]
    public IEnumerator LowEnergy_ShowsSleepZ()
    {
        // Energy < 0.25 triggers the SleepZ icon even when not explicitly sleeping.
        var entity = MakeEntity(urgency: 0.1f, energy: 0.1f, sleeping: false);
        yield return null;

        var icon = _pop.TestComputeIcon(entity);

        Assert.AreEqual(IconKind.SleepZ, icon,
            "Energy below 0.25 should produce a SleepZ icon.");
    }

    [UnityTest]
    public IEnumerator NormalEnergy_NoSleepZ()
    {
        var entity = MakeEntity(urgency: 0.1f, energy: 0.8f, sleeping: false);
        yield return null;

        var icon = _pop.TestComputeIcon(entity);

        Assert.AreNotEqual(IconKind.SleepZ, icon,
            "Normal energy (0.8) with no sleeping flag should not produce SleepZ.");
    }

    [UnityTest]
    public IEnumerator BothSleepingAndHighUrgency_SleepZTakesPrecedence()
    {
        // Sleep state wins over urgency — the NPC is asleep and therefore not panicking.
        var entity = MakeEntity(urgency: 0.9f, energy: 0.1f, sleeping: true);
        yield return null;

        var icon = _pop.TestComputeIcon(entity);

        Assert.AreEqual(IconKind.SleepZ, icon,
            "SleepZ should take precedence over Sweat when the NPC is sleeping.");
    }
}
