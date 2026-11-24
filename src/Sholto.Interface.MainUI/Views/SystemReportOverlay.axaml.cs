using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Views;

/// <summary>The system report modal's body (the amber status dot): the verdict banner and one card per capability
/// the boot-time probe covers. <see cref="Controls.Modal.ModalShell"/> supplies the scrim, panel, eyebrow, Close
/// button and keys. Copy buttons put an install command on the clipboard; that is presentation, so it lives here.</summary>
public partial class SystemReportOverlay : UserControl
{
    private const string CopyLabel = "Copy";
    private const string CopiedLabel = "Copied";

    private readonly List<Button> _copied = [];

    public SystemReportOverlay()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SystemReportCapability capability } button) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        await clipboard.SetTextAsync(capability.FixCommand);
        button.Content = CopiedLabel;
        _copied.Add(button);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is INotifyPropertyChanged vm)
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName != nameof(ISystemReportViewModel.IsOpen)
                    || vm is not ISystemReportViewModel { IsOpen: false }) return;
                foreach (var b in _copied) b.Content = CopyLabel;
                _copied.Clear();
            };
    }
}
