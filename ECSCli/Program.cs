using APIFramework.Core;
using ECSCli;
using System.Diagnostics;

// ── Parse CLI args ────────────────────────────────────────────────────────────
var options = CliOptions.Parse(args);

Console.WriteLine($"{SimVersion.Full}  —  Headless CLI");
Console.WriteLine($"  Timescale  : {options.TimeScale}x");
Console.WriteLine($"  Snapshot   : every {options.SnapshotInterval:F1} sim-seconds");

if (options.Duration.HasValue)
    Console.WriteLine($"  Duration   : {options.Duration:F1} sim-seconds then exit");
else if (options.MaxTicks.HasValue)
    Console.WriteLine($"  Ticks      : {options.MaxTicks:N0} then exit");
else
    Console.WriteLine("  Duration   : run until Ctrl+C");

if (options.Quiet)
    Console.WriteLine("  Output     : quiet (final summary only)");

Console.WriteLine();

// ── Boot simulation ───────────────────────────────────────────────────────────
//
// SimulationBootstrapper is the composition root — it wires every system and
// spawns the initial entities.  The CLI is just another "frontend" that drives
// Engine.Update() in its own loop, exactly like the Avalonia DispatcherTimer does.
//
var sim = new SimulationBootstrapper();
sim.Clock.TimeScale = options.TimeScale;

// ── Run state ─────────────────────────────────────────────────────────────────
long   tick         = 0;
double nextSnapshot = options.SnapshotInterval;
bool   running      = true;
var    wallStart    = Stopwatch.GetTimestamp();

// Graceful shutdown on Ctrl+C — prints a final snapshot before exiting
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;   // don't kill the process immediately; let the loop exit cleanly
    running  = false;
    Console.WriteLine();
    Console.WriteLine("  (Ctrl+C — stopping after this tick)");
};

// ── Main loop — runs as fast as the CPU allows ────────────────────────────────
//
// Why 1/60 as the fixed delta?
//   The Avalonia GUI drives the engine at ~60fps using 1/60 as its realDeltaTime.
//   We use the same value here so the physics feel identical regardless of frontend.
//   TimeScale then multiplies this inside Engine.Update() — so setting TimeScale=60
//   means each loop iteration advances 1 sim-second of biological time.
//
const float dt = 1f / 60f;

while (running)
{
    sim.Engine.Update(dt);
    tick++;

    // ── Snapshot ─────────────────────────────────────────────────────────────
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

// ── Final summary ─────────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("══ SIMULATION ENDED ════════════════════════════════════════");
CliRenderer.PrintSnapshot(sim, tick, wallStart);
Console.WriteLine();
