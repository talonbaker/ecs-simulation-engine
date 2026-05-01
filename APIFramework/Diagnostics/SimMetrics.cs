using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Diagnostics;

// ─────────────────────────────────────────────────────────────────────────────
//  Per-entity resource statistics accumulated over a simulation run
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Streaming min/max/mean accumulator for a single named resource (e.g. Satiation, Energy).
/// Each call to <see cref="Record(float)"/> updates the aggregate without retaining samples.
/// </summary>
public sealed class ResourceStats
{
    /// <summary>Display name of the resource (e.g. "Satiation", "Hydration").</summary>
    public string   Name  { get; }
    private float   _sum;
    private float   _min = float.MaxValue;
    private float   _max = float.MinValue;
    private long    _count;

    /// <summary>Minimum observed value, or 0 if no samples have been recorded.</summary>
    public float Min  => _count > 0 ? _min : 0f;

    /// <summary>Maximum observed value, or 0 if no samples have been recorded.</summary>
    public float Max  => _count > 0 ? _max : 0f;

    /// <summary>Arithmetic mean of all recorded samples, or 0 if no samples have been recorded.</summary>
    public float Mean => _count > 0 ? _sum / _count : 0f;

    /// <summary>Creates an empty stats accumulator with the given display name.</summary>
    /// <param name="name">Display name of the resource being tracked.</param>
    public ResourceStats(string name) => Name = name;

    /// <summary>Adds a single sample to the running min/max/mean aggregates.</summary>
    /// <param name="value">Resource value at the moment of sampling.</param>
    public void Record(float value)
    {
        _sum += value;
        if (value < _min) _min = value;
        if (value > _max) _max = value;
        _count++;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Named lifecycle event — when something important first happened
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A single named lifecycle event captured for an entity at a particular game time —
/// e.g. "First hunger", "Sleep onset", "STARVING".
/// </summary>
/// <param name="EntityName">Name or short ID of the entity the event was observed on.</param>
/// <param name="EventName">Short label for the event (used as the de-duplication key).</param>
/// <param name="GameTime">Game-seconds elapsed when the event was first detected.</param>
public sealed record LifecycleEvent(string EntityName, string EventName, double GameTime)
{
    /// <summary>
    /// Human-readable game-day and clock-time formatted from <see cref="GameTime"/>,
    /// e.g. "Day 2 · 7:30 AM".
    /// </summary>
    public string GameTimeDisplay
    {
        get
        {
            float tod = (float)(GameTime % SimulationClock.SecondsPerDay
                                + SimulationClock.DawnHour * 3600f) % SimulationClock.SecondsPerDay;
            float h   = tod / 3600f;
            int   hr  = (int)h;
            int   mn  = (int)(tod / 60f) % 60;
            string period = hr >= 12 ? "PM" : "AM";
            int    h12   = hr == 0 ? 12 : (hr > 12 ? hr - 12 : hr);
            int    day   = (int)(GameTime / SimulationClock.SecondsPerDay) + 1;
            return $"Day {day} · {h12}:{mn:D2} {period}";
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Per-entity metrics bundle
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Per-entity diagnostics bundle: streaming resource stats, activity counters, and a
/// chronologically-ordered list of lifecycle events (first-occurrence detection).
/// </summary>
public sealed class EntityMetrics
{
    /// <summary>Display name of the entity these metrics belong to.</summary>
    public string EntityName { get; }

    // Resources
    /// <summary>Min/max/mean of <c>MetabolismComponent.Satiation</c> (0–100) across samples.</summary>
    public ResourceStats Satiation    { get; } = new("Satiation");
    /// <summary>Min/max/mean of <c>MetabolismComponent.Hydration</c> (0–100) across samples.</summary>
    public ResourceStats Hydration    { get; } = new("Hydration");
    /// <summary>Min/max/mean of <c>EnergyComponent.Energy</c> (0–100) across samples.</summary>
    public ResourceStats Energy       { get; } = new("Energy");
    /// <summary>Min/max/mean of <c>EnergyComponent.Sleepiness</c> (0–100) across samples.</summary>
    public ResourceStats Sleepiness   { get; } = new("Sleepiness");
    /// <summary>Min/max/mean of <c>StomachComponent.Fill</c> expressed as a percentage (0–100).</summary>
    public ResourceStats StomachFill  { get; } = new("Stomach%");

    // Activity counters
    /// <summary>Number of detected feeding events (HungerTag → cleared transitions).</summary>
    public int FeedEvents  { get; private set; }
    /// <summary>Number of detected drinking events (ThirstTag → cleared transitions).</summary>
    public int DrinkEvents { get; private set; }
    /// <summary>Number of times the entity entered the sleeping state (SleepingTag onset transitions).</summary>
    public int SleepCycles { get; private set; }

    // Previous-tick tag state (to detect transitions)
    private bool _wasHungry, _wasThirsty, _wasSleeping;

    // Lifecycle events in order
    /// <summary>Lifecycle events recorded for this entity, in detection order.</summary>
    public List<LifecycleEvent> Events { get; } = new();

    /// <summary>Creates an empty metrics bundle for the named entity.</summary>
    /// <param name="entityName">Name or short ID of the entity these metrics belong to.</param>
    public EntityMetrics(string entityName) => EntityName = entityName;

    /// <summary>
    /// Sample the entity's current state.  Call once per tick (or per N ticks).
    /// </summary>
    /// <param name="entity">Entity to read components and tags from.</param>
    /// <param name="gameTime">Current simulation game-seconds — recorded against any first-time lifecycle events.</param>
    public void Sample(Entity entity, double gameTime)
    {
        if (entity.Has<MetabolismComponent>())
        {
            var m = entity.Get<MetabolismComponent>();
            Satiation.Record(m.Satiation);
            Hydration.Record(m.Hydration);
        }

        if (entity.Has<EnergyComponent>())
        {
            var e = entity.Get<EnergyComponent>();
            Energy.Record(e.Energy);
            Sleepiness.Record(e.Sleepiness);
        }

        if (entity.Has<StomachComponent>())
        {
            var s = entity.Get<StomachComponent>();
            StomachFill.Record(s.Fill * 100f);
        }

        // ── Lifecycle event detection ─────────────────────────────────────────

        bool hungry   = entity.Has<HungerTag>();
        bool thirsty  = entity.Has<ThirstTag>();
        bool sleeping = entity.Has<SleepingTag>();

        if (hungry   && !_wasHungry)   RecordFirstOnce("First hunger",  gameTime);
        if (thirsty  && !_wasThirsty)  RecordFirstOnce("First thirst",  gameTime);
        if (sleeping && !_wasSleeping) { RecordFirstOnce("Sleep onset",  gameTime); SleepCycles++; }

        if (entity.Has<StarvingTag>())   RecordFirstOnce("STARVING",    gameTime);
        if (entity.Has<DehydratedTag>()) RecordFirstOnce("DEHYDRATED",  gameTime);
        if (entity.Has<ExhaustedTag>())  RecordFirstOnce("EXHAUSTED",   gameTime);

        // Count feed/drink events (each time FeedingSystem spawns food, a new
        // entity appears with BolusTag; we detect the transition via stomach fill rising)
        // Simpler proxy: count how many times HungerTag clears (fed)
        if (_wasHungry && !hungry) FeedEvents++;
        if (_wasThirsty && !thirsty) DrinkEvents++;

        _wasHungry   = hungry;
        _wasThirsty  = thirsty;
        _wasSleeping = sleeping;
    }

    private readonly HashSet<string> _recorded = new();

    private void RecordFirstOnce(string name, double gameTime)
    {
        if (_recorded.Add(name))
            Events.Add(new LifecycleEvent(EntityName, name, gameTime));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Top-level collector — wired into the CLI main loop
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Top-level diagnostics collector wired into the CLI main loop. Maintains one
/// <see cref="EntityMetrics"/> per metabolising entity and samples them on a fixed cadence.
/// </summary>
/// <seealso cref="EntityMetrics"/>
/// <seealso cref="ResourceStats"/>
public sealed class SimMetrics
{
    private readonly SimulationBootstrapper         _sim;
    private readonly Dictionary<Guid, EntityMetrics> _byId = new();
    private          long                            _sampleTick;
    private const    int                             SampleEvery = 20; // ticks between samples

    /// <summary>Creates a collector bound to the given simulation bootstrapper.</summary>
    /// <param name="sim">Simulation whose entity manager and clock are sampled each tick.</param>
    public SimMetrics(SimulationBootstrapper sim) => _sim = sim;

    /// <summary>Call every tick. Samples every N ticks for efficiency.</summary>
    /// <param name="tickNumber">Monotonic tick counter from the simulation main loop.</param>
    public void Tick(long tickNumber)
    {
        if (tickNumber % SampleEvery != 0) return;

        double gameTime = _sim.Clock.TotalTime;
        _sampleTick++;

        foreach (var entity in _sim.EntityManager.Query<MetabolismComponent>())
        {
            if (!_byId.TryGetValue(entity.Id, out var em))
            {
                string name = entity.Has<IdentityComponent>()
                    ? entity.Get<IdentityComponent>().Name
                    : entity.ShortId;
                em = new EntityMetrics(name);
                _byId[entity.Id] = em;
            }
            em.Sample(entity, gameTime);
        }
    }

    /// <summary>All per-entity metric bundles collected during the run, keyed internally by entity id.</summary>
    public IReadOnlyCollection<EntityMetrics> EntityStats => _byId.Values;
}
