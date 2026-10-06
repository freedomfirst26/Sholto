using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Views;

/// <summary>The Layout Wizard's step bodies (Settings ▸ Layout Wizard…): the theme gallery and the waveform
/// cards. <see cref="Controls.Modal.ModalShell"/> supplies the scrim, panel, title, buttons and keys; this file
/// maps card clicks to the view model.</summary>
public partial class LayoutWizardOverlay : UserControl
{
    public LayoutWizardOverlay()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (Wizard is { } wizard) wizard.PropertyChanged += OnWizardChanged;
        };
    }

    /// <summary>Keep the picked theme card in view when the arrow keys move it past the scroll edge.</summary>
    private void OnWizardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ILayoutWizardViewModel.SelectedTheme) || Wizard?.SelectedTheme is not { } picked)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            var inBuiltIn = Wizard?.BuiltInThemes.ToList().IndexOf(picked) ?? -1;
            var card = inBuiltIn >= 0
                ? BuiltInList.ContainerFromIndex(inBuiltIn)
                : UserList.ContainerFromIndex(Wizard?.UserThemes.ToList().IndexOf(picked) ?? -1);
            card?.BringIntoView();
        }, DispatcherPriority.Loaded);
    }

    private ILayoutWizardViewModel? Wizard => DataContext as ILayoutWizardViewModel;

    private void OnCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: WaveformStyleOption option }) Wizard?.Select(option);
        e.Handled = true;
    }

    private void OnThemeCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: ThemeOption option }) Wizard?.SelectTheme(option);
        e.Handled = true;
    }
}
