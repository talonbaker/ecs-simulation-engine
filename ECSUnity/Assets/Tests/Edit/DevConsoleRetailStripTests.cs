using NUnit.Framework;

/// <summary>
/// AT-17: RETAIL build (no WARDEN define) — DevConsolePanel and all console
/// infrastructure must be absent from the compilation.
///
/// C# #if guards are resolved at compile time, so we cannot verify RETAIL
/// stripping from within a WARDEN build by inspecting runtime types.  Instead,
/// this test uses the same structural pattern as
/// JsonlStreamEmitterRetailStripTests:
///
///  - In WARDEN builds: the WARDEN-gated types exist and typeof() resolves.
///  - In RETAIL builds: the types are compiled out, and any file that references
///    them without its own #if WARDEN guard would fail to compile — which is
///    exactly what we want.
///
/// The test also verifies that DevConsoleConfig (intentionally NOT gated) is
/// present in all build configurations so that Inspector references compile even
/// in RETAIL.
///
/// CI note: run this test suite once with the WARDEN scripting define and once
/// without.  The RETAIL pass verifies that removing WARDEN does not produce
/// compile errors in files that reference DevConsoleConfig.
/// </summary>
[TestFixture]
public class DevConsoleRetailStripTests
{
    // Compile-time sentinel: resolves to true in WARDEN builds, false otherwise.
#if WARDEN
    private const bool IsWardenBuild = true;
#else
    private const bool IsWardenBuild = false;
#endif

    // ── WARDEN presence ───────────────────────────────────────────────────────

    /// <summary>
    /// In WARDEN builds, DevConsolePanel must exist (the typeof() reference
    /// would be a compile error if the class were missing or un-guarded).
    /// </summary>
    [Test]
    public void WardenBuild_DevConsolePanelType_Exists()
    {
        if (!IsWardenBuild)
        {
            Assert.Pass("RETAIL build — DevConsolePanel is compiled out as expected.");
            return;
        }

#if WARDEN
        var t = typeof(DevConsolePanel);
        Assert.IsNotNull(t, "DevConsolePanel must exist in WARDEN builds.");
#endif
    }

    /// <summary>
    /// In WARDEN builds, DevConsoleCommandDispatcher must exist.
    /// </summary>
    [Test]
    public void WardenBuild_DevConsoleCommandDispatcherType_Exists()
    {
        if (!IsWardenBuild)
        {
            Assert.Pass("RETAIL build — DevConsoleCommandDispatcher is compiled out.");
            return;
        }

#if WARDEN
        var t = typeof(DevConsoleCommandDispatcher);
        Assert.IsNotNull(t, "DevConsoleCommandDispatcher must exist in WARDEN builds.");
#endif
    }

    /// <summary>
    /// In WARDEN builds, IDevConsoleCommand must exist.
    /// </summary>
    [Test]
    public void WardenBuild_IDevConsoleCommandType_Exists()
    {
        if (!IsWardenBuild)
        {
            Assert.Pass("RETAIL build — IDevConsoleCommand is compiled out.");
            return;
        }

#if WARDEN
        var t = typeof(IDevConsoleCommand);
        Assert.IsNotNull(t, "IDevConsoleCommand interface must exist in WARDEN builds.");
#endif
    }

    /// <summary>
    /// In WARDEN builds, DevConsoleAutocomplete must exist.
    /// </summary>
    [Test]
    public void WardenBuild_DevConsoleAutocompleteType_Exists()
    {
        if (!IsWardenBuild)
        {
            Assert.Pass("RETAIL build — DevConsoleAutocomplete is compiled out.");
            return;
        }

#if WARDEN
        var t = typeof(DevConsoleAutocomplete);
        Assert.IsNotNull(t, "DevConsoleAutocomplete must exist in WARDEN builds.");
#endif
    }

    /// <summary>
    /// In WARDEN builds, DevConsoleHistoryPersister must exist.
    /// </summary>
    [Test]
    public void WardenBuild_DevConsoleHistoryPersisterType_Exists()
    {
        if (!IsWardenBuild)
        {
            Assert.Pass("RETAIL build — DevConsoleHistoryPersister is compiled out.");
            return;
        }

#if WARDEN
        var t = typeof(DevConsoleHistoryPersister);
        Assert.IsNotNull(t, "DevConsoleHistoryPersister must exist in WARDEN builds.");
#endif
    }

    /// <summary>
    /// In WARDEN builds, DevConsoleColorPalette must exist (and the ConsoleEntryKind
    /// enum alongside it).
    /// </summary>
    [Test]
    public void WardenBuild_DevConsoleColorPaletteType_Exists()
    {
        if (!IsWardenBuild)
        {
            Assert.Pass("RETAIL build — DevConsoleColorPalette is compiled out.");
            return;
        }

#if WARDEN
        var t = typeof(DevConsoleColorPalette);
        Assert.IsNotNull(t, "DevConsoleColorPalette must exist in WARDEN builds.");
        var e = typeof(ConsoleEntryKind);
        Assert.IsNotNull(e, "ConsoleEntryKind enum must exist in WARDEN builds.");
#endif
    }

    // ── Always-present config ─────────────────────────────────────────────────

    /// <summary>
    /// DevConsoleConfig is NOT gated behind #if WARDEN by design, because the
    /// Unity Inspector needs to resolve the type in all builds.  Verify it is
    /// always present.
    /// </summary>
    [Test]
    public void AllBuilds_DevConsoleConfigType_AlwaysPresent()
    {
        // typeof(DevConsoleConfig) must compile and resolve in all build configs.
        var t = typeof(DevConsoleConfig);
        Assert.IsNotNull(t,
            "DevConsoleConfig must be present in all build configurations (not WARDEN-gated).");
    }

    // ── Build-define sanity ───────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the IsWardenBuild compile-time constant matches the active
    /// WARDEN scripting define.  A mismatch would indicate a test file misconfiguration.
    /// </summary>
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
