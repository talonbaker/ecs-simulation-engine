#if WARDEN
using System;
using System.Collections.Generic;
using APIFramework.Components;

/// <summary>
/// scenario seed-bereavement &lt;npc-name&gt; &lt;count&gt;
/// Pre-populates the NPC's BereavementHistoryComponent with N synthetic mourned ids.
/// </summary>
public sealed class SeedBereavementSubverb : IScenarioSubverb
{
    public string Name        => "seed-bereavement";
    public string Usage       => "scenario seed-bereavement <npc-name|id> <count>";
    public string Description => "Pre-populate an NPC's bereavement history with N synthetic mourned ids.";

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length < 2)
            return "ERROR: Usage: " + Usage;

        if (ctx.Host?.Engine == null)
            return "ERROR: Engine not available.";

        var entity = ScenarioArgParser.FindEntity(args[0], ctx.Host);
        if (entity == null)
            return $"ERROR: Entity '{args[0]}' not found.";

        if (!int.TryParse(args[1], out int count) || count < 1)
            return $"ERROR: '{args[1]}' is not a valid positive integer.";

        // Get or create BereavementHistoryComponent.
        BereavementHistoryComponent history;
        if (entity.Has<BereavementHistoryComponent>())
        {
            history = entity.Get<BereavementHistoryComponent>();
            history.EncounteredCorpseIds ??= new HashSet<Guid>();
        }
        else
        {
            history = new BereavementHistoryComponent
            {
                EncounteredCorpseIds = new HashSet<Guid>(),
            };
        }

        for (int i = 0; i < count; i++)
            history.EncounteredCorpseIds.Add(Guid.NewGuid());

        entity.Add(history);

        string name = entity.Has<IdentityComponent>()
            ? entity.Get<IdentityComponent>().Name
            : entity.Id.ToString();

        return $"Added {count} synthetic mourned id(s) to '{name}'. " +
               $"Total: {history.EncounteredCorpseIds.Count}.";
    }
}
#endif
