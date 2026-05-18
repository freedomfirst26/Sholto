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

    public DeckViewModel Deck1 { get; }
    public DeckViewModel Deck2 { get; }

    public MainViewModel()
    {
        Deck1 = new DeckViewModel(new DeckPlayer());
        Deck2 = new DeckViewModel(new DeckPlayer());
        WireDeck(Deck1);
        WireDeck(Deck2);
    }

    private void WireDeck(DeckViewModel deck)
    {
        deck.Player.AnalysisUpdated += () =>
        {
            var path = deck.LoadedTrack?.FilePath;
            var bpm = deck.Analysis.Basic?.Bpm;
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
        Deck1.LoadTrack(SelectedTrack, SelectedTrack.FilePath, samples);
    }

    public DeckViewModel DeckFor(int deck) => deck == 1 ? Deck2 : Deck1;

    private double _crossfader = 0.5;
    /// <summary>0..1, 0 = full Deck 1, 1 = full Deck 2. Applies equal-power gains to each deck.</summary>
    public double Crossfader
    {
        get => _crossfader;
        set
        {
            _crossfader = Math.Clamp(value, 0.0, 1.0);
            // Equal-power crossfade: cosine curve so the perceived loudness stays flat
            // through the centre instead of dipping like a linear crossfade would.
            // Equal-power crossfade: cosine curve so perceived loudness stays flat
            // through the centre. Each deck combines this with its own channel-fader gain.
            double angle = _crossfader * (Math.PI / 2);
            Deck1.SetCrossfadeGain((float)Math.Cos(angle));
            Deck2.SetCrossfadeGain((float)Math.Sin(angle));
            Notify();
        }
    }

    public void OnPlayPressed(int deck) => DeckFor(deck).TogglePlay();

    public void SetKnownBpms(IReadOnlyDictionary<string, double> bpms)
    {
        foreach (var row in Tracks)
            if (bpms.TryGetValue(row.FilePath, out var bpm)) row.Bpm = bpm;
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
