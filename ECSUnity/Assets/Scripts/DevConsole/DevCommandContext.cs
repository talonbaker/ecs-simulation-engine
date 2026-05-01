#if WARDEN

/// <summary>
/// Dependency bag passed to every <see cref="IDevConsoleCommand.Execute"/> call — WP-3.1.H.
///
/// Commands access engine services exclusively through this context.
/// All fields are nullable; commands must guard against null references
/// and return an informative error when a required service is absent.
/// </summary>
public sealed class DevCommandContext
{
    // ── Engine ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Live EngineHost. Provides <c>Engine</c> (EntityManager), <c>WorldState</c>,
    /// and <c>TickCount</c>.
    /// </summary>
    public EngineHost Host;

    // ── Mutation ───────────────────────────────────────────────────────────────

    /// <summary>
    /// World mutation API for move, spawn, despawn, lock, unlock operations.
    /// </summary>
    public APIFramework.Mutation.IWorldMutationApi MutationApi;

    // ── UI panels ──────────────────────────────────────────────────────────────

    /// <summary>Save/Load panel for save and load commands.</summary>
    public SaveLoadPanel SaveLoad;

    /// <summary>Time HUD panel for pause and resume commands.</summary>
    public TimeHudPanel TimeHud;

    /// <summary>
    /// The console panel itself — used by the quit command to close the panel.
    /// </summary>
    public DevConsolePanel Console;

    /// <summary>
    /// JSONL stream emitter — used by the tick-rate command to adjust cadence.
    /// Null in RETAIL (the entire emitter is stripped) but we guard against null here.
    /// </summary>
    public JsonlStreamEmitter Emitter;
}

#endif
