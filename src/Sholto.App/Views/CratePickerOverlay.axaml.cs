using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Sholto.App.ViewModels;

namespace Sholto.App.Views;

/// <summary>Add-to-crate overlay. The query box owns keyboard input (the global
/// handler steps aside for focused text boxes), so nav/commit/esc live here.</summary>
public partial class CratePickerOverlay : UserControl
{
    public CratePickerOverlay()
    {
        InitializeComponent();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && IsVisible)
                Dispatcher.UIThread.Post(() => CrateQueryBox.Focus());
        };
    }

    private void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.CratePicker is null) return;
        var picker = vm.CratePicker;
        switch (e.Key)
        {
            case Key.Escape: picker.Close(); e.Handled = true; break;
            case Key.Down:   picker.Move(+1); e.Handled = true; break;
            case Key.Up:     picker.Move(-1); e.Handled = true; break;
            case Key.Enter:  _ = picker.CommitAsync(); e.Handled = true; break;
        }
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.CratePicker?.Close();
        e.Handled = true;
    }

    private void OnPanelPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void OnOptionActivated(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm) _ = vm.CratePicker?.CommitAsync();
        e.Handled = true;
    }
}
