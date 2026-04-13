using APIFramework.Components;
using APIFramework.Core;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ECSVisualizer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<string> ActiveEntityList { get; } = new();
    private int _frameCount = 0;

    private readonly SimulationEngine _engine;
    private readonly SimulationClock _clock;
    private readonly DispatcherTimer _timer;

    [ObservableProperty]
    private string _currentTimeDisplay = "00:00:00";

    [ObservableProperty]
    private float _timeScale = 1.0f;

    [ObservableProperty]
    private string _hungerDisplay = "Hunger: 0%";

    public MainViewModel(SimulationEngine engine, SimulationClock clock)
    {
        _engine = engine;
        _clock = clock;

        // Setup a 60 FPS timer
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        // 1. Define the Fixed Delta (1/60th of a second)
        // We no longer measure the stopwatch. We assume exactly 0.0166 seconds.
        const float fixedDeltaTime = 0.0166667f;

        // 2. Run the Engine
        // The engine still applies the TimeScale internally: (0.0166 * Scale)
        _engine.Update(fixedDeltaTime);

        // 3. Update the UI Clock
        CurrentTimeDisplay = TimeSpan.FromSeconds(_engine.Clock.TotalTime).ToString(@"hh\:mm\:ss");

        // 4. Deferred UI List Refresh
        _frameCount++;
        if (_frameCount % 5 == 0)
        {
            UpdateEntityListUI();
            _frameCount = 0;
        }

        // 5. Update KPI Display
        var human = _engine.EntityManager.Query<MetabolismComponent>().FirstOrDefault();
        if (human != null)
        {
            HungerDisplay = $"Hunger: {human.Get<MetabolismComponent>().Hunger:F1}%";
        }
    }

    private void UpdateEntityListUI()
    {
        ActiveEntityList.Clear();

        foreach (var entity in _engine.EntityManager.GetAllEntities())
        {
            // Start with the ID
            var detail = new StringBuilder($"[{entity.ShortId}]");

            // Check for a Tag first to give the entity a name
            if (entity.Has<IdentityComponent>())
            {
                var tag = entity.Get<IdentityComponent>();
                detail.Append($" {tag.Name} |");
            }

            // Loop through everything else
            foreach (var component in entity.GetAllComponents())
            {
                // Skip the Tag since we already handled it
                if (component is IdentityComponent) continue;

                string name = component.GetType().Name.Replace("Component", "");
                detail.Append($" {name}: {component} |");

                if (entity.Has<ThirstyTag>()) detail.Append(" [THIRSTY]");
                if (entity.Has<HungryTag>()) detail.Append(" [HUNGRY]");
                if (entity.Has<StarvingTag>()) detail.Append(" !!STARVING!!");
            }

            ActiveEntityList.Add(detail.ToString().TrimEnd('|', ' '));
        }
    }

    // This handles the slider/input in the UI
    partial void OnTimeScaleChanged(float value)
    {
        _clock.TimeScale = value;
    }
}