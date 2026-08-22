using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Sholto.App.ViewModels;

namespace Sholto.App.Views;

public partial class TagEditorView : UserControl
{
    public TagEditorView()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // Focus the input box every time the editor becomes visible so the
        // user can type immediately and so Esc reaches OnInputKeyDown.
        if (change.Property == IsVisibleProperty && change.GetNewValue<bool>())
            Avalonia.Threading.Dispatcher.UIThread.Post(FocusInput);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public void FocusInput()
    {
        var box = this.FindControl<TextBox>("InputBox");
        box?.Focus();
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is TagEditorViewModel vm) vm.Close();
    }

    private void OnPanelPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private async void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not TagEditorViewModel vm) return;

        switch (e.Key)
        {
            case Key.Tab:
                e.Handled = true;
                await vm.CommitAsync();
                break;
            case Key.Enter:
                e.Handled = true;
                await vm.CommitAndCloseAsync();
                break;
            case Key.Escape:
                e.Handled = true;
                vm.Close();
                break;
            case Key.Back when string.IsNullOrEmpty(vm.Input):
                e.Handled = true;
                await vm.RemoveLastChipAsync();
                break;
            case Key.Up:
                e.Handled = true;
                vm.MoveSuggestion(-1);
                break;
            case Key.Down:
                e.Handled = true;
                vm.MoveSuggestion(+1);
                break;
        }
    }

    private async void OnRemoveChipClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not TagEditorViewModel vm) return;
        if (sender is Button { Tag: string name }) await vm.RemoveChipAsync(name);
    }
}
