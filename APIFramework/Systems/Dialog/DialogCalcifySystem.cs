using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems.Dialog;

/// <summary>
/// Phase 75 (after DialogFragmentRetrievalSystem) — evaluates each NPC's
/// DialogHistoryComponent and transitions fragments between calcified / un-calcified.
///
/// Calcification fires when:
///   UseCount >= CalcifyThreshold
///   AND dominant-context fraction >= CalcifyContextDominanceMin
///
/// Decalcification fires when:
///   LastUseGameTimeSec is more than DecalcifyTimeoutDays game-days ago
/// </summary>
/// <remarks>
/// Phase: Dialog (75), registered after <see cref="DialogContextDecisionSystem"/> and
/// <see cref="DialogFragmentRetrievalSystem"/>. Reads and writes
/// <c>DialogHistoryComponent.UsesByFragmentId[*].Calcified</c> only.
/// Skips non-Alive NPCs.
/// </remarks>
public sealed class DialogCalcifySystem : ISystem
{
    private readonly DialogConfig _cfg;

    private double _gameTimeSec;

    /// <summary>
    /// Stores the dialog config used for thresholds and timeouts.
    /// </summary>
    /// <param name="cfg">Dialog config — supplies CalcifyThreshold, CalcifyContextDominanceMin, DecalcifyTimeoutDays.</param>
    public DialogCalcifySystem(DialogConfig cfg)
    {
        _cfg = cfg;
    }

    /// <summary>
    /// Per-tick entry point. Updates calcified flags on every NPC's dialog history.
    /// </summary>
    /// <param name="em">Entity manager — queried for NPCs with dialog history.</param>
    /// <param name="deltaTime">Tick delta in seconds; accumulated into the system's clock.</param>
    public void Update(EntityManager em, float deltaTime)
    {
        _gameTimeSec += deltaTime;

        double decalcifyWindowSec = _cfg.DecalcifyTimeoutDays * 86_400.0;

        foreach (var entity in em.Query<DialogHistoryComponent>())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip non-Alive NPCs
            var hist = entity.Get<DialogHistoryComponent>();

            foreach (var rec in hist.UsesByFragmentId.Values)
            {
                if (rec.Calcified)
                {
                    // Decalcify on prolonged disuse
                    if (_gameTimeSec - rec.LastUseGameTimeSec >= decalcifyWindowSec)
                        rec.Calcified = false;
                }
                else
                {
                    // Calcification eligibility check
                    if (rec.UseCount < _cfg.CalcifyThreshold) continue;
                    if (rec.ContextCounts.Count == 0)         continue;

                    int dominantCount = rec.ContextCounts.Values.Max();
                    double dominanceFraction = (double)dominantCount / rec.UseCount;

                    if (dominanceFraction >= _cfg.CalcifyContextDominanceMin)
                        rec.Calcified = true;
                }
            }
        }
    }
}
