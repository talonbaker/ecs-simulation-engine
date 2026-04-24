using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Diagnostics;
using ECSCli;
using ECSCli.Ai;
using System.CommandLine;   // CommandExtensions.InvokeAsync — used for `ai` verb delegation
using System.Diagnostics;

// ── AI verb delegation ────────────────────────────────────────────────────────
//
// If the first argument is "ai", hand control entirely to the System.CommandLine
// sub-tree and exit with its return code. The existing bespoke parser below is
// not invoked, preserving byte-identical behaviour for all other invocations.
//
if (args.Length > 0 && args[0] == "ai")
{
    int exitCode = await AiCommand.Root.InvokeAsync(args[1..]);
    Environment.Exit(exitCode);
}

// ── Parse CLI args ─────────────────────────────────────────────────────────────
var options = CliOptions.Parse(args);

Console.WriteLine($"{SimVersion.Full}  —  Headless CLI");
Console.WriteLine($"  Default TimeScale : loaded from SimConfig.json");

if (options.Duration.HasValue)
    Console.WriteLine($"  Duration         : {options.Duration:F0} game-seconds " +
                      $"({options.Duration.Value / 3600:F1} game-hours)");
else if (options.MaxTicks.HasValue)
    Console.WriteLine($"  Ticks            : {options.MaxTicks:N0} then exit");
else
    Console.WriteLine("  Duration         : run until Ctrl+C");

if (options.Quiet)
    Console.WriteLine("  Output           : quiet (final report only)");
else
    Console.WriteLine($"  Snapshot         : every {options.SnapshotInterval:F0} game-seconds" +
                      $" ({options.SnapshotInterval / 60:F0} game-minutes)");

Console.WriteLine();

// ── Boot simulation ───────────────────────────────────────────────────────────
//
// SimulationBootstrapper is the composition root — it wires every system and
// spawns the initial entities.  The CLI drives Engine.Update() in its own loop,
// exactly like the Avalonia DispatcherTimer does.
//
const string configFile = "SimConfig.json";
var sim = new SimulationBootstrapper(configFile);

// Override TimeScale only if the user explicitly passed --timescale
if (options.TimeScale != 1.0f)
{
    sim.Clock.TimeScale = options.TimeScale;
    Console.WriteLine($"  TimeScale override: {options.TimeScale}x");
}
Console.WriteLine($"  Active TimeScale : {sim.Clock.TimeScale}x  " +
                  $"(1 real-sec = {sim.Clock.TimeScale / 60:F1} game-min)");
Console.WriteLine();

// ── Hot-reload watcher ────────────────────────────────────────────────────────
//
// Runs on a background thread; sets a flag.  The main loop checks the flag at
// the start of each tick and applies the new config before any system runs.
// This ensures no tick ever reads a partially-applied config.
//
SimConfig? _pendingConfig = null;
var _pendingLock = new object();

string? configPath = FindConfigPath(configFile);
SimConfigWatcher? watcher = null;

if (configPath != null)
{
    watcher = new SimConfigWatcher(configPath, newCfg =>
    {
        lock (_pendingLock) _pendingConfig = newCfg;
        Console.WriteLine($"\n[Hot-reload] Change detected in {configFile} — applying next tick...");
    });
    Console.WriteLine($"[Hot-reload] Watching: {configPath}");
    Console.WriteLine($"             Edit & save SimConfig.json to tune values live.");
    Console.WriteLine();
}
else
{
    Console.WriteLine("[Hot-reload] SimConfig.json not found — hot-reload disabled.");
    Console.WriteLine();
}

// ── Metrics collector ─────────────────────────────────────────────────────────
var metrics = new SimMetrics(sim);

// ── Run state ─────────────────────────────────────────────────────────────────
long   tick         = 0;
double nextSnapshot = options.SnapshotInterval > 0 ? options.SnapshotInterval : double.MaxValue;
bool   running      = true;
var    wallStart    = Stopwatch.GetTimestamp();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    running  = false;
    Console.WriteLine();
    Console.WriteLine("  (Ctrl+C — finishing current tick then stopping)");
};

// ── Main loop ─────────────────────────────────────────────────────────────────
const float dt = 1f / 60f;

while (running)
{
    // ── Hot-reload: apply pending config BEFORE any system runs this tick ─────
    SimConfig? pending;
    lock (_pendingLock) { pending = _pendingConfig; _pendingConfig = null; }
    if (pending != null) sim.ApplyConfig(pending);

    // ── Advance simulation ────────────────────────────────────────────────────
    sim.Engine.Update(dt);
    tick++;

    // ── Collect metrics ───────────────────────────────────────────────────────
    metrics.Tick(tick);

    // ── Print live invariant violations ──────────────────────────────────────
    if (options.LiveViolations)
    {
        foreach (var v in sim.Invariants.FlushNewViolations())
            Console.WriteLine($"  ⚠  INVARIANT  {sim.Clock.DayTimeDisplay,-20}  {v}");
    }

    // ── Snapshot ──────────────────────────────────────────────────────────────
    if (!options.Quiet && sim.Clock.TotalTime >= nextSnapshot)
    {
        CliRenderer.PrintSnapshot(sim, tick, wallStart);
        nextSnapshot += options.SnapshotInterval;
    }

    // ── Exit conditions ───────────────────────────────────────────────────────
    if (options.MaxTicks.HasValue && tick >= options.MaxTicks.Value)
        break;

    if (options.Duration.HasValue && sim.Clock.TotalTime >= options.Duration.Value)
        break;
}

// ── Final snapshot + report ───────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("══ SIMULATION ENDED ════════════════════════════════════════════");
CliRenderer.PrintSnapshot(sim, tick, wallStart);

if (options.Report)
    CliRenderer.PrintReport(sim, metrics, tick, wallStart);

// ── Cleanup ───────────────────────────────────────────────────────────────────
watcher?.Dispose();
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────────

static string? FindConfigPath(string fileName)
{
    var dir = Directory.GetCurrentDirectory();
    for (int i = 0; i < 8; i++)
    {
        var candidate = Path.Combine(dir, fileName);
        if (File.Exists(candidate)) return candidate;
        var parent = Directory.GetParent(dir);
        if (parent == null) break;
        dir = parent.FullName;
    }
    return null;
}
