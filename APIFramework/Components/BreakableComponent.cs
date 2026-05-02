namespace APIFramework.Components;

public enum BreakageBehavior
{
    Despawn         = 0,
    SpawnLiquidStain = 1,
    SpawnGlassShards = 2,
    SpawnDebris     = 3,
}

public struct BreakableComponent
{
    /// <summary>Hit energy (Joules) at or above which the object breaks. 0.5 * mass * v^2.</summary>
    public float HitEnergyThreshold;

    /// <summary>What happens to the entity when it breaks.</summary>
    public BreakageBehavior OnBreak;
}
