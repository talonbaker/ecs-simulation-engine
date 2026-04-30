using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Physiology phase. Fills <see cref="BladderComponent"/> at a constant ml/game-second
/// rate each tick. Volume is clamped at <c>CapacityMl</c> — overflow is expressed via
/// tags written by <see cref="BladderSystem"/>, not by exceeding capacity.
/// </summary>
/// <remarks>
/// Reads: <see cref="BladderComponent"/>, <see cref="LifeStateComponent"/>.<br/>
/// Writes: <see cref="BladderComponent"/>.VolumeML (single writer for fill).<br/>
/// Phase: Physiology, alongside <see cref="MetabolismSystem"/> and <see cref="EnergySystem"/>.
/// The bladder does NOT slow during sleep — kidneys filter continuously.
/// </remarks>
public class BladderFillSystem : ISystem
{
    /// <summary>Per-tick fill pass; advances <see cref="BladderComponent"/>.VolumeML toward CapacityMl.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<BladderComponent>().ToList())
        {
            if (!LifeStateGuard.IsBiologicallyTicking(entity)) continue;  // WP-3.0.0: skip Deceased NPCs (Incapacitated still ticks)

            var bladder = entity.Get<BladderComponent>();

            bladder.VolumeML = MathF.Min(
                bladder.CapacityMl,
                bladder.VolumeML + bladder.FillRate * deltaTime);

            entity.Add(bladder);
        }
    }
}
