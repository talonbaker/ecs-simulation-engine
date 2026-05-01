using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-18: Quiet conversation particles — structural and boundary tests.
///
/// Without a live EngineHost (host=null), no particles should ever spawn.
/// These tests verify the renderer's boundary conditions and accessor contracts.
/// </summary>
[TestFixture]
public class ConversationStreamQuietTests
{
    private GameObject                _go;
    private ConversationStreamRenderer _renderer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go       = new GameObject("ConvQuiet_Renderer");
        _renderer = _go.AddComponent<ConversationStreamRenderer>();
        // _host is a [SerializeField] private field left null — no particles will spawn.
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("ConvQuiet_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Default_NoParticles()
    {
        // With a null host the particle count should be zero immediately.
        yield return null;
        Assert.AreEqual(0, _renderer.ActiveParticleCount,
            "No particles should exist before any Update with a null EngineHost.");
    }

    [UnityTest]
    public IEnumerator NullHost_NoParticlesAfterSpawnInterval()
    {
        // Wait beyond the 0.8-second spawn interval — still no particles without a host.
        yield return new WaitForSeconds(0.9f);
        Assert.AreEqual(0, _renderer.ActiveParticleCount,
            "No particles should spawn when EngineHost is null.");
    }

    [UnityTest]
    public IEnumerator MaxParticlesZero_NeverSpawns()
    {
        // Force _maxParticles to 0 via reflection — the cap is respected.
        var field = typeof(ConversationStreamRenderer)
            .GetField("_maxParticles", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, "_maxParticles field must exist on ConversationStreamRenderer.");
        field.SetValue(_renderer, 0);

        yield return new WaitForSeconds(0.9f);

        Assert.AreEqual(0, _renderer.ActiveParticleCount,
            "With _maxParticles==0 no particles should ever be added.");
    }

    [UnityTest]
    public IEnumerator ActiveParticleCount_IsNonNegative()
    {
        yield return null;
        Assert.GreaterOrEqual(_renderer.ActiveParticleCount, 0,
            "ActiveParticleCount must always be >= 0.");
    }
}
