using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ECSVisualizer.ViewModels;
using System;
using System.Diagnostics.CodeAnalysis;

namespace ECSVisualizer
{
    /// <summary>
    /// Given a view model, returns the corresponding view if possible.
    /// </summary>
    [RequiresUnreferencedCode(
        "Default implementation of ViewLocator involves reflection which may be trimmed away.",
        Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
    public class ViewLocator : IDataTemplate
    {
        /// <summary>
        /// Resolves a view for the supplied view model by replacing
        /// <c>ViewModel</c> with <c>View</c> in the type name and instantiating
        /// the result. Returns a placeholder <see cref="TextBlock"/> when no
        /// matching view type is found.
        /// </summary>
        /// <param name="param">The view-model instance to locate a view for.</param>
        /// <returns>The constructed view control, or <c>null</c> if <paramref name="param"/> is <c>null</c>.</returns>
        public Control? Build(object? param)
        {
            if (param is null)
                return null;

            var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
            var type = Type.GetType(name);

            if (type != null)
            {
                return (Control)Activator.CreateInstance(type)!;
            }

            return new TextBlock { Text = "Not Found: " + name };
        }

        /// <summary>
        /// Indicates whether this template applies to the supplied data
        /// object — true only for <see cref="ViewModelBase"/> derivatives.
        /// </summary>
        /// <param name="data">The candidate data-context object to test.</param>
        /// <returns><c>true</c> when <paramref name="data"/> is a <see cref="ViewModelBase"/>; <c>false</c> otherwise.</returns>
        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}
