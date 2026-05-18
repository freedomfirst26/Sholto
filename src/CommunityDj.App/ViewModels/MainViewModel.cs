using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityDj.App.Controls;
using CommunityDj.App.Theming;
using CommunityDj.Audio;
using CommunityDj.Library;

namespace CommunityDj.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private int _selectedTrackIndex = -1;
    private CommunityDjTheme _theme = Themes.Plasma;

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

    public CommunityDjTheme Theme
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

    /// <summary>
    /// 0..1: 1 when both decks are playing and their nearest beats are in-phase,
    /// 0 when out of the magnetic window. Smoothstep curve.
    /// </summary>
    public double MagnetismFactor
    {
        get
        {
            if (!Deck1.Player.IsPlaying || !Deck2.Player.IsPlaying) return 0;
            var d1 = Deck1.Analysis.Basic?.DownbeatTimes;
            var d2 = Deck2.Analysis.Basic?.DownbeatTimes;
            if (d1 is null || d1.Length == 0 || d2 is null || d2.Length == 0) return 0;

            double phase1 = Deck1.PlaybackSeconds - Deck1.NearestDownbeatSec();
            double phase2 = Deck2.PlaybackSeconds - Deck2.NearestDownbeatSec();
            double misalign = Math.Abs(phase1 - phase2);

            const double window = 0.15;  // 150 ms — bar-start tolerance is wider than beat-start
            double t = Math.Min(misalign / window, 1);
            return 1 - t * t * (3 - 2 * t);  // smoothstep, 1 at t=0 → 0 at t=1
        }
    }

    /// <summary>Deck most recently nudged by the jog wheel. The other deck acts as the reference.</summary>
    public int LastJoggedDeck { get; set; } = -1;
    /// <summary>Wall-clock of last jog tick — used to detect "user let go" for quantize.</summary>
    public DateTime LastJogAt { get; set; } = DateTime.MinValue;

    // Quantize lifecycle: arm when the magnets touch (factor ≈ 1), fire once the user
    // stops jogging while still touching, then disarm until they drop out and re-approach.
    private bool _quantizePrimed;   // magnets reached touching this engagement
    private bool _quantizeFired;    // already snapped this engagement
    // Visible "touching" corresponds to misalign ≈ window·(1 - t) with smoothstep —
    // 0.85 ⇒ misalign ≈ 40ms, which is what the eye reads as "the greens met up".
    private const double TouchingThreshold = 0.85;
    private const double DisengageThreshold = 0.35;
    private static readonly TimeSpan JogIdleForQuantize = TimeSpan.FromMilliseconds(180);

    /// <summary>Push current magnetism state into each deck's MagneticGlowSec for the UI.
    /// Also runs the touch → release → quantize state machine.</summary>
    public void UpdateMagnetism()
    {
        double f = MagnetismFactor;

        // Reset the engagement once the user has clearly moved away.
        if (f < DisengageThreshold)
        {
            _quantizePrimed = false;
            _quantizeFired  = false;
        }
        else if (f >= TouchingThreshold)
        {
            _quantizePrimed = true;  // they touched
        }

        // Fire once: primed + user let go (no jog input for a beat) + still close enough.
        if (_quantizePrimed && !_quantizeFired
            && (DateTime.UtcNow - LastJogAt) > JogIdleForQuantize
            && f > DisengageThreshold)
        {
            Quantize();
            _quantizeFired = true;
        }

        // Green stripes are the "still snapping" indicator. After we've quantized this
        // engagement the user has explicitly said "leave them alone" — kill the glow.
        bool active = f > 0.3 && !_quantizeFired;
        Deck1.MagneticGlowSec = active ? Deck1.NearestDownbeatSec() : -1;
        Deck2.MagneticGlowSec = active ? Deck2.NearestDownbeatSec() : -1;
    }

    /// <summary>Snap the last-jogged deck so its downbeat phase matches the reference deck exactly.
    /// If no deck has been jogged this session, snap whichever has the larger phase error.</summary>
    private void Quantize()
    {
        DeckViewModel adjusted, reference;
        if (LastJoggedDeck == 1 || LastJoggedDeck == 2)
        {
            adjusted  = LastJoggedDeck == 1 ? Deck1 : Deck2;
            reference = LastJoggedDeck == 1 ? Deck2 : Deck1;
        }
        else
        {
            // No jog history — pick whichever deck is further from its own downbeat
            // (the one with more error to correct).
            double e1 = Math.Abs(Deck1.PlaybackSeconds - Deck1.NearestDownbeatSec());
            double e2 = Math.Abs(Deck2.PlaybackSeconds - Deck2.NearestDownbeatSec());
            (adjusted, reference) = e1 > e2 ? (Deck1, Deck2) : (Deck2, Deck1);
        }

        double refPhase = reference.PlaybackSeconds - reference.NearestDownbeatSec();
        double adjDownbeat = adjusted.NearestDownbeatSec();
        if (adjDownbeat < 0) return;
        double delta = (adjDownbeat + refPhase) - adjusted.PlaybackSeconds;
        if (Math.Abs(delta) > 0.0001) adjusted.Player.SeekRelative(delta);
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
