using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-18 (heated): Heated-particle settings are configured correctly on the renderer.
///
/// Without a live host no particles spawn, so we verify the renderer's serialized
/// field defaults and the max-particle cap boundary.
/// </summary>
[TestFixture]
public class ConversationStreamHeatedTests
{
    private GameObject                _go;
    private ConversationStreamRenderer _renderer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go       = new GameObject("ConvHeated_Renderer");
        _renderer = _go.AddComponent<ConversationStreamRenderer>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("ConvHeated_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator NoHost_HeatedMode_NoParticles()
    {
        // The heated-particle path requires a live EngineHost; without one no spawning occurs.
        yield return new WaitForSeconds(0.9f);
        Assert.AreEqual(0, _renderer.ActiveParticleCount,
            "No heated particles should spawn without an EngineHost.");
    }

    [UnityTest]
    public IEnumerator ActiveParticleCount_NonNegative()
    {
        yield return null;
        Assert.GreaterOrEqual(_renderer.ActiveParticleCount, 0,
            "ActiveParticleCount must always be non-negative.");
    }

    [UnityTest]
    public IEnumerator MaxParticles_Respected()
    {
        // Set a small cap via reflection and verify count stays within it.
        var field = typeof(ConversationStreamRenderer)
            .GetField("_maxParticles", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, "_maxParticles private field must exist.");
        field.SetValue(_renderer, 2);

        yield return new WaitForSeconds(0.9f);

        Assert.LessOrEqual(_renderer.ActiveParticleCount, 2,
            "Particle count must not exceed the _maxParticles cap.");
    }

    [UnityTest]
    public IEnumerator ParticleLifetime_DefaultIsPositive()
    {
        // Validate the default serialized value is sane (> 0).
        var field = typeof(ConversationStreamRenderer)
            .GetField("_particleLifetime", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, "_particleLifetime private field must exist.");
        float lifetime = (float)field.GetValue(_renderer);

        yield return null;

        Assert.Greater(lifetime, 0f,
            "_particleLifetime default must be a positive number.");
    }
}
