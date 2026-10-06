using Avalonia.Controls;

namespace Sholto.Interface.MainUI.Views;

/// <summary>The system report modal's body (the amber status dot): which external tools the boot-time probe
/// found, and what a missing one costs. <see cref="Controls.Modal.ModalShell"/> supplies the scrim, panel,
/// headline, Close button and keys.</summary>
public partial class SystemReportOverlay : UserControl
{
    public SystemReportOverlay() => InitializeComponent();
}
