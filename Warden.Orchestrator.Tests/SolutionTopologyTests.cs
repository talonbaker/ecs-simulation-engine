using System.Reflection;
using Xunit;

namespace Warden.Orchestrator.Tests;

public class SolutionTopologyTests
{
    /// <summary>
    /// Warden.Orchestrator must not reference APIFramework directly.
    ///
    /// Warden.Telemetry IS permitted as of WP-3.0.W.1: the WARDEN-gated MapSlabFactory
    /// uses AsciiMapProjector (a pure-function text renderer) from Warden.Telemetry.
    /// AsciiMapProjector has no simulation-state side effects; the architectural concern
    /// ("temptation to reach in and manipulate state") does not apply to it.
    /// The direct-APIFramework prohibition remains in force.
    /// </summary>
    [Fact]
    public void Orchestrator_Has_No_Direct_APIFramework_Reference()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("Cannot resolve assembly directory.");

        var repoRoot = assemblyDir;
        for (int i = 0; i < 4; i++)
            repoRoot = Path.GetDirectoryName(repoRoot)
                ?? throw new InvalidOperationException($"Cannot navigate up {i + 1} levels from {assemblyDir}.");

        var csprojPath = Path.Combine(repoRoot, "Warden.Orchestrator", "Warden.Orchestrator.csproj");

        Assert.True(File.Exists(csprojPath),
            $"Could not find Warden.Orchestrator.csproj at expected path: {csprojPath}");

        var content = File.ReadAllText(csprojPath);

        Assert.DoesNotContain("APIFramework", content,
            StringComparison.OrdinalIgnoreCase);
    }
}
