#if WARDEN
// TickRateCommand.cs
// Changes the engine tick rate by setting Unity's Time.fixedDeltaTime.
//
// Background:
//   The ECS simulation runs inside FixedUpdate (one engine tick per fixed update call).
//   Unity calls FixedUpdate at a rate of 1 / Time.fixedDeltaTime per real second.
//   Setting fixedDeltaTime = 1/50 gives 50 ticks/sec; 1/1 gives 1 tick/sec (slow-motion
//   debugging). The maximum is clamped at 1000 tps to avoid Unity's fixed-update budget.
//
// JSONL emitter cadence:
//   SetEmitEveryNTicks(n) controls how frequently the JSONL streamer flushes a snapshot.
//   We set n = tps so that one snapshot is emitted per real second regardless of tick rate.
//   This keeps the external analytics pipeline from being overwhelmed at high tick rates.
//
// Usage:
//   tick-rate <ticks-per-second>
//   tickrate <ticks-per-second>       (alias)
//
// Examples:
//   tick-rate 50     — normal simulation speed
//   tick-rate 1      — one tick per second (easy breakpoint debugging)
//   tick-rate 200    — fast-forward
//
// Return conventions:
//   Plain string on success.
//   "ERROR: ..."  on failure.

using UnityEngine;

public sealed class TickRateCommand : IDevConsoleCommand
{
    public string Name        => "tick-rate";
    public string Usage       => "tick-rate <ticks-per-second>";
    public string Description => "Set engine tick rate (changes Time.fixedDeltaTime). Also adjusts JSONL cadence.";
    public string[] Aliases   => new[] { "tickrate" };

    // Boundaries that keep Unity stable and the simulation observable.
    private const float MinTps = 1f;
    private const float MaxTps = 1000f;

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length == 0)
            return "ERROR: Usage: " + Usage;

        if (!float.TryParse(args[0],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float tps) || tps <= 0f)
            return $"ERROR: Invalid tick rate '{args[0]}'. Must be a positive number.";

        float clamped = Mathf.Clamp(tps, MinTps, MaxTps);

        // The key line: Unity will call FixedUpdate (and thus the ECS tick loop)
        // at this new interval from the next frame onward.
        Time.fixedDeltaTime = 1f / clamped;

        // Keep the JSONL emitter at approximately one snapshot per real second.
        // RoundToInt ensures n >= 1 even at very low tick rates.
        ctx.Emitter?.SetEmitEveryNTicks(Mathf.Max(1, Mathf.RoundToInt(clamped)));

        string clampNote = (tps != clamped)
            ? $" (clamped from {tps:F0} to {clamped:F0})"
            : "";

        return $"Tick rate set to {clamped:F0} tps{clampNote} " +
               $"(fixedDeltaTime = {Time.fixedDeltaTime:F4}s).";
    }
}
#endif
