using Avalonia.Controls;
using Avalonia.Input;
using Sholto.App.ViewModels;

namespace Sholto.App.Views;

/// <summary>Enter-mode action menu. Keyboard nav (arrows/Enter/Esc) is handled
/// globally in MainWindow; this file just handles click-to-activate and
/// click-outside-to-close.</summary>
public partial class TrackActionsOverlay : UserControl
{
    public TrackActionsOverlay() => InitializeComponent();

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.TrackActions.Close();
        e.Handled = true;
    }

    private void OnPanelPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void OnActionActivated(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.TrackActions.Commit();
        e.Handled = true;
    }
}
