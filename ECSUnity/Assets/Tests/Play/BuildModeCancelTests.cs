using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-12: Esc / right-click during ghost preview → preview cleared, no mutation,
/// build mode remains on.
/// </summary>
[TestFixture]
public class BuildModeCancelTests
{
    private GameObject          _ctrlGo;
    private BuildModeController _ctrl;
    private GhostPreview        _ghost;
    private FakeWorldMutationApi _fakeApi;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _ctrlGo = new GameObject("BMCancel_Ctrl");
        _ctrl   = _ctrlGo.AddComponent<BuildModeController>();

        var ghostGo = new GameObject("BMCancel_Ghost");
        _ghost      = ghostGo.AddComponent<GhostPreview>();

        SetField(_ctrl, "_ghost", _ghost);

        _fakeApi = new FakeWorldMutationApi();
        _ctrl.InjectMutationApi(_fakeApi);
        _ctrl.SetBuildMode(true);
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("BMCancel_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Cancel_ClearsIntent()
    {
        // Start a placement intent.
        _ctrl.TestSelectPaletteEntry(MakeEntry());
        yield return null;
        Assert.AreEqual(BuildIntentKind.Placing, _ctrl.CurrentIntent.Kind);

        // Cancel.
        _ctrl.TestCancel();
        yield return null;

        Assert.AreEqual(BuildIntentKind.None, _ctrl.CurrentIntent.Kind,
            "Intent should be None after cancel.");
    }

    [UnityTest]
    public IEnumerator Cancel_DeactivatesGhost()
    {
        _ctrl.TestSelectPaletteEntry(MakeEntry());
        yield return null;

        _ctrl.TestCancel();
        yield return null;

        Assert.IsFalse(_ghost.IsActive, "Ghost should be inactive after cancel.");
    }

    [UnityTest]
    public IEnumerator Cancel_DoesNotCallSpawn()
    {
        _ctrl.TestSelectPaletteEntry(MakeEntry());
        yield return null;
        _ctrl.TestCancel();
        yield return null;

        Assert.AreEqual(0, _fakeApi.SpawnCount,
            "Cancel should not call SpawnStructural.");
    }

    [UnityTest]
    public IEnumerator Cancel_BuildModeRemainsOn()
    {
        _ctrl.TestSelectPaletteEntry(MakeEntry());
        yield return null;
        _ctrl.TestCancel();
        yield return null;

        Assert.IsTrue(_ctrl.IsBuildMode, "Build mode should remain active after cancel.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PaletteEntry MakeEntry() => new PaletteEntry
    {
        Label            = "Cancel Test Wall",
        TemplateIdString = "00000010-0000-0000-0000-000000000001",
        Category         = PaletteCategory.Structural,
    };

    private static void SetField(object target, string name, object value)
    {
        var f = target.GetType().GetField(name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        f?.SetValue(target, value);
    }
}
