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
        // This is the "brain" that creates the tags!

        _engine = engine;
        _clock = clock;

        _engine.AddSystem(new BiologicalConditionSystem());

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
            var detail = new StringBuilder();
            var allComponents = entity.GetAllComponents().ToList();

            // 1. IDENTITY
            var identity = allComponents.OfType<IdentityComponent>().FirstOrDefault();
            detail.Append($"[{entity.ShortId}] {(identity.Name ?? "Entity")}");

            // 2. TAGS (Manual Check)
            // This explicitly checks for the tags we defined in Tags.cs
            if (entity.Has<HungerTag>()) detail.Append(" [HUNGRY]");
            if (entity.Has<ThirstTag>()) detail.Append(" [THIRSTY]");
            if (entity.Has<StarvingTag>()) detail.Append(" [STARVING]");
            if (entity.Has<DehydratedTag>()) detail.Append(" [DEHYDRATED]");
            if (entity.Has<IrritableTag>()) detail.Append(" [IRRITABLE]");

            detail.Append(" | ");

            // 3. DATA COMPONENTS
            foreach (var component in allComponents)
            {
                var type = component.GetType();

                // Skip the Identity and the Marker Tags so they don't double-up
                if (type == typeof(IdentityComponent) || type.Name.EndsWith("Tag"))
                    continue;

                string name = type.Name.Replace("Component", "");
                detail.Append($"{name}: {component} | ");
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