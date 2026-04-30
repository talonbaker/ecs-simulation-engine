using System;
using APIFramework.Core;
using Warden.Contracts.Telemetry;

/// <summary>
/// Thin compile-time shim that routes world-state projection to the correct
/// implementation based on the active scripting define.
///
/// WARDEN BUILD (editor + development)
/// ─────────────────────────────────────
/// Uses <c>Warden.Telemetry.TelemetryProjector</c> — the authoritative projector
/// that ships with the Warden toolkit. Requires <c>Warden.Telemetry.dll</c> to be
/// present in <c>Assets/Plugins/</c>.
///
/// RETAIL BUILD (player distribution)
/// ─────────────────────────────────────
/// Uses <see cref="InlineProjector"/> — a self-contained re-implementation that
/// mirrors the WorldStateDto schema without depending on Warden.Telemetry.dll.
/// This keeps player builds lean and eliminates the dependency on Warden internals
/// for shipped code.
///
/// PARITY
/// ──────
/// <c>InlineProjectorParityTests</c> verifies byte-identical JSON output between
/// the two paths on a representative engine state. If the schemas diverge, that
/// test fails and alerts the author before a release build.
/// </summary>
public sealed class WorldStateProjectorAdapter
{
    /// <summary>
    /// Projects the current engine state into a <see cref="WorldStateDto"/>.
    /// </summary>
    /// <param name="bootstrapper">
    /// The live <see cref="SimulationBootstrapper"/> — carries the EntityManager,
    /// Clock, SunState, Chronicle, and all other services needed for projection.
    /// </param>
    /// <param name="tick">
    /// Current engine tick count (caller-maintained, not the clock's real-seconds).
    /// Stamped onto the DTO for replay and telemetry ordering.
    /// </param>
    public WorldStateDto Project(SimulationBootstrapper bootstrapper, long tick)
    {
#if WARDEN
        // WARDEN path: use Warden.Telemetry.TelemetryProjector directly.
        // Capture() produces an immutable SimulationSnapshot; TelemetryProjector
        // converts it to WorldStateDto without modifying either.
        var snap = SimulationSnapshot.Capture(bootstrapper);
        return Warden.Telemetry.TelemetryProjector.Project(
            snap:             snap,
            entityManager:    bootstrapper.EntityManager,
            capturedAt:       DateTimeOffset.UtcNow,
            tick:             tick,
            seed:             0,
            simVersion:       "3.1.A",
            sunStateService:  bootstrapper.SunState,
            chronicleService: bootstrapper.Chronicle);
#else
        // RETAIL path: inline projector, no Warden dependency.
        var snap = SimulationSnapshot.Capture(bootstrapper);
        return InlineProjector.Project(
            snap:          snap,
            entityManager: bootstrapper.EntityManager,
            capturedAt:    DateTimeOffset.UtcNow,
            tick:          tick);
#endif
    }
}
