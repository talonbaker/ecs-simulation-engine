using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Phase: PreUpdate — generates new task entities once per game-day at the configured hour.
/// Tasks are assigned round-robin to NPCs with available WorkloadComponent capacity,
/// in ascending EntityIntId order for determinism.
/// Uses SeededRandom exclusively; never System.Random.
/// </summary>
public class TaskGeneratorSystem : ISystem
{
    private readonly WorkloadConfig  _cfg;
    private readonly SimulationClock _clock;
    private readonly SeededRandom    _rng;

    private int _lastGenerationDay = -1;

    public TaskGeneratorSystem(WorkloadConfig cfg, SimulationClock clock, SeededRandom rng)
    {
        _cfg   = cfg;
        _clock = clock;
        _rng   = rng;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        int day = _clock.DayNumber;
        if (day == _lastGenerationDay) return;
        if (_clock.GameHour < _cfg.TaskGenerationHourOfDay) return;

        _lastGenerationDay = day;
        GenerateTasks(em);
    }

    private void GenerateTasks(EntityManager em)
    {
        // Build round-robin pool: NPCs with capacity, sorted ascending by EntityIntId.
        var available = em.Query<NpcTag>()
            .Where(e => e.Has<WorkloadComponent>())
            .OrderBy(WillpowerSystem.EntityIntId)
            .ToList();

        if (available.Count == 0) return;

        long now = (long)_clock.TotalTime;
        int  rrIndex = 0;

        for (int i = 0; i < _cfg.TaskGenerationCountPerDay; i++)
        {
            // Find next NPC with capacity.
            Entity? assignee = null;
            for (int attempt = 0; attempt < available.Count; attempt++)
            {
                var candidate = available[(rrIndex + attempt) % available.Count];
                var wl = candidate.Get<WorkloadComponent>();
                int activeCount = wl.ActiveTasks?.Count ?? 0;
                if (activeCount < wl.Capacity)
                {
                    assignee  = candidate;
                    rrIndex   = (rrIndex + attempt + 1) % available.Count;
                    break;
                }
            }

            float effort   = _rng.NextFloatRange(_cfg.TaskEffortHoursMin, _cfg.TaskEffortHoursMax);
            float dlHours  = _rng.NextFloatRange(_cfg.TaskDeadlineHoursMin, _cfg.TaskDeadlineHoursMax);
            int   priority = _cfg.TaskPriorityMin + _rng.NextInt(_cfg.TaskPriorityMax - _cfg.TaskPriorityMin + 1);

            var task = em.CreateEntity();
            task.Add(new TaskTag());
            task.Add(new TaskComponent
            {
                EffortHours    = effort,
                DeadlineTick   = now + (long)(dlHours * 3600f),
                Priority       = priority,
                Progress       = 0f,
                QualityLevel   = 1f,
                AssignedNpcId  = assignee != null ? assignee.Id : Guid.Empty,
                CreatedTick    = now
            });

            if (assignee != null)
            {
                var wl     = assignee.Get<WorkloadComponent>();
                var tasks  = new List<Guid>(wl.ActiveTasks ?? Array.Empty<Guid>()) { task.Id };
                assignee.Add(new WorkloadComponent
                {
                    ActiveTasks = tasks,
                    Capacity    = wl.Capacity,
                    CurrentLoad = ComputeLoad(tasks.Count, wl.Capacity)
                });
            }
        }
    }

    private static int ComputeLoad(int count, int capacity) =>
        capacity > 0 ? Math.Clamp(count * 100 / capacity, 0, 100) : 0;
}
