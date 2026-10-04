using Avalonia.Controls;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI;

/// <summary>Builds the <see cref="LifecyclePrompts"/> for the main window.</summary>
public interface ILifecyclePromptsFactory
{
    /// <param name="viewModel">The main view model, told the saved theme.</param>
    /// <param name="owner">The window the pickers open over.</param>
    LifecyclePrompts Create(MainViewModel viewModel, Window owner);
}
