namespace APIFramework.Core;

public class SimulationClock
{
    public float DeltaTime;  // Actual time passed * TimeScale
    public float TimeScale;  // 1.0 is normal, 0.5 is slow-mo
    public double TotalTime; // Lifetime of the simulation
    public double TotalSimTime { get; private set; }

    public void Tick(float realDeltaTime)
    {
        // Apply the multiplier to the delta
        float scaledDelta = realDeltaTime * TimeScale;
        TotalSimTime += scaledDelta;
    }

    // This is what the Systems should use for their math
    public float GetScaledDelta(float realDeltaTime) => realDeltaTime * TimeScale;
}