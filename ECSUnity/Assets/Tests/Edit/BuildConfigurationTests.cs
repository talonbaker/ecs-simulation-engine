using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AT-12 / AT-13: WARDEN / RETAIL scripting define verification.
///
/// AT-12: In the editor build, WARDEN define is set; WorldStateProjectorAdapter
///        resolves to the Warden.Telemetry projection path.
///
/// AT-13: In RETAIL builds (WARDEN removed), the adapter resolves to InlineProjector.
///        This test cannot fully verify AT-13 at runtime (it requires rebuilding without
///        WARDEN), but it validates the conditional compilation structure by checking
///        the define is present in the editor and by verifying InlineProjector exists.
///
/// NOTE ON DEFINE DETECTION
/// ─────────────────────────
/// C# preprocessor symbols (#if WARDEN) are resolved at compile time, not runtime.
/// This test detects the active define by reading Unity's PlayerSettings API
/// (editor-only) and by a compile-time constant trick.
/// </summary>
[TestFixture]
public class BuildConfigurationTests
{
    // Compile-time sentinel: this field is true in WARDEN builds, false in RETAIL.
    // If neither define is set, it is false (treat as non-WARDEN).
#if WARDEN
    private const bool IsWardenBuild = true;
#else
    private const bool IsWardenBuild = false;
#endif

    [Test]
    public void EditorBuild_HasWardenDefine()
    {
        // In the Unity editor (default configuration), WARDEN must be set.
        // This test is categorised as an Edit-mode test, which always runs in the editor.
        Assert.IsTrue(IsWardenBuild,
            "The editor build must have the WARDEN scripting define set. " +
            "Check Project Settings > Player > Scripting Define Symbols.");
    }

    [Test]
    public void InlineProjector_TypeExists()
    {
        // InlineProjector must compile and be accessible regardless of the active define.
        // Its methods are always compiled; only the call path in WorldStateProjectorAdapter differs.
        var type = typeof(InlineProjector);
        Assert.IsNotNull(type, "InlineProjector type must exist in the compiled assembly.");
    }

    [Test]
    public void WorldStateProjectorAdapter_TypeExists()
    {
        var type = typeof(WorldStateProjectorAdapter);
        Assert.IsNotNull(type, "WorldStateProjectorAdapter type must exist.");
    }

    [Test]
    public void WardenBuild_ProjectorAdapterUsesWardenPath_CompileTimeVerification()
    {
        // This is a documentation test — it cannot verify the runtime routing path
        // without executing Project() in both WARDEN and RETAIL contexts.
        // The actual routing is enforced by InlineProjectorParityTests.
        //
        // What we verify here: if IsWardenBuild is true, the code compiled with WARDEN
        // and the Warden.Telemetry assembly must be loadable.
        if (IsWardenBuild)
        {
            var telemetryType = System.Type.GetType(
                "Warden.Telemetry.TelemetryProjector, Warden.Telemetry");
            Assert.IsNotNull(telemetryType,
                "In WARDEN builds, Warden.Telemetry.TelemetryProjector must be loadable. " +
                "Check that Warden.Telemetry.dll is in Assets/Plugins/.");
        }
        else
        {
            Assert.Pass("RETAIL build — Warden.Telemetry not expected. " +
                        "Verify InlineProjector parity via InlineProjectorParityTests.");
        }
    }
}
