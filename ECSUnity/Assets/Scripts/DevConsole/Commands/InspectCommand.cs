#if WARDEN
// InspectCommand.cs
// Dumps all known component data for a single entity, identified by Guid or display name.
//
// Sources of truth:
//   1. Entity components (IdentityComponent, PositionComponent, LifeStateComponent)
//      — read directly from the engine's EntityManager.
//   2. WorldStateDto (drives, physiology) — read from the last published snapshot.
//      The snapshot may lag up to one tick behind live component state.
//
// Usage:
//   inspect <npcId|name>
//
// Return conventions:
//   Plain multi-line string on success.
//   "ERROR: ..."  on any failure.

using System.Text;
using APIFramework.Components;

public sealed class InspectCommand : IDevConsoleCommand
{
    public string Name        => "inspect";
    public string Usage       => "inspect <npcId|name>";
    public string Description => "Print full component dump for an NPC.";
    public string[] Aliases   => System.Array.Empty<string>();

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length == 0) return "ERROR: Usage: " + Usage;
        if (ctx.Host == null)  return "ERROR: EngineHost not available.";

        var entity = FindEntity(args[0], ctx.Host);
        if (entity == null) return $"ERROR: Entity '{args[0]}' not found.";

        var sb = new StringBuilder();
        sb.AppendLine($"Entity: {entity.Id}");

        // -- IdentityComponent --
        if (entity.Has<IdentityComponent>())
        {
            var id = entity.Get<IdentityComponent>();
            sb.AppendLine($"  Name:  {id.Name}");
            if (!string.IsNullOrEmpty(id.Value))
                sb.AppendLine($"  Value: {id.Value}");
        }

        // -- PositionComponent --
        if (entity.Has<PositionComponent>())
        {
            var pos = entity.Get<PositionComponent>();
            sb.AppendLine($"  Position: ({pos.X:F2}, {pos.Y:F2}, {pos.Z:F2})");
        }

        // -- LifeStateComponent --
        if (entity.Has<LifeStateComponent>())
        {
            var ls = entity.Get<LifeStateComponent>();
            sb.AppendLine($"  LifeState:          {ls.State}");
            sb.AppendLine($"  LastTransitionTick: {ls.LastTransitionTick}");

            if (ls.State == LifeState.Incapacitated)
                sb.AppendLine($"  IncapacitatedBudget: {ls.IncapacitatedTickBudget}");

            if (ls.State == LifeState.Deceased)
                sb.AppendLine($"  CauseOfDeath: {ls.PendingDeathCause}");
        }

        // -- HumanTag (presence-only tag) --
        if (entity.Has<HumanTag>())
            sb.AppendLine($"  HumanTag: present");

        // -- WorldStateDto (drives, physiology) — snapshot data --
        var worldState = ctx.Host.WorldState;
        if (worldState?.Entities != null)
        {
            string idStr = entity.Id.ToString();
            foreach (var dto in worldState.Entities)
            {
                if (dto?.Id != idStr) continue;

                if (dto.Drives != null)
                    sb.AppendLine($"  Dominant drive: {dto.Drives.Dominant}");

                if (dto.Physiology != null)
                    sb.AppendLine($"  Energy: {dto.Physiology.Energy:F2}");

                break;
            }
        }

        // -- Engine tick at time of inspect --
        sb.AppendLine($"  (tick: {ctx.Host.TickCount})");

        return sb.ToString().TrimEnd();
    }

    // Tries Guid first, then falls back to case-insensitive IdentityComponent.Name match.
    private static APIFramework.Core.Entity FindEntity(string idOrName, EngineHost host)
    {
        if (host?.Engine?.Entities == null) return null;

        if (System.Guid.TryParse(idOrName, out var guid))
        {
            foreach (var e in host.Engine.Entities)
                if (e.Id == guid) return e;
        }

        string lower = idOrName.ToLowerInvariant();
        foreach (var e in host.Engine.Entities)
        {
            if (e.Has<IdentityComponent>())
            {
                var id = e.Get<IdentityComponent>();
                if (id.Name?.ToLowerInvariant() == lower) return e;
            }
        }

        return null;
    }
}
#endif
