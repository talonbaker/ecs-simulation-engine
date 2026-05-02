#if WARDEN
using APIFramework.Components;

/// <summary>
/// scenario throw &lt;prop-id&gt; at &lt;target&gt;
/// Applies ThrownVelocityComponent toward the target. Target = NPC name, x,y,z coords, or --here.
/// </summary>
public sealed class ThrowSubverb : IScenarioSubverb
{
    public string Name        => "throw";
    public string Usage       => "scenario throw <prop-id> at <npc-name|x,y,z|--here>";
    public string Description => "Launch a prop toward a target. Breakable items may shatter on impact.";

    private const float LaunchSpeed    = 8.0f;
    private const float DecayPerTick   = 0.05f;
    private const float RandomOffset   = 3.0f;

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length < 3)
            return "ERROR: Usage: " + Usage;

        if (ctx.Host?.Engine == null)
            return "ERROR: Engine not available.";

        if (ctx.MutationApi == null)
            return "ERROR: MutationApi not available.";

        // args: [0]=prop-id  [1]="at"  [2]=target
        if (!string.Equals(args[1], "at", System.StringComparison.OrdinalIgnoreCase))
            return "ERROR: Expected 'at' between prop and target. Usage: " + Usage;

        var prop = ScenarioArgParser.FindEntity(args[0], ctx.Host);
        if (prop == null)
            return $"ERROR: Prop '{args[0]}' not found.";

        if (!prop.Has<PositionComponent>())
            return $"ERROR: Prop '{args[0]}' has no PositionComponent.";

        var propPos = prop.Get<PositionComponent>();

        // Resolve target position.
        float targetX, targetZ;
        string targetStr = args[2];

        if (string.Equals(targetStr, "--here", System.StringComparison.OrdinalIgnoreCase))
        {
            var rng = new System.Random((int)ctx.Host.TickCount);
            targetX = propPos.X + (float)(rng.NextDouble() * 2 - 1) * RandomOffset;
            targetZ = propPos.Z + (float)(rng.NextDouble() * 2 - 1) * RandomOffset;
        }
        else if (TryParseCoords(targetStr, out float cx, out float cz))
        {
            targetX = cx;
            targetZ = cz;
        }
        else
        {
            var targetEntity = ScenarioArgParser.FindEntity(targetStr, ctx.Host);
            if (targetEntity == null)
                return $"ERROR: Target '{targetStr}' not found.";
            if (!targetEntity.Has<PositionComponent>())
                return $"ERROR: Target '{targetStr}' has no PositionComponent.";
            var tp = targetEntity.Get<PositionComponent>();
            targetX = tp.X;
            targetZ = tp.Z;
        }

        // Compute normalised velocity toward target.
        float dx = targetX - propPos.X;
        float dz = targetZ - propPos.Z;
        float len = System.MathF.Sqrt(dx * dx + dz * dz);
        if (len < 0.001f) { dx = 1f; dz = 0f; }
        else { dx /= len; dz /= len; }

        ctx.MutationApi.ThrowEntity(
            prop.Id,
            velocityX:    dx * LaunchSpeed,
            velocityZ:    dz * LaunchSpeed,
            velocityY:    0f,
            decayPerTick: DecayPerTick
        );

        return $"Prop '{args[0]}' thrown toward ({targetX:F1}, {targetZ:F1}).";
    }

    private static bool TryParseCoords(string s, out float x, out float z)
    {
        x = z = 0f;
        var parts = s.Split(',');
        if (parts.Length < 2) return false;
        return float.TryParse(parts[0], out x) && float.TryParse(parts[1], out z);
    }
}
#endif
