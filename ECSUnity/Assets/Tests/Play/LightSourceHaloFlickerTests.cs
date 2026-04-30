using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-05: LightState.Flickering → halo intensity oscillates deterministically.
///
/// Verifies:
///   - Flickering source produces varying intensities over 60 consecutive ticks.
///   - Same (sourceId, tick) → same intensity (determinism guarantee).
///   - Flickering source is always marked as "visible" (halo GO active = true).
/// </summary>
[TestFixture]
public class LightSourceHaloFlickerTests
{
    private GameObject            _haloGo;
    private LightSourceHaloRenderer _haloRenderer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _haloGo       = new GameObject("HaloFlicker_Renderer");
        _haloRenderer = _haloGo.AddComponent<LightSourceHaloRenderer>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        DestroyAll("HaloFlicker_");
    }

    // ── AT-05a: Flickering halo is visible ────────────────────────────────────

    [UnityTest]
    public IEnumerator FlickeringSource_HaloIsVisible()
    {
        _haloRenderer.InjectWorldState(BuildWorldState(
            BuildSource("src-flicker", LightState.Flickering, intensity: 80)));

        yield return null;

        Assert.IsTrue(_haloRenderer.IsHaloVisible("src-flicker"),
            "LightState.Flickering → halo GO should be active.");
    }

    // ── AT-05b: Flickering produces varying intensity over 60 ticks ───────────

    [Test]
    public void FlickerIntensity_VariesOverTicks()
    {
        const string id = "src-flicker-vary";
        const float  freq = 0.07f;
        const float  noise = 0.35f;

        float minSeen = float.MaxValue;
        float maxSeen = float.MinValue;

        for (long tick = 0; tick < 60; tick++)
        {
            float v = LightSourceHaloRenderer.ComputeFlickerAt(id, tick, freq, noise);
            if (v < minSeen) minSeen = v;
            if (v > maxSeen) maxSeen = v;
        }

        float range = maxSeen - minSeen;
        Assert.Greater(range, 0.05f,
            $"Flicker intensity range over 60 ticks should be > 5% (got {range:F3}). " +
            "A truly flickering light must vary noticeably.");
    }

    // ── AT-05c: Flicker is deterministic — same seed + tick = same value ──────

    [Test]
    public void FlickerIntensity_IsDeterministic()
    {
        const string id    = "src-det-flicker";
        const long   tick  = 12345L;
        const float  freq  = 0.07f;
        const float  noise = 0.35f;

        float first  = LightSourceHaloRenderer.ComputeFlickerAt(id, tick, freq, noise);
        float second = LightSourceHaloRenderer.ComputeFlickerAt(id, tick, freq, noise);

        Assert.AreEqual(first, second, 0.0001f,
            "ComputeFlickerAt with same (id, tick) must return identical values.");
    }

    // ── AT-05d: Different source IDs produce different flicker patterns ────────

    [Test]
    public void FlickerIntensity_DiffersBySourceId()
    {
        // Two different IDs at the same tick should produce different patterns on average.
        const long tick = 42L;

        float v1 = LightSourceHaloRenderer.ComputeFlickerAt("light-A", tick);
        float v2 = LightSourceHaloRenderer.ComputeFlickerAt("light-B", tick);

        // They might coincidentally be equal at one tick; check across 10 ticks.
        bool anyDiff = false;
        for (long t = 0; t < 10; t++)
        {
            if (!Mathf.Approximately(
                LightSourceHaloRenderer.ComputeFlickerAt("light-A", t),
                LightSourceHaloRenderer.ComputeFlickerAt("light-B", t)))
            {
                anyDiff = true;
                break;
            }
        }

        Assert.IsTrue(anyDiff,
            "Different source IDs should produce different flicker patterns.");
        _ = v1; _ = v2;   // suppress unused-variable warning
    }

    // ── AT-05e: Flicker values are always in [0, 1] ────────────────────────────

    [Test]
    public void FlickerIntensity_AlwaysInRange()
    {
        const string id = "src-range-check";
        for (long tick = 0; tick < 200; tick++)
        {
            float v = LightSourceHaloRenderer.ComputeFlickerAt(id, tick);
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
