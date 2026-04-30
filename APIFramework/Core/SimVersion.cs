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
    /// <summary>SemVer MAJOR component — incremented on breaking API, component-layout, or save-format changes.</summary>
    public const int Major = 0;

    /// <summary>SemVer MINOR component — incremented when new systems, components, or backward-compatible features land.</summary>
    public const int Minor = 7;

    /// <summary>SemVer PATCH component — incremented for bug fixes, tuning changes, and display improvements.</summary>
    public const int Patch = 2;

    /// <summary>Optional pre-release label. e.g. "-alpha", "-rc1". Empty for stable.</summary>
    public const string Label = "";

    /// <summary>Full SemVer version string, e.g. <c>"0.7.2"</c> (or <c>"0.7.2-rc1"</c> when <see cref="Label"/> is set).</summary>
    public static string Version => $"{Major}.{Minor}.{Patch}{Label}";

    /// <summary>Long-form display string suitable for window titles and CLI banners (<c>"ECS Simulation Engine  v{Version}"</c>).</summary>
    public static string Full    => $"ECS Simulation Engine  v{Version}";

    /// <summary>Short display string suitable for compact headers (<c>"v{Version}"</c>).</summary>
    public static string Short   => $"v{Version}";
}
