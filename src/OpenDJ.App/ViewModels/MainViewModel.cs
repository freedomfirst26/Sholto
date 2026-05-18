using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenDJ.App.Controls;
using OpenDJ.Audio;
using OpenDJ.Library;

namespace OpenDJ.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private int _selectedTrackIndex = -1;
    private WaveformPalette _waveformPalette = WaveformPalette.Bands;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<Track> Tracks { get; } = [];

    public DeckViewModel DeckA { get; } = new DeckViewModel(new DeckPlayer());

    public WaveformPalette WaveformPalette
    {
        get => _waveformPalette;
        set { if (_waveformPalette == value) return; _waveformPalette = value; Notify(); }
    }

    public int SelectedTrackIndex
    {
        get => _selectedTrackIndex;
        set { _selectedTrackIndex = value; Notify(); Notify(nameof(SelectedTrack)); }
    }

    public Track? SelectedTrack =>
        SelectedTrackIndex >= 0 && SelectedTrackIndex < Tracks.Count
            ? Tracks[SelectedTrackIndex]
            : null;

    public void SelectTrack(int index)
    {
        if (Tracks.Count == 0) return;
        SelectedTrackIndex = Math.Clamp(index, 0, Tracks.Count - 1);
    }

    public void OnBrowseRotated(int delta)
    {
        if (Tracks.Count == 0) return;
        int next = SelectedTrackIndex < 0 ? 0 : SelectedTrackIndex + delta;
        SelectTrack(next);
    }

    public void OnBrowsePressed(Func<Track, float[]> decodeTrack)
    {
        if (SelectedTrack is null) return;
        var samples = decodeTrack(SelectedTrack);
        DeckA.LoadTrack(SelectedTrack, samples);
    }

    public void OnPlayPressed(int deck)
    {
        if (deck == 0) DeckA.TogglePlay();
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
