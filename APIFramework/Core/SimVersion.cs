namespace APIFramework.Core;

/// <summary>
/// Single source of truth for the simulation version. Referenced by every
/// frontend (CLI, Avalonia, future) so the running version is always visible.
///
/// Follows Semantic Versioning — https://semver.org
///   MAJOR — breaking changes to the API, component layout, or save format
///   MINOR — new systems, components, or backward-compatible features
///   PATCH — bug fixes, tuning changes, output/display improvements
///
/// Bump this file when committing a meaningful change.
/// </summary>
public static class SimVersion
{
    public const int Major = 0;
    public const int Minor = 7;
    public const int Patch = 1;

    /// <summary>Optional pre-release label. e.g. "-alpha", "-rc1". Empty for stable.</summary>
    public const string Label = "";

    public static string Version => $"{Major}.{Minor}.{Patch}{Label}";
    public static string Full    => $"ECS Simulation Engine  v{Version}";
    public static string Short   => $"v{Version}";
}
