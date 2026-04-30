using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Movement;
using APIFramework.Systems.Spatial;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// Detects when an Alive NPC is locked in a room with no reachable exit and high hunger.
/// Runs once per game-day (gated by LockoutCheckHour), not every tick.
///
/// For each Alive NPC:
/// 1. Check if hunger is high enough (Satiation &lt;= 100 - threshold).
/// 2. If yes, query pathfinding service for reachability to any outdoor exit.
/// 3. If no path exists AND hunger is high:
///    - Attach LockedInComponent if not already present.
/// 4. If already locked in, decrement StarvationTickBudget (once per day).
/// 5. If budget expires, transition to Deceased(StarvedAlone).
///
/// Determinism contract: pathfinding is deterministic; exit set is built at boot and updates only
/// on StructuralChangeBus emissions. Two runs with same world state produce same outcomes.
/// </summary>
public class LockoutDetectionSystem : ISystem
{
    private readonly EntityManager _entityManager;
    private readonly SimulationClock _clock;
    private readonly LockoutConfig _config;
    private readonly PathfindingService _pathfindingService;
    private readonly LifeStateTransitionSystem _lifeStateTransitionSystem;
    private readonly SeededRandom _rng;

    // Cache of exit tiles (outdoor anchor tiles); built once and updated on structural change
    private List<(int X, int Y)> _exitTiles = new();
    private int _lastExitCacheDay = -1;

    public LockoutDetectionSystem(
        EntityManager entityManager,
        SimulationClock clock,
        SimConfig config,
        PathfindingService pathfindingService,
        LifeStateTransitionSystem lifeStateTransitionSystem,
        SeededRandom rng)
    {
        _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _config = config?.Lockout ?? throw new ArgumentNullException(nameof(config));
        _pathfindingService = pathfindingService ?? throw new ArgumentNullException(nameof(pathfindingService));
        _lifeStateTransitionSystem = lifeStateTransitionSystem ?? throw new ArgumentNullException(nameof(lifeStateTransitionSystem));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
    }

    public void Update(EntityManager em, float deltaTime)
    {
        // Only run at the configured hour of the day
        if (!IsAtLockoutHour())
            return;

        // Rebuild exit tile cache once per day
        RebuildExitTilesIfNeeded();

        // Deterministic seed for reachability queries this day
        int lockoutSeed = (int)_clock.DayNumber ^ 0x12345678;

        // Iterate all Alive NPCs in deterministic order
        var npcs = em.Query<NpcTag>()
            .Where(e => LifeStateGuard.IsAlive(e))
            .OrderBy(e => e.Id)
            .ToList();

        foreach (var npc in npcs)
        {
            if (!npc.Has<MetabolismComponent>()) continue;

            var metabolism = npc.Get<MetabolismComponent>();

            // Gate: only care about NPCs with high hunger
            // Hunger = 100 - Satiation; so check Satiation <= 100 - threshold
            if (metabolism.Satiation > (100 - _config.LockoutHungerThreshold))
            {
                // Hunger is below threshold; clear any lockout component
                if (npc.Has<LockedInComponent>())
                {
                    npc.Remove<LockedInComponent>();
                }
                continue;
            }

            // NPC is hungry; check if they can reach an exit
            if (!npc.Has<PositionComponent>()) continue;

            var pos = npc.Get<PositionComponent>();
            int npcTileX = (int)MathF.Round(pos.X);
            int npcTileY = (int)MathF.Round(pos.Z);

            bool canReachExit = CanReachAnyExit(npcTileX, npcTileY, lockoutSeed);

            if (canReachExit)
            {
                // Exit is reachable; no lockout
                if (npc.Has<LockedInComponent>())
                {
                    npc.Remove<LockedInComponent>();
                }
            }
            else
            {
                // No exit reachable and NPC is hungry
                if (!npc.Has<LockedInComponent>())
                {
                    // First detection of lockout
                    npc.Add(new LockedInComponent
                    {
                        FirstDetectedTick = (long)_clock.TotalTime,
                        StarvationTickBudget = _config.StarvationTicks
                    });
                }
                else
                {
                    // Already locked in; decrement budget
                    var locked = npc.Get<LockedInComponent>();
                    locked.StarvationTickBudget--;
                    npc.Add(locked); // Re-add (overwrites) the modified component

                    if (locked.StarvationTickBudget <= 0)
                    {
                        // Starvation death
                        _lifeStateTransitionSystem.RequestTransition(
                            npc.Id,
                            Components.LifeState.Deceased,
                            CauseOfDeath.StarvedAlone);
                        npc.Remove<LockedInComponent>();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks if the NPC at (x, y) can reach any outdoor exit tile.
    /// Returns true if at least one exit is reachable; false if all exits are blocked or no exits exist.
    /// </summary>
    private bool CanReachAnyExit(int npcX, int npcY, int seed)
    {
        if (_exitTiles.Count == 0)
            return true; // No exits defined = no lockout possible

        foreach (var (exitX, exitY) in _exitTiles)
        {
            // Check if there's a path from NPC to this exit
            var path = _pathfindingService.ComputePath(npcX, npcY, exitX, exitY, seed);
            if (path.Count > 0 || (npcX == exitX && npcY == exitY))
            {
                // Path exists or NPC is already at exit
                return true;
            }
        }

        return false; // No path to any exit
    }

    /// <summary>
    /// Rebuilds the exit-tile cache once per game-day by collecting all tiles
    /// belonging to outdoor named anchors.
    /// </summary>
    private void RebuildExitTilesIfNeeded()
    {
        int currentDay = (int)_clock.DayNumber;
        if (_lastExitCacheDay == currentDay)
            return; // Already built today

        _exitTiles.Clear();

        foreach (var entity in _entityManager.Query<NamedAnchorComponent>())
        {
            var anchor = entity.Get<NamedAnchorComponent>();
            if (anchor.Tag != _config.ExitNamedAnchorTag)
                continue; // Not an outdoor anchor

            if (!entity.Has<PositionComponent>()) continue;

            var pos = entity.Get<PositionComponent>();
            int x = (int)MathF.Round(pos.X);
            int y = (int)MathF.Round(pos.Z);
            _exitTiles.Add((x, y));
        }

        _lastExitCacheDay = currentDay;
    }

    /// <summary>
    /// Returns true if the current game tick is at or after the configured lockout-check hour.
    /// Used to gate the system to run once per game-day.
    /// </summary>
    private bool IsAtLockoutHour()
    {
        // Extract the hour from the simulation clock's total time
        // Assuming time is in seconds; game-day is 86400 seconds (24 hours)
        double secondsInDay = _clock.TotalTime % 86400.0;
        double currentHour = secondsInDay / 3600.0;
        return currentHour >= _config.LockoutCheckHour;
    }
}
