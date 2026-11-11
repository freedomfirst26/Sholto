namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Builds the main window's view model.</summary>
public interface IMainViewModelFactory
{
    /// <summary>Make the view model. Call once: the handed-in view models are wired by each call.</summary>
    MainViewModel Create();
}
