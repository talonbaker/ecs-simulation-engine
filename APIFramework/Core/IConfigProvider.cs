using APIFramework.Config;

namespace APIFramework.Core;

/// <summary>
/// Abstracts the act of obtaining a SimConfig, decoupling the simulation engine
/// from any specific loading strategy.
///
/// WHY THIS MATTERS
/// ----------------
/// SimulationBootstrapper originally hardcoded SimConfig.Load(path), which:
///   - Ties the engine to the filesystem — unit tests must have a real file on disk
///   - Prevents Unity from injecting a ScriptableObject-backed config
///   - Makes CLI tools hardcode a search-up-directory strategy
///
/// With IConfigProvider the bootstrapper's only job is wiring the simulation;
/// HOW config is obtained is now a separate concern injected at construction time.
///
/// Built-in implementations
/// ------------------------
///   FileConfigProvider        — reads SimConfig.json from disk (production default)
///   InMemoryConfigProvider    — wraps an already-constructed SimConfig (tests + Unity)
///
/// To add a new source (e.g. Unity ScriptableObject, web endpoint, command-line flags)
/// implement this interface and pass it to SimulationBootstrapper's constructor.
/// </summary>
public interface IConfigProvider
{
    /// <summary>Returns the SimConfig to use when the simulation starts.</summary>
    SimConfig GetConfig();
}
