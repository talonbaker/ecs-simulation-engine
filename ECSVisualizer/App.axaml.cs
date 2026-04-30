using System;
using APIFramework.Core;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ECSVisualizer.ViewModels;
using ECSVisualizer.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ECSVisualizer;

/// <summary>
/// Avalonia application root. Wires the dependency-injection container
/// (simulation bootstrapper + view models) and creates the main window.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// The DI service provider built during framework initialization. Null
    /// before <see cref="OnFrameworkInitializationCompleted"/> has run.
    /// </summary>
    public IServiceProvider? Services { get; private set; }

    /// <summary>
    /// Avalonia framework initialization hook. Builds the service provider,
    /// eagerly resolves the simulation so it is ticking before any UI shows,
    /// and assigns <see cref="MainWindow"/> with its <see cref="MainViewModel"/>.
    /// </summary>
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
