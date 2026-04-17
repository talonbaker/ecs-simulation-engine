using APIFramework.Components;
using APIFramework.Core;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ECSVisualizer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    // ── Simulation ───────────────────────────────────────────────────────────
    private readonly SimulationBootstrapper _sim;
    private readonly DispatcherTimer        _timer;
    private int _frameCount = 0;

    // ── Version ──────────────────────────────────────────────────────────────
    public string VersionDisplay => SimVersion.Full;

    // ── Clock ────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _currentTimeDisplay = "6:00 AM";
    [ObservableProperty] private string _dayDisplay         = "Day 1";
    [ObservableProperty] private string _dayNightIcon       = "☀";

    [ObservableProperty] private float _timeScale = 1.0f;
    partial void OnTimeScaleChanged(float value) => _sim.Clock.TimeScale = value;

    // ── Entity Collections ───────────────────────────────────────────────────
    public ObservableCollection<EntityViewModel> LivingEntities  { get; } = new();
    public ObservableCollection<EntityViewModel> PipelineEntities { get; } = new();

    private readonly Dictionary<Guid, EntityViewModel> _viewModelCache = new();

    // ── Design-time constructor ───────────────────────────────────────────────
    public MainViewModel() : this(new SimulationBootstrapper()) { }

    // ── Runtime constructor ───────────────────────────────────────────────────
    private SimConfigWatcher? _configWatcher;

    public MainViewModel(SimulationBootstrapper sim)
    {
        _sim = sim;
        // Sync the UI slider to whatever TimeScale the config loaded
        _timeScale = sim.Clock.TimeScale;

        // Hot-reload: watch SimConfig.json, marshal apply onto the UI thread so
        // the config change lands between ticks (DispatcherTimer is single-threaded)
        var configPath = FindConfigPath("SimConfig.json");
        if (configPath != null)
        {
            _configWatcher = new SimConfigWatcher(configPath, newCfg =>
            {
                Dispatcher.UIThread.InvokeAsync(() => _sim.ApplyConfig(newCfg));
            });
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60 fps
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

        // ── Clock display ─────────────────────────────────────────────────────
        CurrentTimeDisplay = _sim.Clock.GameTimeDisplay;
        DayDisplay         = $"Day {_sim.Clock.DayNumber}";
        DayNightIcon       = _sim.Clock.IsDaytime ? "☀" : "🌙";

        // ── Refresh entity displays every 3 frames ────────────────────────────
        _frameCount++;
        if (_frameCount % 3 != 0) return;
        _frameCount = 0;

        RefreshLivingEntities();
        RefreshPipelineEntities();
    }

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
