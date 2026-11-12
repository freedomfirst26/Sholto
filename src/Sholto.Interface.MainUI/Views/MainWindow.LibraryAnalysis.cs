using Avalonia.Interactivity;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Views;

/// <summary>The Settings menu's library-analysis entries: start it, and cancel it.</summary>
public partial class MainWindow
{
    private void OnAnalyzeLibraryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.RequestAnalyzeLibrary();
    }

    private void OnCancelLibraryAnalysisClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.RequestCancelLibraryAnalysis();
    }
}
