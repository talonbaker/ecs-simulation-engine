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

    private readonly SimulationEngine _engine;
    private readonly SimulationClock _clock;
    private readonly DispatcherTimer _timer;

    // Add a field to your class
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private double _lastElapsedSeconds = 0;

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
        // 1. Timing (The Heartbeat)
        double currentSeconds = _stopwatch.Elapsed.TotalSeconds;
        float realDeltaTime = (float)(currentSeconds - _lastElapsedSeconds);
        _lastElapsedSeconds = currentSeconds;

        _engine.Update(realDeltaTime);
        CurrentTimeDisplay = TimeSpan.FromSeconds(_clock.TotalTime).ToString(@"hh\:mm\:ss\.ff");

        ActiveEntityList.Clear();

        foreach (var entity in _engine.Manager.GetAllEntities())
        {
            // Use the ShortId here for professional-looking logs
            var detail = new StringBuilder($"[{entity.ShortId}]");

            foreach (var component in entity.GetAllComponents())
            {
                string name = component.GetType().Name.Replace("Component", "");
                detail.Append($" | {name}: {component}");
            }

            ActiveEntityList.Add(detail.ToString());
        }

        var human = _engine.Manager.Query<MetabolismComponent>().FirstOrDefault();
        if (human != null)
        {
            HungerDisplay = $"Hunger: {human.Get<MetabolismComponent>().Hunger:F1}%";
        }
    }

    // This handles the slider/input in the UI
    partial void OnTimeScaleChanged(float value)
    {
        _clock.TimeScale = value;
    }
}