using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Interactivity;
using CommunityDj.App.Controls;
using CommunityDj.App.Theming;
using CommunityDj.App.ViewModels;
using CommunityDj.Audio;
using CommunityDj.Library;

namespace CommunityDj.App.Views;

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
    /// Write the theme's colors into Window.Resources keyed under "CommunityDj…" names.
    /// Every UI element that needs a themed color references these via
    /// {DynamicResource CommunityDj…}, so the references re-evaluate without going
    /// through visual-tree traversal (which goes stale under Fluent's hover/menu states).
    /// </summary>
    private void ApplyThemeToResources(CommunityDjTheme theme)
    {
        Resources["CommunityDjBgDeep"]        = theme.BgDeep;
        Resources["CommunityDjSurface"]       = theme.Surface;
        Resources["CommunityDjSurfaceRaised"] = theme.SurfaceRaised;
        Resources["CommunityDjBorder"]        = theme.Border;
        Resources["CommunityDjPrimary"]       = theme.Primary;
        Resources["CommunityDjAccent"]        = theme.Accent;
        Resources["CommunityDjAccentBg"]      = theme.AccentBg;
        Resources["CommunityDjMint"]          = theme.Mint;
        Resources["CommunityDjTextBright"]    = theme.TextBright;
        Resources["CommunityDjTextMuted"]     = theme.TextMuted;
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        bool shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;

        switch (e.Key)
        {
            // 1 / 2 — load the highlighted track into Deck 1 / Deck 2.
            case Key.D1: case Key.NumPad1:
                LoadSelectedInto(vm, 0); e.Handled = true; return;
            case Key.D2: case Key.NumPad2:
                LoadSelectedInto(vm, 1); e.Handled = true; return;
        }

        // Transport keys — Shift switches to Deck 2.
        var deck = shift ? vm.Deck2 : vm.Deck1;
        if (!deck.Player.IsLoaded) return;
        switch (e.Key)
        {
            case Key.Space:
                vm.OnPlayPressed(shift ? 1 : 0);
                e.Handled = true;
                break;
            case Key.Left:
                deck.Player.SeekRelative(-10.0);
                e.Handled = true;
                break;
            case Key.Right:
                deck.Player.SeekRelative(+10.0);
                e.Handled = true;
                break;
        }
    }

    private static async void LoadSelectedInto(MainViewModel vm, int deckIndex)
    {
        var track = vm.SelectedTrack;
        if (track is null) return;
        try
        {
            var samples = await Task.Run(() => AudioFileDecoder.Decode(track.FilePath));
            vm.DeckFor(deckIndex).LoadTrack(track, track.FilePath, samples);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Track] load into deck {deckIndex + 1} FAILED: {ex.Message}");
        }
    }

    private void OnTrackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Selection only — no automatic load. User presses 1/2 (or FLX-4 LOAD 1/2)
        // to put the highlighted track on a deck.
        if (e.AddedItems is { Count: > 0 } && e.AddedItems[0] is TrackRow row)
            Console.WriteLine($"[Track] selected {row.Title}");
    }

    private async void OnOutputDeviceClick(object? sender, RoutedEventArgs e)
    {
        if (Avalonia.Application.Current is App app)
            await app.ChangeOutputDeviceAsync(this);
    }

    private void OnThemeClassic(object? sender, RoutedEventArgs e) => SetTheme(Themes.Classic);
    private void OnThemePlasma (object? sender, RoutedEventArgs e) => SetTheme(Themes.Plasma);
    private void OnThemeSerato (object? sender, RoutedEventArgs e) => SetTheme(Themes.Serato);

    private void SetTheme(CommunityDjTheme theme)
    {
        if (DataContext is MainViewModel vm) vm.Theme = theme;
        ApplyThemeToResources(theme);
    }
}
