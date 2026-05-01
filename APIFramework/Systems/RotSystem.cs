using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Ages all food entities that carry a RotComponent, accumulating rot over game time.
///
/// HOW IT WORKS
/// ────────────
/// Each tick, AgeSeconds advances by deltaTime.  Once AgeSeconds ≥ RotStartAge,
/// RotLevel begins climbing at RotRate per game-second.  When RotLevel crosses
/// rotTagThreshold, RotTag is applied to the food entity.
///
/// FeedingSystem checks RotTag before Billy eats.  If the food is rotten:
///   - It applies ConsumedRottenFoodTag to Billy
///   - MoodSystem converts this into a Disgust spike next tick
///
/// PIPELINE POSITION
/// ──────────────────
/// Position 13 — runs after all digestion is complete.  Food entities age during
/// every tick regardless of whether they are being eaten, in transit, or just
/// sitting in the world waiting to be consumed.
///
/// PLANNED
/// ────────
/// Once a spatial/proximity system exists, RotTag on nearby food entities will
/// also be read by MoodSystem to build Disgust even before consumption (smelling
/// something rotten raises boredom-adjacent aversion without direct contact).
/// </summary>
/// <remarks>
/// Reads: <see cref="RotComponent"/>.<br/>
/// Writes: <see cref="RotComponent"/> Age/RotLevel and <see cref="RotTag"/>
/// (single writer of <see cref="RotTag"/>).<br/>
/// Phase: World, after the Elimination pipeline.
/// </remarks>
public class RotSystem : ISystem
{
    private readonly RotSystemConfig _cfg;

    /// <summary>Constructs the rot system with its tuning.</summary>
    /// <param name="cfg">Rot tuning (RotTagThreshold).</param>
    public RotSystem(RotSystemConfig cfg) => _cfg = cfg;

    /// <summary>Per-tick aging pass over <see cref="RotComponent"/> entities.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<RotComponent>())
        {
            var rot = entity.Get<RotComponent>();

            // Advance age
            rot.AgeSeconds += deltaTime;

            // Once past the freshness window, accumulate rot
            if (rot.IsDecaying)
            {
                rot.RotLevel = MathF.Min(100f, rot.RotLevel + rot.RotRate * deltaTime);
            }

            entity.Add(rot);

            // Apply or remove RotTag based on current rot level
            bool isRotten = rot.RotLevel >= _cfg.RotTagThreshold;
            if (isRotten && !entity.Has<RotTag>())
                entity.Add(new RotTag());
            else if (!isRotten && entity.Has<RotTag>())
                entity.Remove<RotTag>();
        }
    }
}
