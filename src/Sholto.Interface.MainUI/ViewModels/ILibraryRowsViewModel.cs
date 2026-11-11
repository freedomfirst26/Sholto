using System.Collections.ObjectModel;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The part of the library rows the main view model reaches: the visible rows and the theme refresh.</summary>
public interface ILibraryRowsViewModel
{
    ObservableCollection<TrackRow> Items { get; }
    void RefreshThemeBindings();
}
