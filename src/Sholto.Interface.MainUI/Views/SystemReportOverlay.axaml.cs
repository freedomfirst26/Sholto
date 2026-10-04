using Avalonia.Controls;
using Avalonia.Input;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Views;

/// <summary>What the amber status dot opens: which external tools the boot-time probe
/// found, and what a missing one costs. Same shape as
/// <see cref="TrackActionsOverlay"/> — Esc is handled globally in MainWindow, this
/// file only does click-outside-to-close.</summary>
public partial class SystemReportOverlay : UserControl
{
    public SystemReportOverlay() => InitializeComponent();

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.CloseSystemReport();
        e.Handled = true;
    }

    private void OnPanelPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;
}
