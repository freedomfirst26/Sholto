using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Interactivity;
using OpenDJ.App.Controls;
using OpenDJ.App.ViewModels;
using OpenDJ.Audio;
using OpenDJ.Library;

namespace OpenDJ.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Intercept keys before child controls (ListBox would otherwise eat arrows).
        AddHandler(KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm || !vm.DeckA.Player.IsLoaded) return;

        switch (e.Key)
        {
            case Key.Space:
                vm.OnPlayPressed(deck: 0);
                e.Handled = true;
                break;
            case Key.Left:
                vm.DeckA.Player.SeekRelative(-10.0);
                e.Handled = true;
                break;
            case Key.Right:
                vm.DeckA.Player.SeekRelative(+10.0);
                e.Handled = true;
                break;
        }
    }

    private async void OnTrackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems is null || e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is not Track track) return;
        if (DataContext is not MainViewModel vm) return;

        Console.WriteLine($"[Track] selected {track.Title}");
        try
        {
            var samples = await Task.Run(() => AudioFileDecoder.Decode(track.FilePath));
            Console.WriteLine($"[Track] decoded {samples.Length} samples");
            vm.DeckA.LoadTrack(track, samples);
            if (!vm.DeckA.IsPlaying) vm.OnPlayPressed(deck: 0);
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

    private void OnPaletteBands(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.WaveformPalette = WaveformPalette.Bands;
    }

    private void OnPaletteHot(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.WaveformPalette = WaveformPalette.Hot;
    }
}
