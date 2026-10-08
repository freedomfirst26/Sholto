using Avalonia.Controls;
using Avalonia.Interactivity;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Views;

/// <summary>The Track List strip. Binds to an <see cref="ITrackListViewModel"/>; the only thing it adds is
/// routing a chip's × to the view model, which sends the command.</summary>
public partial class TrackListHeader : UserControl
{
    public TrackListHeader() => InitializeComponent();

    private void OnSourceRemoved(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: TrackListSourceChip chip } && DataContext is ITrackListViewModel vm)
            vm.RemoveSource(chip.Key);
    }
}
