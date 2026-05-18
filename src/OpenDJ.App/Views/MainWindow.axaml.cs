using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Interactivity;
using OpenDJ.Audio;
using OpenDJ.App.ViewModels;
using OpenDJ.Library;

namespace OpenDJ.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
}
