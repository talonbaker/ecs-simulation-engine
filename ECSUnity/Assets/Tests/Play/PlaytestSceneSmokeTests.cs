using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Play-mode smoke tests for PlaytestScene: loads the scene, ticks the engine for
/// 60 simulated seconds, and asserts basic scene health.
/// </summary>
public class PlaytestSceneSmokeTests
{
    private const string PlaytestSceneName = "PlaytestScene";
    private const float  SimDurationSeconds = 60f;
    private const int    ExpectedNpcCount   = 15;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return SceneManager.LoadSceneAsync(PlaytestSceneName, LoadSceneMode.Single);
        // Allow one frame for all Awake/Start/AfterSceneLoad to complete.
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        yield return null;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator PlaytestScene_LoadsWithoutExceptions()
    {
        // If scene loaded without throwing, this test passes. Exceptions during
        // load fail the test via Unity's LogAssert infrastructure.
        LogAssert.NoUnexpectedReceived();
        yield return null;
    }

    [UnityTest]
    public IEnumerator PlaytestScene_EngineHostIsAlive()
    {
        var host = Object.FindObjectOfType<EngineHost>();
        Assert.IsNotNull(host, "EngineHost must be present in PlaytestScene");
        Assert.IsNotNull(host.WorldState, "EngineHost.WorldState must be non-null after boot");
        yield return null;
    }

    [UnityTest]
    public IEnumerator PlaytestScene_NpcCountMatchesExpected()
    {
        yield return null; // let Start() complete

        var host = Object.FindObjectOfType<EngineHost>();
        Assert.IsNotNull(host, "EngineHost must be present");

        // Allow up to 3 seconds for NPC spawn to complete.
        float waited = 0f;
        while (waited < 3f)
        {
            if (host.WorldState?.Entities != null &&
                host.WorldState.Entities.Count >= ExpectedNpcCount)
                break;
            waited += Time.deltaTime;
            yield return null;
        }

        int actual = host.WorldState?.Entities?.Count ?? 0;
        Assert.AreEqual(ExpectedNpcCount, actual,
            $"Expected {ExpectedNpcCount} NPCs but found {actual} after boot");
    }

    [UnityTest]
    public IEnumerator PlaytestScene_TicksFor60SecondsWithoutException()
    {
        var host = Object.FindObjectOfType<EngineHost>();
        Assert.IsNotNull(host, "EngineHost must be present");

        long ticksBefore = host.TickCount;

        // Run at x4 to get through 60 sim seconds quickly.
        Time.timeScale = 4f;
        yield return new WaitForSeconds(SimDurationSeconds / 4f);
        Time.timeScale = 1f;

        long ticksAfter = host.TickCount;
        Assert.Greater(ticksAfter, ticksBefore,
            "Engine must have ticked during the 60-second simulated run");

        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator PlaytestScene_RoomRectangleRendererIsActive()
    {
        yield return null;
        var renderer = Object.FindObjectOfType<RoomRectangleRenderer>();
        Assert.IsNotNull(renderer, "RoomRectangleRenderer must be active in PlaytestScene");
        Assert.IsTrue(renderer.isActiveAndEnabled, "RoomRectangleRenderer must be enabled");
        yield return null;
    }

    [UnityTest]
    public IEnumerator PlaytestScene_NpcDotRendererIsActive()
    {
        yield return null;
        var renderer = Object.FindObjectOfType<NpcDotRenderer>();
        Assert.IsNotNull(renderer, "NpcDotRenderer must be active in PlaytestScene");
        Assert.IsTrue(renderer.isActiveAndEnabled, "NpcDotRenderer must be enabled");
        yield return null;
    }

    [UnityTest]
    public IEnumerator PlaytestScene_PlaytestSceneSeederIsPresent()
    {
        yield return null;
        var seeder = Object.FindObjectOfType<PlaytestSceneSeeder>();
        Assert.IsNotNull(seeder, "PlaytestSceneSeeder must be present in PlaytestScene");
        Assert.AreEqual("playtest-office.json", seeder.WorldDefinitionPath,
            "Seeder must reference playtest-office.json");
        yield return null;
    }

    [UnityTest]
    public IEnumerator PlaytestScene_FrameRateIsAcceptable()
    {
        // Warm up for 1 second before sampling.
        yield return new WaitForSeconds(1f);

        float[] samples = new float[60];
        for (int i = 0; i < 60; i++)
        {
            samples[i] = 1f / Time.deltaTime;
            yield return null;
        }

        // Sort for p95 (index 57 of 60 sorted samples).
        System.Array.Sort(samples);
        float p95 = samples[57];

        Assert.GreaterOrEqual(p95, 55f,
            $"p95 FPS must be >= 55 (got {p95:F1}). Check performance with 15 NPCs.");
    }
}
