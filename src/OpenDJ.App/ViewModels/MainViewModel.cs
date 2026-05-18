using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenDJ.App.Controls;
using OpenDJ.App.Theming;
using OpenDJ.Audio;
using OpenDJ.Library;

namespace OpenDJ.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private int _selectedTrackIndex = -1;
    private OpenDjTheme _theme = Themes.Plasma;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<TrackRow> Tracks { get; } = [];

    public DeckViewModel DeckA { get; }

    public MainViewModel()
    {
        DeckA = new DeckViewModel(new DeckPlayer());
        DeckA.Player.AnalysisUpdated += () =>
        {
            // Push the freshly-computed BPM back into the matching row so the list uplifts.
            var path = DeckA.LoadedTrack?.FilePath;
            var bpm = DeckA.Analysis.Basic?.Bpm;
            if (path is null || bpm is null) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var row in Tracks)
                    if (row.FilePath == path) row.Bpm = bpm;
            });
        };
    }

    public OpenDjTheme Theme
    {
        get => _theme;
        set
        {
            if (_theme == value) return;
            _theme = value;
            Notify();
            Notify(nameof(WaveformPalette));
        }
    }

    public WaveformPalette WaveformPalette => _theme.WaveformPalette;

    public int SelectedTrackIndex
    {
        get => _selectedTrackIndex;
        set { _selectedTrackIndex = value; Notify(); Notify(nameof(SelectedTrack)); }
    }

    public Track? SelectedTrack =>
        SelectedTrackIndex >= 0 && SelectedTrackIndex < Tracks.Count
            ? Tracks[SelectedTrackIndex].Track
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
        DeckA.LoadTrack(SelectedTrack, SelectedTrack.FilePath, samples);
    }

    public void OnPlayPressed(int deck)
    {
        if (deck == 0) DeckA.TogglePlay();
    }

    /// <summary>Apply a batch of cached BPMs (from the DB on startup) to whatever rows are loaded.</summary>
    public void SetKnownBpms(IReadOnlyDictionary<string, double> bpms)
    {
        foreach (var row in Tracks)
            if (bpms.TryGetValue(row.FilePath, out var bpm)) row.Bpm = bpm;
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
