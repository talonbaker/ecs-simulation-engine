using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using ECSVisualizer.ViewModels;
using ECSVisualizer.Views;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using APIFramework.Core;

namespace ECSVisualizer
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Foundational Components
            services.AddSingleton<EntityManager>();
            services.AddSingleton<SimulationClock>();

            // The Engine that runs the simulation and updates all systems
            services.AddSingleton<SimulationEngine>();

            // The ViewModels
            services.AddTransient<MainWindowViewModel>();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}