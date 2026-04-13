using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ECSVisualizer.ViewModels;
using ECSVisualizer.Views;
using Microsoft.Extensions.DependencyInjection; // Ensure this is installed via NuGet
using System;


namespace ECSVisualizer;

public partial class App : Application
{
    // This defines 'Services' so the rest of the class can see it
    public IServiceProvider? Services { get; private set; }

    public override void OnFrameworkInitializationCompleted()
    {
        // 1. You MUST declare and instantiate it first
        var serviceCollection = new ServiceCollection();

        // 2. Register all your dependencies
        ConfigureServices(serviceCollection);

        // 3. Build the provider and store it in the class property
        Services = serviceCollection.BuildServiceProvider();

        var engine = Services.GetRequiredService<SimulationEngine>();
        var manager = Services.GetRequiredService<EntityManager>();

        engine.AddSystem(new MetabolismSystem());
        engine.AddSystem(new FeedingSystem());
        engine.AddSystem(new EsophagusSystem());

        // Create human entity
        EntityTemplates.SpawnHuman(manager);

        // Create cat entity
        EntityTemplates.SpawnCat(manager);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Views.MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var manager = Services?.GetRequiredService<EntityManager>();
        var engine = Services?.GetRequiredService<SimulationEngine>();

        // Create the Human entity
        var human = manager?.CreateEntity();
        human?.Add(new MetabolismComponent { Hunger = 0f, HungerRate = 5.0f }); // Increases 0.5 per sec

        // 2. Register the System
        engine?.AddSystem(new MetabolismSystem());
        engine?.AddSystem(new FeedingSystem());
        engine?.AddSystem(new EsophagusSystem());

        // Foundation
        services.AddSingleton<EntityManager>();
        services.AddSingleton<SimulationClock>();

        // Engine
        services.AddSingleton<SimulationEngine>();

        // ViewModels
        services.AddTransient<MainViewModel>();
    }
}