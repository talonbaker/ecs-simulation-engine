using APIFramework.Config;

namespace APIFramework.Core;

/// <summary>
/// Provides a SimConfig that is already in memory — no file I/O involved.
///
/// PRIMARY USE CASES
/// -----------------
/// - Unit / integration tests: construct a SimConfig programmatically and
///   inject it so tests don't need SimConfig.json on disk.
///
///   Example:
///     var cfg = SimConfig.Default();
///     cfg.Systems.Feeding.HungerThreshold = 10f;  // custom tuning for test
///     var sim = new SimulationBootstrapper(new InMemoryConfigProvider(cfg));
///
/// - Unity: build a SimConfig from a ScriptableObject and pass it here so
///   APIFramework has no dependency on Unity's filesystem or Resources API.
///
/// - Hot-override: replace specific sub-configs at startup without touching
///   the JSON file on disk.
/// </summary>
public sealed class InMemoryConfigProvider : IConfigProvider
{
    private readonly SimConfig _config;

    /// <param name="config">The already-constructed config to return.</param>
    public InMemoryConfigProvider(SimConfig config)
    {
        _config = config;
    }

    /// <inheritdoc/>
    public SimConfig GetConfig() => _config;
}
