using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-06: LightState.Dying → halo at low base intensity with sporadic seed-deterministic drops.
///
/// Verifies:
///   - Dying source has lower average intensity than an On source at the same nominal intensity.
///   - At least one "drop to zero" occurs in a 200-tick window (at ~8% per-tick drop probability).
///   - Drop events are deterministic (same seed + tick = same value).
///   - Dying halo is still visible (GO is active).
/// </summary>
[TestFixture]
public class LightSourceHaloDyingTests
{
    private GameObject            _haloGo;
    private LightSourceHaloRenderer _haloRenderer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _haloGo       = new GameObject("HaloDying_Renderer");
        _haloRenderer = _haloGo.AddComponent<LightSourceHaloRenderer>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        DestroyAll("HaloDying_");
    }

    // ── AT-06a: Dying halo is visible ─────────────────────────────────────────

    [UnityTest]
    public IEnumerator DyingSource_HaloIsVisible()
    {
        _haloRenderer.InjectWorldState(BuildWorldState(
            BuildSource("src-dying", LightState.Dying, intensity: 80)));

        yield return null;

        Assert.IsTrue(_haloRenderer.IsHaloVisible("src-dying"),
            "LightState.Dying → halo GO should still be active.");
    }

    // ── AT-06b: Dying average intensity is much lower than On ─────────────────

    [Test]
    public void DyingIntensity_AverageIsLow()
    {
        const string id     = "src-dying-avg";
        const float  drop   = 0.08f;
        const float  base_  = 0.22f;

        double sum = 0;
        for (long tick = 0; tick < 1000; tick++)
            sum += LightSourceHaloRenderer.ComputeDyingAt(id, tick, drop, base_);

        float avg = (float)(sum / 1000);

        // Expected average ≈ baseFrac * (1 - dropProb) ≈ 0.22 * 0.92 ≈ 0.2024.
        Assert.Less(avg, 0.35f,
            $"Dying source average intensity should be < 0.35, got {avg:F3}.");
        Assert.Greater(avg, 0.05f,
            $"Dying source average intensity should be > 0.05 (not completely dead), got {avg:F3}.");
    }

    // ── AT-06c: At least one full drop occurs over 200 ticks ──────────────────

    [Test]
    public void DyingIntensity_HasAtLeastOneFullDrop()
    {
        const string id = "src-dying-drop";
        bool hadDrop = false;

        for (long tick = 0; tick < 200; tick++)
        {
            float v = LightSourceHaloRenderer.ComputeDyingAt(id, tick);
            if (v <= 0.001f)
            {
                hadDrop = true;
                break;
            }
        }

        Assert.IsTrue(hadDrop,
            "Dying source should drop to zero intensity at least once in 200 ticks " +
            "(expected ~8% per tick → p(at least one drop) ≈ 1 - 0.92^200 ≈ 1.0).");
    }

    // ── AT-06d: Drops are deterministic ───────────────────────────────────────

    [Test]
    public void DyingIntensity_IsDeterministic()
    {
        const string id   = "src-dying-det";
        const long   tick = 99999L;

        float first  = LightSourceHaloRenderer.ComputeDyingAt(id, tick);
        float second = LightSourceHaloRenderer.ComputeDyingAt(id, tick);

        Assert.AreEqual(first, second, 0.0001f,
            "ComputeDyingAt with same (id, tick) must return identical values.");
    }

    // ── AT-06e: Different source IDs have independent drop schedules ──────────

    [Test]
    public void DyingIntensity_DiffersBySourceId()
    {
        bool anyDiff = false;
        for (long t = 0; t < 50; t++)
        {
            float vA = LightSourceHaloRenderer.ComputeDyingAt("dying-A", t);
            float vB = LightSourceHaloRenderer.ComputeDyingAt("dying-B", t);
            if (!Mathf.Approximately(vA, vB))
            {
                anyDiff = true;
                break;
            }
        }

        Assert.IsTrue(anyDiff,
            "Different source IDs should have different drop patterns.");
    }

    // ── AT-06f: Dying values always in [0, 1] ────────────────────────────────

    [Test]
    public void DyingIntensity_AlwaysInRange()
    {
        const string id = "src-dying-range";
        for (long tick = 0; tick < 200; tick++)
        {
            float v = LightSourceHaloRenderer.ComputeDyingAt(id, tick);
            Assert.GreaterOrEqual(v, 0f, $"Tick {tick}: value below 0.");
            Assert.LessOrEqual(v,   1f, $"Tick {tick}: value above 1.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static LightSourceDto BuildSource(string id, LightState state, int intensity)
        => new LightSourceDto
        {
            Id                = id,
            State             = state,
            Intensity         = intensity,
            ColorTemperatureK = 4000,
            Kind              = LightKind.OverheadFluorescent,
            Position          = new TilePointDto { X = 5, Y = 5 },
            RoomId            = "room-1",
        };

    private static WorldStateDto BuildWorldState(params LightSourceDto[] sources)
        => new WorldStateDto
        {
            SchemaVersion = "0.3.0",
            LightSources  = new List<LightSourceDto>(sources),
        };

    private static void DestroyAll(string prefix)
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith(prefix))
                Object.Destroy(go);
    }
}
