using System;
using APIFramework.Core;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ECSVisualizer.ViewModels;
using ECSVisualizer.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ECSVisualizer;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        // The bootstrapper owns the entire headless simulation.
        // No system registration or entity spawning happens here.
        services.AddSingleton<SimulationBootstrapper>();

        // ViewModels
        services.AddTransient<MainViewModel>();

        Services = services.BuildServiceProvider();

        // Resolve the bootstrapper so the simulation is ready before the window opens
        Services.GetRequiredService<SimulationBootstrapper>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
