#if WARDEN
using APIFramework.Core;

/// <summary>
/// scenario set-time &lt;time&gt;
/// Jumps the sim wall-clock to the specified time.
/// Accepts: morning (08:00), midday (12:00), dusk (18:00), night (22:00), or HH:MM (24-hour).
/// </summary>
public sealed class SetTimeSubverb : IScenarioSubverb
{
    public string Name        => "set-time";
    public string Usage       => "scenario set-time <morning|midday|dusk|night|HH:MM>";
    public string Description => "Jump the sim clock to a specified time of day.";

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length == 0)
            return "ERROR: Usage: " + Usage;

        if (ctx.Host?.Clock == null)
            return "ERROR: SimulationClock not available.";

        if (!TryResolveHour(args[0], out float targetHour))
            return $"ERROR: Unknown time '{args[0]}'. Valid: morning, midday, dusk, night, or HH:MM.";

        var clock = ctx.Host.Clock;

        // Compute new TotalTime for the target hour on the current game-day.
        // GameTimeOfDay = (TotalTime + StartOffsetSeconds) % SecondsPerDay
        // StartOffsetSeconds = DawnHour * 3600 = 6 * 3600 = 21600
        const float secsPerDay       = SimulationClock.SecondsPerDay;
        const float startOffsetSecs  = SimulationClock.DawnHour * 3600f;

        double currentBase  = System.Math.Floor((clock.TotalTime + startOffsetSecs) / secsPerDay) * secsPerDay;
        double newTotalTime = currentBase + targetHour * 3600.0 - startOffsetSecs;

        // If the target is behind current time-of-day, advance to next day.
        if (newTotalTime < clock.TotalTime)
            newTotalTime += secsPerDay;

        clock.SetTotalTime(newTotalTime);

        int h = (int)targetHour;
        int m = (int)((targetHour - h) * 60);
        string period = h >= 12 ? "PM" : "AM";
        int h12 = h == 0 ? 12 : (h > 12 ? h - 12 : h);
        return $"Sim time set to {h12}:{m:D2} {period}.";
    }

    private static bool TryResolveHour(string input, out float hour)
    {
        switch (input.ToLowerInvariant())
        {
            case "morning": hour = 8f;  return true;
            case "midday":  hour = 12f; return true;
            case "dusk":    hour = 18f; return true;
            case "night":   hour = 22f; return true;
        }

        // HH:MM format
        var parts = input.Split(':');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int hh) &&
            int.TryParse(parts[1], out int mm) &&
            hh >= 0 && hh < 24 && mm >= 0 && mm < 60)
        {
            hour = hh + mm / 60f;
            return true;
        }

        hour = 0f;
        return false;
    }
}
#endif
