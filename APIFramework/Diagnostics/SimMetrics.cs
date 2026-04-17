using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Diagnostics;

// ─────────────────────────────────────────────────────────────────────────────
//  Per-entity resource statistics accumulated over a simulation run
// ─────────────────────────────────────────────────────────────────────────────

public sealed class ResourceStats
{
    public string   Name  { get; }
    private float   _sum;
    private float   _min = float.MaxValue;
    private float   _max = float.MinValue;
    private long    _count;

    public float Min  => _count > 0 ? _min : 0f;
    public float Max  => _count > 0 ? _max : 0f;
    public float Mean => _count > 0 ? _sum / _count : 0f;

    public ResourceStats(string name) => Name = name;

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

public sealed record LifecycleEvent(string EntityName, string EventName, double GameTime)
{
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

public sealed class EntityMetrics
{
    public string EntityName { get; }

    // Resources
    public ResourceStats Satiation    { get; } = new("Satiation");
    public ResourceStats Hydration    { get; } = new("Hydration");
    public ResourceStats Energy       { get; } = new("Energy");
    public ResourceStats Sleepiness   { get; } = new("Sleepiness");
    public ResourceStats StomachFill  { get; } = new("Stomach%");

    // Activity counters
    public int FeedEvents  { get; private set; }
    public int DrinkEvents { get; private set; }
    public int SleepCycles { get; private set; }

    // Previous-tick tag state (to detect transitions)
    private bool _wasHungry, _wasThirsty, _wasSleeping;

    // Lifecycle events in order
    public List<LifecycleEvent> Events { get; } = new();

    public EntityMetrics(string entityName) => EntityName = entityName;

    /// <summary>
    /// Sample the entity's current state.  Call once per tick (or per N ticks).
    /// </summary>
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

public sealed class SimMetrics
{
    private readonly SimulationBootstrapper         _sim;
    private readonly Dictionary<Guid, EntityMetrics> _byId = new();
    private          long                            _sampleTick;
    private const    int                             SampleEvery = 20; // ticks between samples

    public SimMetrics(SimulationBootstrapper sim) => _sim = sim;

    /// <summary>Call every tick. Samples every N ticks for efficiency.</summary>
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

    public IReadOnlyCollection<EntityMetrics> EntityStats => _byId.Values;
}
