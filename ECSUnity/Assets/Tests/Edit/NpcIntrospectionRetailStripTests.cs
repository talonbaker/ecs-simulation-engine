using NUnit.Framework;

/// <summary>
/// AT-10: Confirms that NpcIntrospectionOverlay, NpcIntrospectionToggle,
/// IntrospectCommand, and IntrospectionStatusPill are present in WARDEN builds
/// and compile out cleanly in RETAIL builds.
///
/// Pattern mirrors DevConsoleRetailStripTests and JsonlStreamEmitterRetailStripTests:
/// the typeof() references below are compile-time sentinels. In a RETAIL (non-WARDEN)
/// build these classes are absent; any file that referenced them without its own
/// #if WARDEN guard would fail to compile — which is exactly what we want to verify.
/// </summary>
[TestFixture]
public class NpcIntrospectionRetailStripTests
{
#if WARDEN
    private const bool IsWardenBuild = true;
#else
    private const bool IsWardenBuild = false;
#endif

    [Test]
    public void WardenBuild_NpcIntrospectionOverlayType_Exists()
    {
        if (!IsWardenBuild)
        {
            Assert.Pass("RETAIL build — NpcIntrospectionOverlay is compiled out as expected.");
            return;
        }

#if WARDEN
        var t = typeof(NpcIntrospectionOverlay);
        Assert.IsNotNull(t, "NpcIntrospectionOverlay must exist in WARDEN builds.");
#endif
    }

    [Test]
    public void WardenBuild_NpcIntrospectionModeEnum_Exists()
    {
        if (!IsWardenBuild)
        {
            Assert.Pass("RETAIL build — NpcIntrospectionMode is compiled out as expected.");
            return;
        }

#if WARDEN
        var t = typeof(NpcIntrospectionMode);
        Assert.IsNotNull(t, "NpcIntrospectionMode enum must exist in WARDEN builds.");
        Assert.IsTrue(System.Enum.IsDefined(typeof(NpcIntrospectionMode), "Off"),
            "NpcIntrospectionMode.Off must be defined.");
        Assert.IsTrue(System.Enum.IsDefined(typeof(NpcIntrospectionMode), "Selected"),
            "NpcIntrospectionMode.Selected must be defined.");
        Assert.IsTrue(System.Enum.IsDefined(typeof(NpcIntrospectionMode), "All"),
            "NpcIntrospectionMode.All must be defined.");
#endif
    }

    [Test]
    public void WardenBuild_NpcIntrospectionToggleType_Exists()
    {
        if (!IsWardenBuild)
        {
            Assert.Pass("RETAIL build — NpcIntrospectionToggle is compiled out as expected.");
            return;
        }

#if WARDEN
        var t = typeof(NpcIntrospectionToggle);
        Assert.IsNotNull(t, "NpcIntrospectionToggle must exist in WARDEN builds.");
#endif
    }

    [Test]
    public void WardenBuild_IntrospectCommandType_Exists()
    {
        if (!IsWardenBuild)
        {
            Assert.Pass("RETAIL build — IntrospectCommand is compiled out as expected.");
            return;
        }

#if WARDEN
        var t = typeof(IntrospectCommand);
        Assert.IsNotNull(t, "IntrospectCommand must exist in WARDEN builds.");
#endif
    }

    [Test]
    public void WardenBuild_IntrospectionStatusPillType_Exists()
    {
        if (!IsWardenBuild)
        {
            Assert.Pass("RETAIL build — IntrospectionStatusPill is compiled out as expected.");
            return;
        }

#if WARDEN
        var t = typeof(IntrospectionStatusPill);
        Assert.IsNotNull(t, "IntrospectionStatusPill must exist in WARDEN builds.");
#endif
    }

    [Test]
    public void WardenBuild_NpcIntrospectionTextRowType_Exists()
    {
        if (!IsWardenBuild)
        {
            Assert.Pass("RETAIL build — NpcIntrospectionTextRow is compiled out as expected.");
            return;
        }

#if WARDEN
        var t = typeof(NpcIntrospectionTextRow);
        Assert.IsNotNull(t, "NpcIntrospectionTextRow must exist in WARDEN builds.");
#endif
    }

    [Test]
    public void IsWardenBuild_MatchesActiveDefine()
    {
        Assert.AreEqual(IsWardenBuild,
#if WARDEN
            true,
#else
            false,
#endif
            "IsWardenBuild compile-time constant must match the active WARDEN define.");
    }
}
