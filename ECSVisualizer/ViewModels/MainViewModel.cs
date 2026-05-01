using APIFramework.Components;
using APIFramework.Core;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ECSVisualizer.ViewModels;

/// <summary>
/// Top-level view model that binds the running simulation to the main window.
///
/// Owns a <see cref="DispatcherTimer"/> ticking at ~60 Hz which advances the
/// engine, samples charts on a game-time cadence, throttles the UI refresh
/// rate based on <c>TimeScale</c>, and synchronises the
/// <see cref="LivingEntities"/>, <see cref="PipelineEntities"/>, and
/// <see cref="WorldEntities"/> collections with the ECS world.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    // ── Simulation ───────────────────────────────────────────────────────────
    private readonly SimulationBootstrapper _sim;
    private readonly DispatcherTimer        _timer;

    // ── Version ──────────────────────────────────────────────────────────────
    /// <summary>Full simulation version string shown in the title bar.</summary>
    public string VersionDisplay => SimVersion.Full;

    // ── Clock ────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _currentTimeDisplay = "6:00 AM";
    [ObservableProperty] private string _dayDisplay         = "Day 1";
    [ObservableProperty] private string _dayNightIcon       = "☀";
    [ObservableProperty] private string _circadianLabel     = "circadian ×0.10";

    // ── Sim stats ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _entityCountLabel    = "0 living";
    [ObservableProperty] private int    _invariantViolations = 0;
    [ObservableProperty] private bool   _hasViolations       = false;

    // ── Performance display ───────────────────────────────────────────────────
    [ObservableProperty] private string _fpsLabel           = "60 fps";
    [ObservableProperty] private string _refreshRateLabel   = "UI: every 3 frames";

    [ObservableProperty] private float _timeScale = 1.0f;
    partial void OnTimeScaleChanged(float value) => _sim.Clock.TimeScale = value;

    // ── Charts ────────────────────────────────────────────────────────────────
    /// <summary>Collection of scrolling-chart series (resources, mood, FPS, entity load).</summary>
    public ChartViewModel Charts { get; } = new();

    // ── Entity Collections ───────────────────────────────────────────────────
    /// <summary>Living entities (those carrying a <c>MetabolismComponent</c>) bound to the entity panel.</summary>
    public ObservableCollection<EntityViewModel>      LivingEntities   { get; } = new();
    /// <summary>Entities currently in the esophagus transit pipeline.</summary>
    public ObservableCollection<EntityViewModel>      PipelineEntities { get; } = new();
    /// <summary>Food and liquid entities sitting in the world (not currently in transit).</summary>
    public ObservableCollection<WorldEntityViewModel> WorldEntities    { get; } = new();

    private readonly Dictionary<Guid, EntityViewModel>      _viewModelCache      = new();
    private readonly Dictionary<Guid, WorldEntityViewModel> _worldViewModelCache = new();

    // ── Adaptive refresh state ────────────────────────────────────────────────
    private int  _frameCount     = 0;
    private int  _uiRefreshEvery = 3;  // recomputed each tick

    // ── Chart sampling state ──────────────────────────────────────────────────
    // Charts sample every GameChartIntervalSec game-seconds (= 1 game-minute).
    // This is independent of TimeScale — same game-time resolution at any speed.
    private const double GameChartIntervalSec = 60.0;
    private double _lastChartGameTime = 0.0;

    // ── FPS measurement ───────────────────────────────────────────────────────
    private readonly Stopwatch _fpsWatch   = Stopwatch.StartNew();
    private int                _fpsTicks   = 0;
    private const int          FpsSampleEvery = 60; // real frames between FPS reads

    // ── Design-time constructor ───────────────────────────────────────────────
    /// <summary>
    /// Design-time constructor — boots a fresh <see cref="SimulationBootstrapper"/>
    /// so the Avalonia previewer has a populated view model to render.
    /// </summary>
    public MainViewModel() : this(new SimulationBootstrapper()) { }

    // ── Runtime constructor ───────────────────────────────────────────────────
    private SimConfigWatcher? _configWatcher;

    /// <summary>
    /// Runtime constructor — wires the supplied simulation bootstrapper, sets
    /// up <c>SimConfig.json</c> hot-reload watching, and starts the dispatcher
    /// timer that ticks the engine and refreshes the UI.
    /// </summary>
    /// <param name="sim">The headless simulation bootstrapper resolved from DI.</param>
    public MainViewModel(SimulationBootstrapper sim)
    {
        _sim = sim;
        _timeScale = sim.Clock.TimeScale;

        var configPath = FindConfigPath("SimConfig.json");
        if (configPath != null)
        {
            _configWatcher = new SimConfigWatcher(configPath, newCfg =>
            {
                Dispatcher.UIThread.InvokeAsync(() => _sim.ApplyConfig(newCfg));
            });
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private static string? FindConfigPath(string fileName)
    {
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        const float fixedDelta = 1f / 60f;
        _sim.Engine.Update(fixedDelta);

        // ── Adaptive UI refresh interval ──────────────────────────────────────
        // At low TimeScale the UI refreshes frequently (every 3 frames = ~20fps)
        // At high TimeScale we back off so the UI thread doesn't saturate.
        // Formula: refresh every N real frames, where N grows with TimeScale.
        //   1×   → every  3 frames (~20 fps display)
        //   60×  → every  4 frames (~15 fps display)
        //   120× → every  8 frames (~7.5 fps display)
        //   480× → every 32 frames (~2 fps display)
        // The simulation itself always runs at full speed; only the UI slows.
        _uiRefreshEvery = Math.Max(3, (int)(_sim.Clock.TimeScale / 15f));

        // ── Clock display (every tick — lightweight string ops) ───────────────
        CurrentTimeDisplay = _sim.Clock.GameTimeDisplay;
        DayDisplay         = $"Day {_sim.Clock.DayNumber}";
        DayNightIcon       = _sim.Clock.IsDaytime ? "☀" : "🌙";
        CircadianLabel     = $"circadian ×{_sim.Clock.CircadianFactor:F2}";

        // ── Sim stats (every tick) ────────────────────────────────────────────
        InvariantViolations = _sim.Invariants.Violations.Count;
        HasViolations       = InvariantViolations > 0;

        // ── FPS measurement ───────────────────────────────────────────────────
        _fpsTicks++;
        if (_fpsTicks >= FpsSampleEvery)
        {
            double elapsedSec = _fpsWatch.Elapsed.TotalSeconds;
            double realFps    = _fpsTicks / elapsedSec;
            FpsLabel          = $"{realFps:F0} fps";
            RefreshRateLabel  = $"UI: every {_uiRefreshEvery} frames";
            Charts.Fps.Push(realFps);
            _fpsWatch.Restart();
            _fpsTicks = 0;
        }

        // ── Chart sampling (game-time driven — same resolution at any speed) ──
        double gameNow = _sim.Clock.TotalTime;
        if (gameNow - _lastChartGameTime >= GameChartIntervalSec)
        {
            _lastChartGameTime = gameNow;
            SampleCharts();
        }

        // ── Throttled UI entity refresh ───────────────────────────────────────
        _frameCount++;
        if (_frameCount % _uiRefreshEvery != 0) return;
        _frameCount = 0;

        int livingCount = _sim.EntityManager.Query<MetabolismComponent>().Count();
        EntityCountLabel = $"{livingCount} living";
        Charts.EntityLoad.Push(livingCount);

        RefreshLivingEntities();
        RefreshPipelineEntities();
        RefreshWorldEntities();
    }

    // ── Chart sampling ────────────────────────────────────────────────────────

    private void SampleCharts()
    {
        // Sample the first living entity — typically "Billy" or whichever was
        // spawned first. Future versions can add per-entity chart selection.
        var firstLiving = _sim.EntityManager.Query<MetabolismComponent>().FirstOrDefault();
        if (firstLiving == null) return;

        var meta = firstLiving.Get<MetabolismComponent>();

        Charts.Satiation.Push(meta.Satiation);
        Charts.Hydration.Push(meta.Hydration);
        Charts.BodyTemp.Push(meta.BodyTemp);

        if (firstLiving.Has<EnergyComponent>())
        {
            var en = firstLiving.Get<EnergyComponent>();
            Charts.Energy.Push(en.Energy);
            Charts.Sleepiness.Push(en.Sleepiness);
        }

        if (firstLiving.Has<MoodComponent>())
        {
            var mood = firstLiving.Get<MoodComponent>();
            Charts.Joy.Push(mood.Joy);
            Charts.Anger.Push(mood.Anger);
            Charts.Sadness.Push(mood.Sadness);
        }
    }

    // ── Entity list refreshes ─────────────────────────────────────────────────

    private void RefreshLivingEntities()
    {
        var living = _sim.EntityManager.Query<MetabolismComponent>().ToList();

        if (LivingEntities.Count != living.Count)
        {
            LivingEntities.Clear();
            foreach (var entity in living)
            {
                var vm = GetOrCreateViewModel(entity.Id);
                vm.Update(entity);
                LivingEntities.Add(vm);
            }
            return;
        }

        for (int i = 0; i < living.Count; i++)
        {
            var vm = GetOrCreateViewModel(living[i].Id);
            vm.Update(living[i]);
            if (i < LivingEntities.Count && LivingEntities[i] != vm)
                LivingEntities[i] = vm;
            else if (i >= LivingEntities.Count)
                LivingEntities.Add(vm);
        }
    }

    private void RefreshPipelineEntities()
    {
        var inTransit = _sim.EntityManager.Query<EsophagusTransitComponent>().ToList();

        PipelineEntities.Clear();
        foreach (var entity in inTransit)
        {
            var vm = GetOrCreateViewModel(entity.Id);
            vm.Update(entity);
            PipelineEntities.Add(vm);
        }

        var allIds = _sim.EntityManager.GetAllEntities().Select(e => e.Id).ToHashSet();
        var stale  = _viewModelCache.Keys.Where(id => !allIds.Contains(id)).ToList();
        foreach (var id in stale) _viewModelCache.Remove(id);
    }

    private void RefreshWorldEntities()
    {
        var transitIds = _sim.EntityManager.Query<EsophagusTransitComponent>()
            .Select(e => e.Id).ToHashSet();

        var worldFood = _sim.EntityManager.Query<BolusComponent>()
            .Concat(_sim.EntityManager.Query<LiquidComponent>())
            .Where(e => !transitIds.Contains(e.Id))
            .Distinct().ToList();

        WorldEntities.Clear();
        foreach (var entity in worldFood)
        {
            if (!_worldViewModelCache.TryGetValue(entity.Id, out var vm))
            {
                vm = new WorldEntityViewModel();
                _worldViewModelCache[entity.Id] = vm;
            }
            vm.Update(entity);
            WorldEntities.Add(vm);
        }

        var allIds = _sim.EntityManager.GetAllEntities().Select(e => e.Id).ToHashSet();
        var staleWorld = _worldViewModelCache.Keys.Where(id => !allIds.Contains(id)).ToList();
        foreach (var id in staleWorld) _worldViewModelCache.Remove(id);
    }

    private EntityViewModel GetOrCreateViewModel(Guid id)
    {
        if (!_viewModelCache.TryGetValue(id, out var vm))
        {
            vm = new EntityViewModel();
            _viewModelCache[id] = vm;
        }
        return vm;
    }
}
