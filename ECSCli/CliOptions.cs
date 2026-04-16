namespace ECSCli;

/// <summary>
/// Parsed command-line configuration for the headless CLI runner.
/// </summary>
public class CliOptions
{
    /// <summary>Sim-time multiplier per tick. Higher = more sim-time per loop iteration.</summary>
    public float TimeScale { get; init; } = 1.0f;

    /// <summary>Stop after this many simulation-seconds have elapsed. Null = run until Ctrl+C.</summary>
    public double? Duration { get; init; } = null;

    /// <summary>Stop after exactly this many ticks. Null = no tick limit.</summary>
    public long? MaxTicks { get; init; } = null;

    /// <summary>Print a state snapshot every N simulation-seconds.</summary>
    public double SnapshotInterval { get; init; } = 10.0;

    /// <summary>When true, suppress mid-run snapshots; only print the final summary.</summary>
    public bool Quiet { get; init; } = false;

    // ─────────────────────────────────────────────────────────────────────────

    public static CliOptions Parse(string[] args)
    {
        float   timeScale = 1.0f;
        double? duration  = null;
        long?   maxTicks  = null;
        double  snapshot  = 10.0;
        bool    quiet     = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--timescale" or "-t" when i + 1 < args.Length:
                    float.TryParse(args[++i], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out timeScale);
                    break;

                case "--duration" or "-d" when i + 1 < args.Length:
                    if (double.TryParse(args[++i], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var dur))
                        duration = dur;
                    break;

                case "--ticks" when i + 1 < args.Length:
                    if (long.TryParse(args[++i], out var t))
                        maxTicks = t;
                    break;

                case "--snapshot" or "-s" when i + 1 < args.Length:
                    double.TryParse(args[++i], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out snapshot);
                    break;

                case "--quiet" or "-q":
                    quiet = true;
                    break;

                case "--help" or "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;

                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}  (run with --help)");
                    break;
            }
        }

        return new CliOptions
        {
            TimeScale        = timeScale,
            Duration         = duration,
            MaxTicks         = maxTicks,
            SnapshotInterval = snapshot,
            Quiet            = quiet,
        };
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            ECS Simulation Engine — Headless CLI Runner

            Usage:  ECSCli [options]

            Options:
              --timescale, -t <n>    Sim-time multiplier per tick.  Default: 1.0
                                     Higher values fast-forward the simulation
                                     (e.g. --timescale 60 runs 1 sim-minute per second of CPU).

              --duration,  -d <n>    Stop after N simulation-seconds have elapsed.
                                     Omit to run until Ctrl+C.

              --ticks          <n>   Stop after exactly N ticks (alternative to --duration).

              --snapshot,  -s <n>    Print a state snapshot every N simulation-seconds.
                                     Default: 10.  Use 0 to suppress all mid-run output.

              --quiet,     -q        Same as --snapshot 0; only the final summary prints.

              --help,      -h        Show this help and exit.

            Examples:
              ECSCli                                Run forever, snapshot every 10 sim-s
              ECSCli --duration 300 -s 30           Run 5 sim-minutes, snapshot every 30s
              ECSCli --timescale 60 --duration 300  Sprint 5 sim-min at 60x (wall: ~5s)
              ECSCli --ticks 10000 --quiet          10 000 ticks, final summary only
            """);
    }
}
