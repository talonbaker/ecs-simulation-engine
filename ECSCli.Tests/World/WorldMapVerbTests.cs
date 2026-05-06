using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using Xunit;

#if WARDEN
using ECSCli.World;
#endif

namespace ECSCli.Tests.World;

#if WARDEN

/// <summary>
/// Integration tests for the <c>ECSCli world map</c> verb (WP-3.0.W AT-17).
///
/// AT-17 — verb compiles, runs against a baseline world, prints to stdout,
///         returns exit code 0.
///
/// Tests invoke <see cref="WorldCommand.Root"/> directly via <c>InvokeAsync</c>
/// — no subprocess. Stdout is captured by swapping <see cref="Console.Out"/>.
/// </summary>
// System.CommandLine beta's RootCommand singleton is not safe for concurrent
// InvokeAsync calls; serialize with the existing AI-verb tests.
[Collection("AiCommandSingleton")]
public sealed class WorldMapVerbTests
{
    // ── AT-17: verb runs and exits 0, prints non-empty map to stdout ──────────

    /// <summary>
    /// AT-17: <c>world map</c> renders the baseline world to stdout and exits 0.
    /// </summary>
    [Fact]
    public async Task WorldMap_BaselineWorld_PrintsMapAndExitsZero()
    {
        var origOut = Console.Out;
        try
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);

            int exit = await WorldCommand.Root.InvokeAsync(new[] { "map" });

            Console.SetOut(origOut);

            Assert.Equal(0, exit);

            var output = sw.ToString();
            Assert.False(string.IsNullOrWhiteSpace(output),
                "world map output must be non-empty.");

            // Header is always present on a successful render.
            Assert.Contains("WORLD MAP", output);

            // The outer double-line boundary is always emitted.
            Assert.Contains("╔", output);
            Assert.Contains("╝", output);
        }
        finally
        {
            Console.SetOut(origOut);
        }
    }

    /// <summary>
    /// AT-17 (--no-legend variant): the LEGEND section is omitted when requested.
    /// </summary>
    [Fact]
    public async Task WorldMap_NoLegendFlag_OmitsLegendSection()
    {
        var origOut = Console.Out;
        try
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);

            int exit = await WorldCommand.Root.InvokeAsync(new[] { "map", "--no-legend" });

            Console.SetOut(origOut);

            Assert.Equal(0, exit);
            Assert.DoesNotContain("LEGEND", sw.ToString());
        }
        finally
        {
            Console.SetOut(origOut);
        }
    }

    /// <summary>
    /// AT-17 (--help variant): help on the <c>map</c> sub-command exits 0.
    /// Confirms the option set is wired up cleanly.
    /// </summary>
    [Fact]
    public async Task WorldMap_HelpFlag_ExitsZero()
    {
        var origOut = Console.Out;
        try
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);

            int exit = await WorldCommand.Root.InvokeAsync(new[] { "map", "--help" });

            Console.SetOut(origOut);

            Assert.Equal(0, exit);
        }
        finally
        {
            Console.SetOut(origOut);
        }
    }
}

#endif
