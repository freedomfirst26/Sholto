namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>Builds the Glance view model together with the collaborators it is handed.</summary>
public interface IGlanceViewModelFactory
{
    /// <summary>A new view model; concrete because the harness and the tests drive its frame and event handlers.</summary>
    GlanceViewModel Create();
}
