using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Views;

/// <summary>In-window "Choose Audio Output" picker. Arrow/Enter/Esc are handled globally in MainWindow;
/// this file handles clicks. A click outside the panel does nothing: the app needs an answer.</summary>
public partial class OutputPickerOverlay : UserControl
{
    public OutputPickerOverlay() => InitializeComponent();

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void OnPanelPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void OnDeviceActivated(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.OutputPicker.Commit();
        e.Handled = true;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.OutputPicker.Cancel();
    }

    private void OnUseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.OutputPicker.Commit();
    }
}
