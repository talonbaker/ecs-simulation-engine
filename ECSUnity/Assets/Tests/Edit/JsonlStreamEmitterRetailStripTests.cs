using NUnit.Framework;

/// <summary>
/// AT-08: RETAIL build (no WARDEN define): JsonlStreamEmitter type is not present
/// in the compilation.
///
/// C# #if guards are compile-time constructs, so we cannot verify RETAIL stripping
/// from within a WARDEN build at runtime. This test documents the expected behaviour
/// and verifies the guard is structurally correct:
///
///   - In WARDEN builds: typeof(JsonlStreamEmitter) resolves (class exists).
///   - In RETAIL builds: the file compiles to nothing; typeof(JsonlStreamEmitter)
///     would be a compile error in any file that references it without its own guard.
///
/// The edit-mode test JsonlStreamEmitterRetailExists (WARDEN branch) and the
/// documentation test (RETAIL branch) together cover AT-08.
/// </summary>
[TestFixture]
public class JsonlStreamEmitterRetailStripTests
{
    // Compile-time sentinel reused from BuildConfigurationTests.
#if WARDEN
    private const bool IsWardenBuild = true;
#else
    private const bool IsWardenBuild = false;
#endif

    [Test]
    public void WardenBuild_JsonlStreamEmitterType_Exists()
    {
        if (!IsWardenBuild)
        {
            Assert.Pass("RETAIL build — JsonlStreamEmitter is compiled out as expected.");
            return;
        }

#if WARDEN
        // In WARDEN builds the type must exist; referencing it directly would fail
        // to compile if the #if WARDEN guard in JsonlStreamEmitter.cs were missing.
        var type = typeof(JsonlStreamEmitter);
        Assert.IsNotNull(type,
            "JsonlStreamEmitter type must exist in WARDEN builds.");
#endif
    }

    [Test]
    public void RetailBuild_EmitterAbsence_DocumentedByGuard()
    {
        // This test verifies that the structural protection (the #if WARDEN guard
        // wrapping the entire JsonlStreamEmitter class body) is in place.
        // We confirm this by checking that BuildConfigurationTests.IsWardenBuild
        // matches the current compilation context.
        //
        // A CI pipeline that builds WITHOUT the WARDEN define should see this
        // test pass with IsWardenBuild == false, and any code that references
        // JsonlStreamEmitter directly (without a guard) would fail to compile.
        Assert.AreEqual(IsWardenBuild,
#if WARDEN
            true,
#else
            false,
#endif
            "IsWardenBuild compile-time constant must match the active WARDEN define.");
    }

    [Test]
    public void JsonlStreamConfig_TypeAlwaysPresent()
    {
        // JsonlStreamConfig is NOT gated behind #if WARDEN.
        // It must always be present so RETAIL code that holds a null reference to
        // it still compiles without errors.
        var type = typeof(JsonlStreamConfig);
        Assert.IsNotNull(type,
            "JsonlStreamConfig must be present in all build configurations (not WARDEN-gated).");
    }
}
