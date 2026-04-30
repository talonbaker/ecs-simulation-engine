namespace APIFramework.Diagnostics;

/// <summary>
/// Records a single instance of a value escaping its valid range.
/// Produced by InvariantSystem and collected by SimMetrics.
/// </summary>
/// <param name="GameTime">Game-seconds when the violation was detected.</param>
/// <param name="EntityName">Name or short ID of the offending entity.</param>
/// <param name="Component">Component type the offending property lives on (e.g. "MetabolismComponent").</param>
/// <param name="Property">Property whose value escaped its valid range (e.g. "Satiation").</param>
/// <param name="ActualValue">The bad value that was detected before clamping.</param>
/// <param name="ClampedTo">Value the property was corrected to — typically the nearest range bound.</param>
/// <param name="ValidMin">Expected lower bound of the valid range.</param>
/// <param name="ValidMax">Expected upper bound of the valid range.</param>
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
    /// <summary>Returns a human-readable single-line description of the violation.</summary>
    /// <returns>A formatted string with the component, property, actual value, clamp target, valid range, and entity name.</returns>
    public override string ToString() =>
        $"{Component}.{Property} = {ActualValue:F4}  " +
        $"(clamped to {ClampedTo:F4}, valid [{ValidMin}, {ValidMax}])  " +
        $"entity: {EntityName}";
}
