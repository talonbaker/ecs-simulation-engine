using Avalonia.Controls;

namespace ECSVisualizer.Views;

/// <summary>
/// Code-behind for the main visualizer window. Loads the AXAML markup; the
/// <see cref="ViewModels.MainViewModel"/> is supplied as <c>DataContext</c>
/// from <see cref="App.OnFrameworkInitializationCompleted"/>.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Initializes a new <see cref="MainWindow"/> and loads its AXAML.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}