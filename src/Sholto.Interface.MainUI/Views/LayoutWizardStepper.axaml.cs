using Avalonia.Controls;
using Avalonia.Interactivity;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Views;

/// <summary>The Layout Wizard's two-step indicator, shown in the modal shell's header. The finished step shows
/// what was picked and goes back when clicked.</summary>
public partial class LayoutWizardStepper : UserControl
{
    public LayoutWizardStepper() => InitializeComponent();

    private void OnBack(object? sender, RoutedEventArgs e) => (DataContext as ILayoutWizardViewModel)?.Back();
}
