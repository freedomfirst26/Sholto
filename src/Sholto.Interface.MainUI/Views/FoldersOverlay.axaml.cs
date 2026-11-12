using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Views;

/// <summary>The Folders modal's body: the Music and Analysis rows. Each row's Change… opens the OS folder
/// picker over the window and hands the pick to <see cref="IFoldersViewModel.Pick"/>.
/// <see cref="Controls.Modal.ModalShell"/> supplies the scrim, panel, title and buttons.</summary>
public partial class FoldersOverlay : UserControl
{
    public FoldersOverlay() => InitializeComponent();

    private async void OnChangeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: FolderRowViewModel row } || DataContext is not IFoldersViewModel vm) return;
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var start = row.Path is { } current ? await top.StorageProvider.TryGetFolderFromPathAsync(current) : null;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = $"Choose the {row.Label.ToLowerInvariant()}",
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });
        if (folders.Count == 0) return;
        var uri = folders[0].Path;
        vm.Pick(row, uri.IsFile ? uri.LocalPath : uri.ToString());
    }
}
