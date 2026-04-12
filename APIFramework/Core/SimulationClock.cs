namespace APIFramework.Core;

public class SimulationClock
{
    public float DeltaTime;  // Actual time passed * TimeScale
    public float TimeScale;  // 1.0 is normal, 0.5 is slow-mo
    public double TotalTime; // Lifetime of the simulation
}