using Avalonia;
using System;

namespace ECSVisualizer;

/// <summary>
/// Entry point for the Avalonia desktop visualizer.
/// </summary>
internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    /// <summary>
    /// Process entry point. Builds the Avalonia application and starts the
    /// classic desktop (single-window) lifetime.
    /// </summary>
    /// <param name="args">Raw command-line arguments forwarded to Avalonia.</param>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    /// <summary>
    /// Avalonia configuration factory. Required by the Avalonia visual designer
    /// and by <see cref="Main"/>; configures platform detection, the bundled
    /// Inter font, and trace logging.
    /// </summary>
    /// <returns>A configured <see cref="AppBuilder"/> ready to launch.</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
