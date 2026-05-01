using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AT-08: ChibiEmotionSlot.Show(IconKind) and Hide() execute without exception,
/// even when no sprite is loaded.
///
/// These are EDIT-MODE tests — no scene, no play loop. The slot is created and
/// exercised via AddComponent on a temporary GameObject, which triggers Awake().
/// Edit-mode tests can call Object.DestroyImmediate to clean up.
///
/// The core requirement: the stub slot API must never throw, so downstream systems
/// (NpcSilhouetteInstance, NpcAnimatorController) can call it unconditionally
/// without guard clauses.
/// </summary>
[TestFixture]
public class ChibiEmotionSlotApiTests
{
    private GameObject      _go;
    private ChibiEmotionSlot _slot;

    [SetUp]
    public void SetUp()
    {
        _go   = new GameObject("TestChibiSlot");
        _slot = _go.AddComponent<ChibiEmotionSlot>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Test]
    public void Show_Anger_DoesNotThrow_WhenNoSpriteLoaded()
    {
        Assert.DoesNotThrow(() => _slot.Show(IconKind.Anger),
            "Show(Anger) must not throw when no sprites are loaded.");
    }

    [Test]
    public void Show_AllKinds_DoNotThrow_WhenNoSpriteLoaded()
    {
        foreach (IconKind kind in System.Enum.GetValues(typeof(IconKind)))
        {
            Assert.DoesNotThrow(() => _slot.Show(kind),
                $"Show({kind}) must not throw when no sprites are loaded.");
        }
    }

    [Test]
    public void Hide_DoesNotThrow_WhenNotShowing()
    {
        Assert.DoesNotThrow(() => _slot.Hide(),
            "Hide() must not throw when slot is not showing anything.");
    }

    [Test]
    public void Hide_DoesNotThrow_AfterShow()
    {
        _slot.Show(IconKind.Anger);
        Assert.DoesNotThrow(() => _slot.Hide(),
            "Hide() must not throw after Show(Anger).");
    }

    [Test]
    public void Show_None_DelegatesToHide()
    {
        _slot.Show(IconKind.Anger);

        // Show(None) should be equivalent to Hide().
        Assert.DoesNotThrow(() => _slot.Show(IconKind.None));
        Assert.AreEqual(IconKind.None, _slot.CurrentIcon,
            "After Show(None), CurrentIcon must be None.");
    }

    [Test]
    public void CurrentIcon_StartsAsNone()
    {
        Assert.AreEqual(IconKind.None, _slot.CurrentIcon,
            "ChibiEmotionSlot must start with CurrentIcon = None.");
    }

    [Test]
    public void CurrentIcon_UpdatesAfterShow_EvenWithoutSprite()
    {
        // CurrentIcon must track what was requested, not whether a sprite was found.
        _slot.Show(IconKind.SleepZ);
        Assert.AreEqual(IconKind.SleepZ, _slot.CurrentIcon,
            "CurrentIcon must reflect the last Show() call even if no sprite is assigned.");
    }

    [Test]
    public void CurrentIcon_ReturnsNoneAfterHide()
    {
        _slot.Show(IconKind.Heart);
        _slot.Hide();
        Assert.AreEqual(IconKind.None, _slot.CurrentIcon,
            "CurrentIcon must be None after Hide().");
    }

    [Test]
    public void IsVisible_FalseWhenNoSpriteLoaded()
    {
        // Without a sprite, the SpriteRenderer is disabled even after Show().
        _slot.Show(IconKind.Exclamation);
        Assert.IsFalse(_slot.IsVisible,
            "IsVisible must be false when no sprite is loaded, even after Show().");
    }

    [Test]
    public void IsVisible_FalseAfterHide()
    {
        _slot.Hide();
        Assert.IsFalse(_slot.IsVisible,
            "IsVisible must be false after Hide().");
    }

    [Test]
    public void MultipleShowHide_Cycles_DoNotThrow()
    {
        for (int i = 0; i < 100; i++)
        {
            int idx = i % 9;  // cycle through all 9 IconKind values
            Assert.DoesNotThrow(() => _slot.Show((IconKind)idx));
            Assert.DoesNotThrow(() => _slot.Hide());
        }
    }
}
