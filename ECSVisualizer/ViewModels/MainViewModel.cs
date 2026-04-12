using APIFramework.Components;
using APIFramework.Core;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Diagnostics;
using System.Linq;

namespace ECSVisualizer.ViewModels;

public partial class MainViewModel : ObservableObject
{
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
        // 1. Get the current exact time from the CPU
        double currentElapsedSeconds = _stopwatch.Elapsed.TotalSeconds;

        // 2. Calculate the actual Delta (the difference since last tick)
        float realDeltaTime = (float)(currentElapsedSeconds - _lastElapsedSeconds);
        _lastElapsedSeconds = currentElapsedSeconds;

        // 3. Update the engine with the REAL delta
        _engine.Update(realDeltaTime);

        // 4. Update the UI
        CurrentTimeDisplay = TimeSpan.FromSeconds(_clock.TotalTime).ToString(@"hh\:mm\:ss\.ff");

        // Find our human and update the UI string
        var human = _engine.Manager.Query<MetabolismComponent>().FirstOrDefault();
        if (human != null)
        {
            var meta = human.Get<MetabolismComponent>();
            HungerDisplay = $"Hunger: {meta.Hunger:F1}%";
        }
    }

    // This handles the slider/input in the UI
    partial void OnTimeScaleChanged(float value)
    {
        _clock.TimeScale = value;
    }
}