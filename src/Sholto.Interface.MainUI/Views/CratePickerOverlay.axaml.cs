using Avalonia.Controls;
using Avalonia.Input;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Views;

/// <summary>The add-to-crate body: query box and crate list, hosted in a <c>ModalShell</c>. Keys are routed
/// from MainWindow; the only input handled here is a double-tap on a row.</summary>
public partial class CratePickerOverlay : UserControl
{
    public CratePickerOverlay() => InitializeComponent();

    private void OnOptionActivated(object? sender, TappedEventArgs e)
    {
        if (DataContext is CratePickerViewModel picker) picker.Confirm();
        e.Handled = true;
    }
}
