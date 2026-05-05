namespace ECSCli;

/// <summary>
/// Parsed command-line configuration for the headless CLI runner.
/// </summary>
public class CliOptions
{
    /// <summary>Sim-time multiplier per tick. Higher = more sim-time per loop iteration.</summary>
    public float TimeScale { get; init; } = 1.0f;

    /// <summary>Stop after this many game-seconds have elapsed. Null = run until Ctrl+C.</summary>
    public double? Duration { get; init; } = null;

    /// <summary>Stop after exactly this many ticks. Null = no tick limit.</summary>
    public long? MaxTicks { get; init; } = null;

    /// <summary>Print a state snapshot every N game-seconds.</summary>
    public double SnapshotInterval { get; init; } = 600.0; // every 10 game-minutes

    /// <summary>When true, suppress mid-run snapshots; only print the final summary.</summary>
    public bool Quiet { get; init; } = false;

    /// <summary>When true, print a full metrics report at the end of the run.</summary>
    public bool Report { get; init; } = true;

    /// <summary>When true, print invariant violations immediately as they happen.</summary>
    public bool LiveViolations { get; init; } = true;

    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses a raw command-line argument vector into a populated
    /// <see cref="CliOptions"/> instance. Unknown flags are reported to
    /// <see cref="Console.Error"/> but do not cause exit; passing
    /// <c>--help</c> or <c>-h</c> prints help and terminates the process.
    /// </summary>
    /// <param name="args">The raw argument vector as supplied to <c>Main</c>.</param>
    /// <returns>An immutable <see cref="CliOptions"/> with each flag resolved.</returns>
    public static CliOptions Parse(string[] args)
    {
        float   timeScale      = 1.0f;
        double? duration       = null;
        long?   maxTicks       = null;
        double  snapshot       = 600.0;
        bool    quiet          = false;
        bool    report         = true;
        bool    liveViolations = true;

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
                    quiet          = true;
                    liveViolations = false;
                    break;

                case "--no-report":
                    report = false;
                    break;

                case "--no-violations":
                    liveViolations = false;
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
            Report           = report,
            LiveViolations   = liveViolations,
        };
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            ECS Simulation Engine — Headless CLI Runner  v0.5.0

            Usage:  ECSCli [options]

            Options:
              --timescale, -t <n>    Override the default TimeScale from SimConfig.json.
                                     At default 120x: 1 real second = 2 game minutes.
                                     --timescale 1200 runs 20 game-minutes per real second.

              --duration,  -d <n>    Stop after N GAME-seconds have elapsed.
                                     E.g. --duration 86400 = run one full game-day.

              --ticks          <n>   Stop after exactly N ticks (alternative to --duration).

              --snapshot,  -s <n>    Print a snapshot every N game-seconds.
                                     Default: 600 (every 10 game-minutes).
                                     Use -s 0 to suppress all mid-run snapshots.

              --quiet,     -q        Suppress mid-run snapshots AND live violation output.
                                     Only the final report and summary print.

              --no-report            Skip the end-of-run metrics report.
              --no-violations        Don't print violations as they happen (still in report).

              --help,      -h        Show this help and exit.

            Balancing workflow:
              1. Start a long run:  ECSCli --duration 172800       (2 game-days)
              2. Watch the output.  Edit SimConfig.json and save.
              3. The running sim hot-reloads instantly — no restart needed.
              4. Read the end-of-run report to see if the rhythm improved.

            Examples:
              ECSCli                              Run forever, snapshot every 10 game-min
              ECSCli -d 86400 -s 3600             One game-day, snapshot every game-hour
              ECSCli -d 172800 --quiet            Two game-days, report only at end
              ECSCli --ticks 100000 --no-report   Benchmark: 100k ticks, no overhead
            """);
    }
}
