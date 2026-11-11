using System.ComponentModel;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The track action menu, the tag editor and the crate picker: which is open, and opening them.</summary>
public interface ITrackActionsCoordinator : INotifyPropertyChanged
{
    TagEditorViewModel? TagEditor { get; }
    bool IsTagEditorOpen { get; }
    CratePickerViewModel? CratePicker { get; }

    /// <summary>Whether the crate picker is showing (a pass-through to <see cref="CratePicker"/>).</summary>
    bool IsCratePickerOpen { get; }

    TrackActionsViewModel TrackActions { get; }
    bool IsTrackActionsOpen { get; }

    /// <summary>The database is up (or not): build the tag editor and the crate picker once it is.</summary>
    void AttachLibrary(bool available);

    Task OpenTagEditorAsync(TrackRow row);

    /// <summary>Enter on a library row opens the action menu for it.</summary>
    void OpenTrackActions(TrackRow row);

    Task OpenCratePickerAsync(TrackRow row);
}
