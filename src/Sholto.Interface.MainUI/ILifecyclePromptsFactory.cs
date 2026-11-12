using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI;

/// <summary>Builds the <see cref="LifecyclePrompts"/> for the main window.</summary>
public interface ILifecyclePromptsFactory
{
    /// <param name="viewModel">The main view model, told the saved theme.</param>
    LifecyclePrompts Create(MainViewModel viewModel);
}
