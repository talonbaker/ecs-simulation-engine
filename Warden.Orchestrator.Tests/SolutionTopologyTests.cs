using System.Reflection;

namespace Warden.Orchestrator.Tests;

public class SolutionTopologyTests
{
    /// <summary>
    /// AT-06: Warden.Orchestrator must not reference APIFramework or Warden.Telemetry.
    /// The orchestrator spawns ECSCli as a subprocess; it must never take a compile-time
    /// dependency on the simulation engine (see 03-naming-conventions.md §4).
    /// </summary>
    [Fact]
    public void Orchestrator_Has_No_Engine_Reference()
    {
        // Walk up from the test assembly's directory to find the repo root,
        // then locate the csproj by known relative path.
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("Cannot resolve assembly directory.");

        // The output layout is:  <repo>/Warden.Orchestrator.Tests/bin/<cfg>/<tfm>/
        // Four levels up lands at the repo root.
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

        Assert.DoesNotContain("Warden.Telemetry", content,
            StringComparison.OrdinalIgnoreCase);
    }
}
