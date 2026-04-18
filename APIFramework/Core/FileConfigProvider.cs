using APIFramework.Config;

namespace APIFramework.Core;

/// <summary>
/// Loads SimConfig from a JSON file on disk.
///
/// This is the production default — used by the Avalonia GUI and the CLI.
/// The path is resolved relative to the current directory; the typical
/// convention is to place SimConfig.json next to the executable or in the
/// solution root and rely on SimulationBootstrapper's FindConfigPath helper
/// to locate it by walking up the directory tree.
/// </summary>
public sealed class FileConfigProvider : IConfigProvider
{
    private readonly string _path;

    /// <param name="path">
    /// Absolute or relative path to the JSON config file.
    /// Default is "SimConfig.json" (resolved by the caller or the bootstrapper).
    /// </param>
    public FileConfigProvider(string path = "SimConfig.json")
    {
        _path = path;
    }

    /// <inheritdoc/>
    public SimConfig GetConfig() => SimConfig.Load(_path);
}
