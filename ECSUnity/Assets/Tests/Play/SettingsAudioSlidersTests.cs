using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-10: Audio slider values persist and are clamped to [0, 1].
/// </summary>
[TestFixture]
public class SettingsAudioSlidersTests
{
    private GameObject     _go;
    private SettingsPanel  _panel;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go    = new GameObject("SetAudio_Panel");
        _panel = _go.AddComponent<SettingsPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("SetAudio_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Default_MasterVolumeIsPoint7()
    {
        yield return null;
        Assert.AreEqual(0.7f, _panel.MasterVolume, 0.01f,
            "Default master volume should be 0.70 (from PlayerUIConfig default).");
    }

    [UnityTest]
    public IEnumerator SetMasterVolume_Persists()
    {
        _panel.SetMasterVolume(0.5f);
        yield return null;
        Assert.AreEqual(0.5f, _panel.MasterVolume, 0.01f,
            "SetMasterVolume(0.5f) should persist as 0.5.");
    }

    [UnityTest]
    public IEnumerator ClampMax_VolumeNotAbove1()
    {
        _panel.SetMasterVolume(1.5f);
        yield return null;
        Assert.LessOrEqual(_panel.MasterVolume, 1f,
            "Master volume must be clamped to a maximum of 1.0.");
    }

    [UnityTest]
    public IEnumerator ClampMin_VolumeNotBelow0()
    {
        _panel.SetMasterVolume(-0.5f);
        yield return null;
        Assert.GreaterOrEqual(_panel.MasterVolume, 0f,
            "Master volume must be clamped to a minimum of 0.0.");
    }
}
