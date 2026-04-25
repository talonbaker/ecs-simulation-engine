namespace APIFramework.Diagnostics;

/// <summary>
/// Records a single instance of a value escaping its valid range.
/// Produced by InvariantSystem and collected by SimMetrics.
/// </summary>
public readonly record struct InvariantViolation(
    double GameTime,       // game-seconds when the violation was detected
    string EntityName,     // name or short ID of the offending entity
    string Component,      // e.g. "MetabolismComponent"
    string Property,       // e.g. "Satiation"
    float  ActualValue,    // the bad value that was detected
    float  ClampedTo,      // value it was corrected to (same as min/max of range)
    float  ValidMin,       // the expected lower bound
    float  ValidMax        // the expected upper bound
)
{
    public override string ToString() =>
        $"{Component}.{Property} = {ActualValue:F4}  " +
        $"(clamped to {ClampedTo:F4}, valid [{ValidMin}, {ValidMax}])  " +
        $"entity: {EntityName}";
}
