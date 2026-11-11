using System.ComponentModel;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The part of a deck's view model the main view model reaches: its tuner (open, close, nudge,
/// BPM) and the theme refresh. <see cref="IsLoaded"/> has no production caller through the main view model;
/// it is here so the main view model tests read it without a cast.</summary>
public interface IDeckViewModel : INotifyPropertyChanged
{
    bool IsLoaded { get; }
    bool EditOpen { get; }
    void CloseEditor();
    void NudgeGrid(int beats);
    void NudgeGridFine(double seconds);
    void AdjustBpm(double delta);
    void RefreshThemeBindings();
}
