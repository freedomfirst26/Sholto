using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Interactivity;
using Sholto.App.Controls;
using Sholto.App.Theming;
using Sholto.App.ViewModels;
using Sholto.Audio;
using Sholto.Music;

namespace Sholto.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Intercept keys before child controls (ListBox would otherwise eat arrows).
        AddHandler(KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);

        // Push the initial theme into the Window's dynamic-resource brushes so the
        // first paint already has the right colors.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm) ApplyThemeToResources(vm.Theme);
        };
    }

    /// <summary>
    /// Write the theme's colors into Window.Resources keyed under "Sholto…" names.
    /// Every UI element that needs a themed color references these via
    /// {DynamicResource Sholto…}, so the references re-evaluate without going
    /// through visual-tree traversal (which goes stale under Fluent's hover/menu states).
    /// </summary>
    private void ApplyThemeToResources(SholtoTheme theme)
    {
        Resources["SholtoBgDeep"]        = theme.BgDeep;
        Resources["SholtoSurface"]       = theme.Surface;
        Resources["SholtoSurfaceRaised"] = theme.SurfaceRaised;
        Resources["SholtoBorder"]        = theme.Border;
        Resources["SholtoPrimary"]       = theme.Primary;
        Resources["SholtoAccent"]        = theme.Accent;
        Resources["SholtoAccentBg"]      = theme.AccentBg;
        Resources["SholtoMint"]          = theme.Mint;
        Resources["SholtoTextBright"]    = theme.TextBright;
        Resources["SholtoTextMuted"]     = theme.TextMuted;
        // Foreground drawn on top of Camelot key chips. Themes pick this once so
        // dark/light text stays legible against their tuned chip palette.
        Resources["SholtoChipForeground"] = theme.CamelotPalette.OnChipForeground;
        // Tag editor + indicator brushes. Fixed for now; if themes ever want to
        // override these, add fields to SholtoTheme and forward them here.
        Resources["TagChipBackground"]     = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4C5C8A"));
        Resources["TagChipForeground"]     = Avalonia.Media.Brushes.White;
        Resources["TagIndicatorBackground"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#33000000"));
        Resources["TagIndicatorForeground"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#C0C7D6"));
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        bool shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;

        // Tag editor open → let it own input. The InputBox handles
        // Tab/Enter/Esc/Backspace/Up/Down via its own KeyDown; we only need to
        // suppress global shortcuts (space, 1, 2, P, etc.) from firing
        // underneath when the user is typing a tag. Esc is a backstop in case
        // focus isn't actually on the InputBox yet.
        if (vm.IsTagEditorOpen)
        {
            if (e.Key == Key.Escape) { vm.TagEditor?.Close(); e.Handled = true; }
            return;
        }

        // Crate picker owns input via its TextBox; Esc is a global backstop.
        if (vm.IsCratePickerOpen)
        {
            if (e.Key == Key.Escape) { vm.CratePicker?.Close(); e.Handled = true; }
            return;
        }

        // Enter-mode action menu: arrows move + Enter fires, plus direct shortcuts
        // (1/2 load a deck, C crate, T tag), Esc closes.
        if (vm.IsTrackActionsOpen)
        {
            switch (e.Key)
            {
                case Key.Up:     vm.TrackActions.Move(-1); e.Handled = true; break;
                case Key.Down:   vm.TrackActions.Move(+1); e.Handled = true; break;
                case Key.Enter:  vm.TrackActions.Commit();  e.Handled = true; break;
                case Key.Escape: vm.TrackActions.Close();   e.Handled = true; break;
                case Key.D1: case Key.NumPad1: vm.TrackActions.Invoke(TrackActionKind.LoadDeck1); e.Handled = true; break;
                case Key.D2: case Key.NumPad2: vm.TrackActions.Invoke(TrackActionKind.LoadDeck2); e.Handled = true; break;
                case Key.C: vm.TrackActions.Invoke(TrackActionKind.AddToCrate); e.Handled = true; break;
                case Key.T: vm.TrackActions.Invoke(TrackActionKind.Tag); e.Handled = true; break;
            }
            return;
        }

        // Same isolation for the search overlay.
        if (vm.IsSearchOpen) return;

        // Any other focused text input (current or future) gets full keyboard
        // ownership — global shortcuts skip when a TextBox is focused. Without
        // this, typing in any input would still trigger spacebar=search and
        // 1/2=load-deck via our Tunnel+handledEventsToo handler registration.
        if (FocusManager?.GetFocusedElement() is TextBox) return;

        // Spacebar opens search (from anywhere outside an input).
        if (e.Key == Key.Space)
        {
            vm.IsSearchOpen = true;
            e.Handled = true;
            return;
        }

        // Enter on a highlighted library row → the track action menu (Tag / Add to
        // crate / Load). Replaces the old T-to-tag shortcut.
        if (e.Key == Key.Enter)
        {
            var row = vm.SelectedTrackRow;
            if (row is not null)
            {
                vm.OpenTrackActions(row);
                e.Handled = true;
                return;
            }
        }

        // M — drop a marker on the target deck at its current position.
        if (e.Key == Key.M)
        {
            _ = vm.AddMarkerToTargetDeckAsync(shift ? 1 : 0);
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            // 1 / 2 — load the highlighted track into Deck 1 / Deck 2.
            case Key.D1: case Key.NumPad1:
                LoadSelectedInto(vm, 0); e.Handled = true; return;
            case Key.D2: case Key.NumPad2:
                LoadSelectedInto(vm, 1); e.Handled = true; return;

            // G — accelerator: open the Grid tool on the target deck (same as
            // clicking the BPM then the grid icon). That's the edit mode in which
            // the ← / → phase and click-two-kicks keys become active.
            case Key.G:
                GridTarget(vm)?.OpenEdit();
                e.Handled = true;
                return;
            case Key.Escape:
                if (vm.Deck1.EditOpen || vm.Deck2.EditOpen)
                {
                    vm.Deck1.CloseEdit();
                    vm.Deck2.CloseEdit();
                    e.Handled = true;
                    return;
                }
                break;
        }

        // Transport keys — Shift switches to Deck 2 for the actions that
        // care about which deck (P, seek). Nudge intentionally doesn't —
        // see below — so we no longer early-return on "modifier-selected
        // deck not loaded": that would silently swallow Shift+Left when
        // only Deck 1 has a loop loaded.
        var deck = shift ? vm.Deck2 : vm.Deck1;
        switch (e.Key)
        {
            // P = play/pause on the shift-selected deck. Requires that deck
            // to actually have a track.
            case Key.P:
                if (deck.Player.IsLoaded) vm.OnPlayPressed(shift ? 1 : 0);
                e.Handled = true;
                break;
            // Beatgrid editing — always active (no loop required), saved to
            // the DB automatically. Targets the deck with an active loop, or
            // the first loaded deck otherwise.
            //   ← / →          phase alignment, FINE  (±10 ms)
            //   Shift + ← / →  phase alignment, COARSE (±1 beat)
            //   ↑ / ↓          BPM width, COARSE (±0.1)
            //   Shift + ↑ / ↓  BPM width, FINE   (±0.01)
            // Workflow: ↑/↓ to fix spacing drift first, then ←/→ to align.
            case Key.Left:
            case Key.Right:
            {
                // Phase nudge — ONLY while the Grid tool is open on the target deck.
                // Outside edit mode these keys do nothing (grid stays locked).
                var target = GridTarget(vm);
                if (target is { GridTuneActive: true })
                {
                    int sign = e.Key == Key.Left ? -1 : +1;
                    if (shift) target.Player.NudgeGrid(sign);   // ±1 beat
                    else       target.Player.NudgeGridFine(sign * 0.010);
                    e.Handled = true;
                }
                break;
            }
            case Key.Up:
            case Key.Down:
            {
                // Tempo — ONLY while the tune editor is open on the target deck;
                // otherwise ↑/↓ move the track-list selection (up = previous row).
                // Shift = whole-BPM steps, plain = fine (0.1).
                var target = GridTarget(vm);
                if (target is { GridTuneActive: true })
                {
                    int sign = e.Key == Key.Up ? +1 : -1;   // up = faster BPM
                    double delta = shift ? 1.0 : 0.1;
                    target.Player.AdjustBpm(sign * delta);
                }
                else
                {
                    vm.SelectTrack(vm.SelectedTrackIndex + (e.Key == Key.Up ? -1 : +1));
                }
                e.Handled = true;
                break;
            }
        }
    }

    /// <summary>Which deck a grid edit applies to: the one with an active
    /// loop (you're tuning what you're hearing), else the first loaded deck,
    /// else null.</summary>
    private static DeckViewModel? GridTarget(MainViewModel vm)
    {
        if (vm.Deck1.Player.ActiveLoop is not null) return vm.Deck1;
        if (vm.Deck2.Player.ActiveLoop is not null) return vm.Deck2;
        if (vm.Deck1.Player.IsLoaded) return vm.Deck1;
        if (vm.Deck2.Player.IsLoaded) return vm.Deck2;
        return null;
    }

    private static void LoadSelectedInto(MainViewModel vm, int deckIndex)
        => _ = vm.LoadSelectedToDeckAsync(deckIndex);

    private void OnTrackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Selection only — no automatic load. User presses 1/2 (or FLX-4 LOAD 1/2)
        // to put the highlighted track on a deck.
        if (e.AddedItems is { Count: > 0 } && e.AddedItems[0] is TrackRow row)
            Console.WriteLine($"[Track] selected {row.Title}");
    }

    // Double-click a library row → re-run analysis on it. The single click already
    // selected the row; the VM raises the request and the orchestration layer runs
    // the same decode + re-analyze path as the browse-knob long-press.
    private void OnTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.RequestReanalyzeSelected();
    }

    private void OnTagIndicatorPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (sender is Avalonia.Controls.Control { DataContext: TrackRow row })
        {
            e.Handled = true;
            _ = vm.OpenTagEditorAsync(row);
        }
    }

    private async void OnOutputDeviceClick(object? sender, RoutedEventArgs e)
    {
        if (Avalonia.Application.Current is App app)
            await app.ChangeOutputDeviceAsync(this);
    }

    private async void OnMusicFolderClick(object? sender, RoutedEventArgs e)
    {
        if (Avalonia.Application.Current is App app)
            await app.ChangeMusicDirAsync(this);
    }


    private void OnThemeClassic          (object? sender, RoutedEventArgs e) => SetTheme(Themes.Classic);
    private void OnThemeSerato           (object? sender, RoutedEventArgs e) => SetTheme(Themes.Serato);
    private void OnThemeFrontLineAssembly(object? sender, RoutedEventArgs e) => SetTheme(Themes.FrontLineAssembly);
    private void OnThemeSilenceGroove    (object? sender, RoutedEventArgs e) => SetTheme(Themes.SilenceGroove);
    private void OnThemeJeremySoule      (object? sender, RoutedEventArgs e) => SetTheme(Themes.JeremySoule);
    private void OnThemeDrabMajesty      (object? sender, RoutedEventArgs e) => SetTheme(Themes.DrabMajesty);
    private void OnThemeSubFocus         (object? sender, RoutedEventArgs e) => SetTheme(Themes.SubFocus);
    private void OnThemeTypeONegative    (object? sender, RoutedEventArgs e) => SetTheme(Themes.TypeONegative);
    private void OnThemeBirthdayMassacre (object? sender, RoutedEventArgs e) => SetTheme(Themes.BirthdayMassacre);
    private void OnThemeBoardsOfCanada   (object? sender, RoutedEventArgs e) => SetTheme(Themes.BoardsOfCanada);
    private void OnThemePantera          (object? sender, RoutedEventArgs e) => SetTheme(Themes.Pantera);

    private void SetTheme(SholtoTheme theme)
    {
        if (DataContext is MainViewModel vm) vm.Theme = theme;
        ApplyThemeToResources(theme);
    }
}
