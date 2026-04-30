using CommunityToolkit.Mvvm.ComponentModel;

namespace ECSVisualizer.ViewModels
{
    /// <summary>
    /// Base class for all view models in the visualizer. Inherits from
    /// CommunityToolkit.Mvvm's <see cref="ObservableObject"/> so derived view
    /// models automatically support <c>INotifyPropertyChanged</c> and the
    /// source-generator-friendly <c>[ObservableProperty]</c> attribute.
    /// </summary>
    public abstract class ViewModelBase : ObservableObject
    {
    }
}
