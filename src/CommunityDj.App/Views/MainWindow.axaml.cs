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

        // Shift modifier switches all transport keys to Deck 2.
        bool shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
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

    private async void OnTrackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems is null || e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is not TrackRow row) return;
        var track = row.Track;
        if (DataContext is not MainViewModel vm) return;

        Console.WriteLine($"[Track] selected {track.Title}");
        try
        {
            var samples = await Task.Run(() => AudioFileDecoder.Decode(track.FilePath));
            Console.WriteLine($"[Track] decoded {samples.Length} samples");
            vm.Deck1.LoadTrack(track, track.FilePath, samples);
            // Load only — user explicitly starts playback (Space, FLX-4 play, etc.)
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Track] FAILED: {ex.GetType().Name}: {ex.Message}");
        }
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
