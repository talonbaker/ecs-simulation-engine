using APIFramework.Components;
using APIFramework.Core;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
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
    [ObservableProperty] private string _currentTimeDisplay = "00:00:00";

    [ObservableProperty] private float _timeScale = 1.0f;
    partial void OnTimeScaleChanged(float value) => _sim.Clock.TimeScale = value;

    // ── Entity Collections ───────────────────────────────────────────────────
    // Living entities: anything with a metabolism (humans, cats)
    public ObservableCollection<EntityViewModel> LivingEntities { get; } = new();

    // Pipeline entities: bolus/liquid currently in the esophagus
    public ObservableCollection<EntityViewModel> PipelineEntities { get; } = new();

    // Cache so we reuse ViewModels instead of recreating them every frame
    private readonly Dictionary<Guid, EntityViewModel> _viewModelCache = new();

    // ── Design-time constructor (no args — Avalonia designer requires this) ──
    public MainViewModel() : this(new SimulationBootstrapper()) { }

    // ── Runtime constructor (injected by DI) ─────────────────────────────────
    public MainViewModel(SimulationBootstrapper sim)
    {
        _sim = sim;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60 fps
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        // 1. Advance the simulation — this is the only place Update is called
        const float fixedDelta = 1f / 60f;
        _sim.Engine.Update(fixedDelta);

        // 2. Clock display
        CurrentTimeDisplay = TimeSpan.FromSeconds(_sim.Clock.TotalTime).ToString(@"hh\:mm\:ss");

        // 3. Refresh entity displays every 3 frames (smooth enough, not wasteful)
        _frameCount++;
        if (_frameCount % 3 != 0) return;
        _frameCount = 0;

        RefreshLivingEntities();
        RefreshPipelineEntities();
    }

    private void RefreshLivingEntities()
    {
        var living = _sim.EntityManager.Query<MetabolismComponent>().ToList();

        // Remove entries that no longer exist
        var livingIds = living.Select(e => e.Id).ToHashSet();
        var toRemove = LivingEntities.Where(vm => !livingIds.Contains(Guid.Parse(vm.EntityId.PadRight(32, '0')))).ToList();
        // Simpler: just rebuild if counts differ, otherwise update in place
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

        // Update in place
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

        // Evict stale cache entries for entities that no longer exist
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
