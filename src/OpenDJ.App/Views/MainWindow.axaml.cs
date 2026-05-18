using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenDJ.App.ViewModels;

namespace OpenDJ.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnPlayButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.OnPlayPressed(deck: 0);
    }
}
