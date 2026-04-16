namespace APIFramework.Components;

/// <summary>
/// The biological desire currently winning the priority queue.
/// BrainSystem writes this every tick. Action systems read it to decide
/// whether it is their turn to act.
/// Named DesireType (not DriveType) to avoid collision with System.IO.DriveType.
/// </summary>
public enum DesireType
{
    None,
    Eat,
    Drink,
    Sleep,      // Scored once EnergyComponent is implemented
    // Future: Pee, Socialise, Play, Flee, ...
}

/// <summary>
/// Stores the urgency score (0.0–1.0) for every drive this entity has.
/// Written by BrainSystem each tick. Read by action systems (FeedingSystem,
/// DrinkingSystem, SleepSystem, etc.) to decide if they should act.
///
/// Score formula:  urgency = (raw_sensation / 100) * maxScore
/// A score of 1.0 is life-or-death — no other drive can outbid it.
/// </summary>
public struct DriveComponent
{
    /// <summary>Urgency to eat. Driven by MetabolismComponent.Hunger.</summary>
    public float EatUrgency;

    /// <summary>Urgency to drink. Driven by MetabolismComponent.Thirst.</summary>
    public float DrinkUrgency;

    /// <summary>Urgency to sleep. Driven by fatigue (future: EnergyComponent).</summary>
    public float SleepUrgency;

    // ── Dominant drive ───────────────────────────────────────────────────────

    /// <summary>
    /// The single drive with the highest urgency score this tick.
    /// BrainSystem guarantees this is always current before action systems run.
    /// Ties favour the drive listed first (Eat > Drink > Sleep) — a known
    /// limitation to be replaced by a weighted tiebreaker in future.
    /// </summary>
    public readonly DesireType Dominant
    {
        get
        {
            float max = MathF.Max(EatUrgency, MathF.Max(DrinkUrgency, SleepUrgency));
            if (max < 0.001f)            return DesireType.None;
            if (EatUrgency   >= max - float.Epsilon) return DesireType.Eat;
            if (DrinkUrgency >= max - float.Epsilon) return DesireType.Drink;
            if (SleepUrgency >= max - float.Epsilon) return DesireType.Sleep;
            return DesireType.None;
        }
    }

    public override string ToString() =>
        $"Dominant: {Dominant}  (eat {EatUrgency:F2}  drink {DrinkUrgency:F2}  sleep {SleepUrgency:F2})";
}
