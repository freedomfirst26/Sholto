using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Sholto.App.ViewModels;

namespace Sholto.App.Views;

/// <summary>
/// Spacebar-triggered search overlay. The XAML wires up the data bindings;
/// this file handles keyboard navigation inside the query box (up/down to
/// move the selection, enter to commit, escape to dismiss) and click-outside-
/// to-close on the backdrop.
/// </summary>
public partial class SearchOverlay : UserControl
{
    public SearchOverlay()
    {
        InitializeComponent();
        // Focus the input every time the overlay becomes visible so the user
        // can start typing immediately after pressing space.
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && IsVisible)
                Dispatcher.UIThread.Post(() => QueryBox.Focus());
        };
    }

    private void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var search = vm.Search;
        switch (e.Key)
        {
            case Key.Escape:
                vm.IsSearchOpen = false;
                e.Handled = true;
                break;

            case Key.Down:
                if (search.Results.Count > 0)
                    search.SelectedIndex = Math.Min(search.Results.Count - 1, search.SelectedIndex + 1);
                e.Handled = true;
                break;

            case Key.Up:
                if (search.Results.Count > 0)
                    search.SelectedIndex = Math.Max(0, search.SelectedIndex - 1);
                e.Handled = true;
                break;

            case Key.Enter:
                // Commit: select the row in the library, then load it into the
                // currently-highlighted deck-target chip (LoadTargetDeck),
                // then close. The default deck is picked when the overlay
                // opens — see MainViewModel.PickDefaultLoadTarget.
                CommitSelection(vm);
                _ = vm.LoadSelectedToDeckAsync(vm.LoadTargetDeck);
                vm.IsSearchOpen = false;
                e.Handled = true;
                break;

            case Key.Left:
                // Toggle deck-target highlight between 1 and 2. Two decks only,
                // so left/right are equivalent — both just flip the selection.
                vm.LoadTargetDeck = vm.LoadTargetDeck == 0 ? 1 : 0;
                e.Handled = true;
                break;
            case Key.Right:
                vm.LoadTargetDeck = vm.LoadTargetDeck == 0 ? 1 : 0;
                e.Handled = true;
                break;

            case Key.D1:
            case Key.NumPad1:
                vm.LoadTargetDeck = 0;
                LoadIntoDeck(vm, deckIndex: 0);
                e.Handled = true;
                break;
            case Key.D2:
            case Key.NumPad2:
                vm.LoadTargetDeck = 1;
                LoadIntoDeck(vm, deckIndex: 1);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Backdrop click → close. Inner-panel clicks shouldn't close,
    /// so the panel itself swallows pointer events via <see cref="OnPanelPressed"/>.</summary>
    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.IsSearchOpen = false;
        e.Handled = true;
    }

    private void OnPanelPressed(object? sender, PointerPressedEventArgs e)
    {
        // Stop the click from bubbling up to the backdrop handler.
        e.Handled = true;
    }

    private void OnCrateRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (sender is Control { Tag: Sholto.Storage.CrateSummary crate })
        {
            e.Handled = true;
            vm.Search.PickCrate(crate);
        }
    }

    private void OnTagChipPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel vm) return;
        if (sender is Avalonia.Controls.Control { Tag: string name })
        {
            e.Handled = true;
            vm.Search.PickTag(name);
        }
    }

    private static void CommitSelection(MainViewModel vm)
    {
        var row = vm.Search.SelectedRow;
        if (row is null) return;
        // Re-find the selected row inside the main Tracks list — index in
        // Search.Results doesn't map to Tracks because the search filters.
        for (int i = 0; i < vm.Tracks.Count; i++)
        {
            if (ReferenceEquals(vm.Tracks[i], row))
            {
                vm.SelectTrack(i);
                return;
            }
        }
    }

    private static void LoadIntoDeck(MainViewModel vm, int deckIndex)
    {
        CommitSelection(vm);
        _ = vm.LoadSelectedToDeckAsync(deckIndex);
        vm.IsSearchOpen = false;
    }
}
