using Avalonia.Controls;

namespace Sholto.Interface.MainUI.Views;

/// <summary>The Settings modal's body (Settings ▸ Settings…): two cards, the backspin time and distance knobs.
/// <see cref="Controls.Modal.ModalShell"/> supplies the scrim, panel, title, Close button and keys.</summary>
public partial class SettingsOverlay : UserControl
{
    public SettingsOverlay()
    {
        InitializeComponent();
    }
}
