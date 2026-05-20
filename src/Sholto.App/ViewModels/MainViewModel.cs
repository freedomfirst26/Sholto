using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Analysis;
using Sholto.App.Controls;
using Sholto.App.Theming;
using Sholto.Audio;
using Sholto.Library;

namespace Sholto.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private int _selectedTrackIndex = -1;
    private SholtoTheme _theme = Themes.TokyoNight;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<TrackRow> Tracks { get; } = [];

    public DeckViewModel Deck1 { get; }
    public DeckViewModel Deck2 { get; }

    /// <summary>Single reporter instance shared by both decks. Anywhere in the app can
    /// listen to <see cref="AnalysisReporter.Updated"/> to surface per-track progress.</summary>
    public AnalysisReporter Reporter { get; } = new();

    private string? _debugStats;
    /// <summary>Top-bar CPU/RAM readout when SHOLTO_DEBUG_STATS=1. Null otherwise — the
    /// bound TextBlock auto-hides via its IsVisible binding on string-empty.</summary>
    public string? DebugStats
    {
        get => _debugStats;
        set { _debugStats = value; Notify(); Notify(nameof(DebugStatsVisible)); }
    }
    public bool DebugStatsVisible => !string.IsNullOrEmpty(_debugStats);

    public MainViewModel()
    {
        Deck1 = new DeckViewModel(new DeckPlayer { Reporter = Reporter });
        Deck2 = new DeckViewModel(new DeckPlayer { Reporter = Reporter });
        Deck1.PersistBpmMultiplier = RaiseBpmMultiplierChanged;
        Deck2.PersistBpmMultiplier = RaiseBpmMultiplierChanged;
        WireDeck(Deck1);
        WireDeck(Deck2);

        // Surface any analysis-in-progress on its row's spinner.
        Reporter.Updated += report =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var row in Tracks)
                    if (row.FilePath == report.FilePath) row.IsAnalyzing = report.IsBusy;
            });
        };
    }

    private void WireDeck(DeckViewModel deck)
    {
        deck.Player.AnalysisUpdated += () =>
        {
            var path = deck.LoadedTrack?.FilePath;
            if (path is null) return;
            var bpm = deck.Analysis.Basic?.Bpm;
            var stems = deck.Analysis.Get<StemPaths>();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var row in Tracks)
                {
                    if (row.FilePath != path) continue;
                    if (bpm is not null) row.Bpm = bpm;
                    if (stems is not null) row.StemsReady = true;
                }
            });
        };
    }

    public SholtoTheme Theme
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

    /// <summary>Look up any persisted ½ / ×2 override for this file path.</summary>
    public double GetBpmMultiplierFor(string filePath)
    {
        foreach (var row in Tracks)
            if (row.FilePath == filePath) return row.BpmMultiplier;
        return 1.0;
    }

    public void OnBrowsePressed(Func<Track, float[]> decodeTrack)
    {
        if (SelectedTrack is null) return;
        var samples = decodeTrack(SelectedTrack);
        Deck1.LoadTrack(SelectedTrack, SelectedTrack.FilePath, samples,
            GetBpmMultiplierFor(SelectedTrack.FilePath));
    }

    /// <summary>Long-press on the browse / song-select button: force-reanalyze the
    /// highlighted track (decode → compute → overwrite both cache tiers). Used to
    /// rescue tracks whose cached BPM/beats are wrong (e.g. madmom misread).</summary>
    public async Task OnBrowseHeldAsync(
        Func<Track, float[]> decodeTrack,
        Sholto.Analysis.AnalysisProvider analysisProvider)
    {
        var track = SelectedTrack;
        if (track is null) return;

        try
        {
            var samples = await Task.Run(() => decodeTrack(track));
            var analysis = await analysisProvider.RecomputeAsync(
                track.FilePath, samples, Sholto.Audio.AudioFileDecoder.TargetSampleRate);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var row in Tracks)
                    if (row.FilePath == track.FilePath) row.Bpm = analysis.Bpm;
            });
            Console.WriteLine($"[MainVM] re-analyzed {track.FilePath}: {analysis.Bpm:F1} BPM");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainVM] re-analyze failed: {ex.Message}");
        }
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

    /// <summary>Hydrate per-track BPM overrides (½ / ×2 corrections for madmom
    /// half/double-tempo mistakes) from the database into the track rows.</summary>
    public void SetKnownBpmMultipliers(IReadOnlyDictionary<string, double> multipliers)
    {
        foreach (var row in Tracks)
            if (multipliers.TryGetValue(row.FilePath, out var m)) row.BpmMultiplier = m;
    }

    /// <summary>Raised by deck VMs when the user halves/doubles the BPM of a loaded
    /// track. The app subscribes and persists to SQLite.</summary>
    public event Action<string, double>? BpmMultiplierChanged;
    internal void RaiseBpmMultiplierChanged(string filePath, double multiplier)
    {
        // Also update the matching TrackRow so the library list reflects the change.
        foreach (var row in Tracks)
            if (row.FilePath == filePath) row.BpmMultiplier = multiplier;
        BpmMultiplierChanged?.Invoke(filePath, multiplier);
    }

    /// <summary>Walks every track and asks the stem cache if its 4 WAVs are already
    /// on disk; flips <see cref="TrackRow.StemsReady"/> for the hits. Runs off the
    /// UI thread because each check hashes 1 MiB of the file.</summary>
    public Task HydrateStemStateAsync()
    {
        var rows = Tracks.ToArray();  // snapshot so we don't race the collection
        return Task.Run(() =>
        {
            foreach (var row in rows)
            {
                bool cached;
                try { cached = DemucsStemAnalyzer.AreCached(row.FilePath); }
                catch { cached = false; }
                if (cached)
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => row.StemsReady = true);
            }
        });
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

    // Quantize state: cleared when decks separate, set once they snap.
    private bool _quantizeFired;
    private const double EngageThreshold = 0.3;     // same as glow threshold — see one, fire one
    private const double DisengageThreshold = 0.15; // hysteresis to avoid re-fire chatter
    private static readonly TimeSpan JogIdleForQuantize = TimeSpan.FromMilliseconds(180);

    /// <summary>Push current magnetism state into each deck's MagneticGlowSec for the UI.
    /// Also runs the engaged → release → quantize state machine.</summary>
    public void UpdateMagnetism()
    {
        double f = MagnetismFactor;

        if (f < DisengageThreshold)
            _quantizeFired = false;  // user pulled them apart; re-arm

        // Fire once: greens visible + user let go of the jog for a beat.
        if (!_quantizeFired
            && f >= EngageThreshold
            && (DateTime.UtcNow - LastJogAt) > JogIdleForQuantize)
        {
            Quantize();
            _quantizeFired = true;
        }

        // Show greens whenever engaged AND we haven't snapped yet. Once snapped, the
        // visuals collapse — that's the "locked, hands off" signal.
        bool active = f >= EngageThreshold && !_quantizeFired;
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
