using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems.Chores;

/// <summary>
/// Phase: PreUpdate (0). Runs once per tick; idempotent.
/// Spawns one ChoreComponent entity per ChoreKind if none exists yet.
/// Links each chore to its world anchor by matching IdentityComponent.Name.
/// Also attaches ChoreHistoryComponent to NPC entities that lack one.
/// </summary>
public sealed class ChoreInitializerSystem : ISystem
{
    private static readonly IReadOnlyDictionary<ChoreKind, string> AnchorNames =
        new Dictionary<ChoreKind, string>
        {
            [ChoreKind.CleanMicrowave]     = "Microwave",
            [ChoreKind.CleanFridge]        = "Fridge",
            [ChoreKind.CleanBathroom]      = "Bathroom",
            [ChoreKind.TakeOutTrash]       = "Trash",
            [ChoreKind.RefillWaterCooler]  = "WaterCooler",
            [ChoreKind.RestockSupplyCloset]= "SupplyCloset",
            [ChoreKind.ReplaceToner]       = "Toner",
        };

    private readonly ChoreConfig _cfg;
    private bool _spawned;

    public ChoreInitializerSystem(ChoreConfig cfg)
    {
        _cfg = cfg;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        // Spawn chore entities once.
        if (!_spawned)
        {
            SpawnChoreEntities(em);
            _spawned = true;
        }

        // Attach ChoreHistoryComponent to NPC entities that lack one.
        foreach (var npc in em.Query<NpcTag>())
        {
            if (npc.Has<ChoreHistoryComponent>()) continue;
            npc.Add(new ChoreHistoryComponent
            {
                TimesPerformed       = new System.Collections.Generic.Dictionary<ChoreKind, int>(),
                TimesRefused         = new System.Collections.Generic.Dictionary<ChoreKind, int>(),
                AverageQuality       = new System.Collections.Generic.Dictionary<ChoreKind, float>(),
                WindowTimesPerformed = new System.Collections.Generic.Dictionary<ChoreKind, int>(),
                WindowStartDay       = new System.Collections.Generic.Dictionary<ChoreKind, int>(),
                LastRefusalTick      = 0,
            });
        }
    }

    private void SpawnChoreEntities(EntityManager em)
    {
        // Check which kinds already have a ChoreComponent entity.
        var existing = new HashSet<ChoreKind>();
        foreach (var e in em.GetAllEntities())
            if (e.Has<ChoreComponent>())
                existing.Add(e.Get<ChoreComponent>().Kind);

        foreach (ChoreKind kind in Enum.GetValues(typeof(ChoreKind)))
        {
            if (existing.Contains(kind)) continue;

            var anchorId = FindAnchorId(em, kind);
            var chore = em.CreateEntity();
            chore.Add(new ChoreComponent
            {
                Kind              = kind,
                CompletionLevel   = 1.0f,   // starts clean
                QualityOfLastExecution = 1.0f,
                LastDoneTick      = 0,
                NextScheduledTick = GetFrequencyTicks(kind),
                CurrentAssigneeId = Guid.Empty,
                TargetAnchorId    = anchorId,
            });
        }
    }

    private static Guid FindAnchorId(EntityManager em, ChoreKind kind)
    {
        if (!AnchorNames.TryGetValue(kind, out var name)) return Guid.Empty;
        foreach (var e in em.GetAllEntities())
            if (e.Has<IdentityComponent>() && e.Get<IdentityComponent>().Name == name)
                return e.Id;
        return Guid.Empty;
    }

    private long GetFrequencyTicks(ChoreKind kind) => kind switch
    {
        ChoreKind.CleanMicrowave     => _cfg.FrequencyTicks.CleanMicrowave,
        ChoreKind.CleanFridge        => _cfg.FrequencyTicks.CleanFridge,
        ChoreKind.CleanBathroom      => _cfg.FrequencyTicks.CleanBathroom,
        ChoreKind.TakeOutTrash       => _cfg.FrequencyTicks.TakeOutTrash,
        ChoreKind.RefillWaterCooler  => _cfg.FrequencyTicks.RefillWaterCooler,
        ChoreKind.RestockSupplyCloset=> _cfg.FrequencyTicks.RestockSupplyCloset,
        ChoreKind.ReplaceToner       => _cfg.FrequencyTicks.ReplaceToner,
        _                            => 7_200_000,
    };
}
