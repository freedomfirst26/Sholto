using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Sholto.Analysis;
using Sholto.App.Theming;
using Sholto.Music;

namespace Sholto.App.ViewModels;

/// <summary>The single, mutually-exclusive analysis state of a track row — one
/// decoration in the ANALYZED column, never two.</summary>
public enum TrackAnalysisState
{
    /// <summary>Nothing computed yet.</summary>
    Unanalyzed,
    /// <summary>An analysis step is currently running for this track.</summary>
    Analyzing,
    /// <summary>BPM/beats + stems are done.</summary>
    Analyzed,
    /// <summary>An analysis step reported a failure and the track did not finish.
    /// Distinct from <see cref="Unanalyzed"/> on purpose: a broken external analyser
    /// used to look exactly like a track nobody had got round to yet.</summary>
    Failed,
}

/// <summary>
/// One row in the track list. Wraps a Track plus the slow-to-compute fields
/// (BPM, key, etc.) so the row can "uplift" as analyses arrive.
/// </summary>
public sealed class TrackRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public Track Track { get; }
    public string FilePath => Track.FilePath;
    public string Title => Track.Title;
    public string Artist => Track.Artist;
    public TimeSpan Duration => Track.Duration;

    private readonly IThemeContext _theme;
    private readonly IHarmonicKeys _harmonicKeys;

    public TrackRow(Track track, IThemeContext theme, IHarmonicKeys harmonicKeys)
    {
        Track = track;
        _theme = theme;
        _harmonicKeys = harmonicKeys;
    }

    private double? _bpm;
    /// <summary>Raw BPM as detected by madmom (or null pre-analysis).</summary>
    public double? Bpm
    {
        get => _bpm;
        set { if (Math.Abs((_bpm ?? -1) - (value ?? -1)) < 0.05) return; _bpm = value; Notify(); Notify(nameof(BpmDisplay)); NotifyAnalysisState(); }
    }

    private double _bpmMultiplier = 1.0;
    /// <summary>User-applied half/double override. 0.5 corrects a doubled madmom
    /// estimate; 2.0 corrects a halved one. Persisted per-file.</summary>
    public double BpmMultiplier
    {
        get => _bpmMultiplier;
        set { if (Math.Abs(_bpmMultiplier - value) < 0.0001) return; _bpmMultiplier = value; Notify(); Notify(nameof(BpmDisplay)); }
    }

    /// <summary>BPM after the user override — what we show in the library list.</summary>
    public string BpmDisplay => _bpm is { } b ? $"{(b * _bpmMultiplier):F1}" : "";

    private bool _stemsReady;
    /// <summary>True once Demucs has produced the 4 stem WAVs for this track (this session,
    /// or — once we wire startup-scan — pre-existing on disk).</summary>
    public bool StemsReady
    {
        get => _stemsReady;
        set { if (_stemsReady == value) return; _stemsReady = value; Notify(); NotifyAnalysisState(); }
    }

    private bool _isAnalyzing;
    /// <summary>True while any analysis step is running for this track. Drives the
    /// spinner in the ANALYZED column.</summary>
    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        set { if (_isAnalyzing == value) return; _isAnalyzing = value; Notify(); NotifyAnalysisState(); }
    }

    private string? _analysisFailure;
    /// <summary>The failure text from the reporter (step name + the analyser's own
    /// output tail), or null if nothing has failed. Set by MainViewModel from
    /// <see cref="AnalysisReport.FailureMessage"/>.</summary>
    public string? AnalysisFailure
    {
        get => _analysisFailure;
        set
        {
            if (_analysisFailure == value) return;
            _analysisFailure = value;
            Notify();
            Notify(nameof(AnalysisFailureTooltip));
            NotifyAnalysisState();
        }
    }

    /// <summary>Tooltip behind the failure marker.</summary>
    public string AnalysisFailureTooltip =>
        string.IsNullOrEmpty(_analysisFailure) ? "" : "Analysis failed\n" + _analysisFailure;

    private bool _hasRequiredFailure;
    /// <summary>True when <see cref="AnalysisFailure"/> includes a failure of a
    /// REQUIRED step (currently just beat/BPM detection — see
    /// <see cref="Sholto.Analysis.AnalysisReport.HasRequiredFailure"/>). Set by
    /// MainViewModel alongside <see cref="AnalysisFailure"/>. Distinct from a plain
    /// non-empty <see cref="AnalysisFailure"/> because that also covers OPTIONAL
    /// step failures (stems, segments), which must not override a green tick — a
    /// required failure must.</summary>
    public bool HasRequiredFailure
    {
        get => _hasRequiredFailure;
        set
        {
            if (_hasRequiredFailure == value) return;
            _hasRequiredFailure = value;
            Notify();
            NotifyAnalysisState();
        }
    }

    private void NotifyAnalysisState()
    {
        Notify(nameof(Analyzed));
        Notify(nameof(AnalysisState));
        Notify(nameof(ShowAnalyzingSpinner));
        Notify(nameof(ShowAnalyzedCheck));
        Notify(nameof(ShowAnalysisFailed));
    }

    /// <summary>Fully analyzed: basic (BPM + beats) AND stems on disk.</summary>
    public bool Analyzed => _bpm is not null && _stemsReady;

    /// <summary>The single source of truth for the ANALYZED column. Actively running
    /// wins over "done" so we never render a spinner and a checkmark together — that
    /// overlap (reporter still busy on a later step while BPM+stems already landed)
    /// was the double-decoration bug.</summary>
    /// <para>An OPTIONAL step's failure ranks below Analyzed: it can
    /// fail on a track whose BPM and stems both landed, and that track really is
    /// analysed — the marker appears only when something is genuinely missing.
    /// A REQUIRED step's failure (<see cref="HasRequiredFailure"/>) ranks ABOVE
    /// Analyzed: a re-analysis with a broken beat tracker must not keep showing the
    /// green tick just because a previous successful run left BPM + stems cached —
    /// the beatgrid the tick promises did not actually get regenerated.</para>
    public TrackAnalysisState AnalysisState =>
        _isAnalyzing                         ? TrackAnalysisState.Analyzing
      : _hasRequiredFailure                  ? TrackAnalysisState.Failed
      : Analyzed                             ? TrackAnalysisState.Analyzed
      : !string.IsNullOrEmpty(_analysisFailure) ? TrackAnalysisState.Failed
      :                                        TrackAnalysisState.Unanalyzed;

    // View helpers — exactly one is ever true (compiled bindings are off, so the
    // XAML binds these bools rather than comparing the enum).
    public bool ShowAnalyzingSpinner => AnalysisState == TrackAnalysisState.Analyzing;
    public bool ShowAnalyzedCheck    => AnalysisState == TrackAnalysisState.Analyzed;
    public bool ShowAnalysisFailed   => AnalysisState == TrackAnalysisState.Failed;

    private string? _key;
    public string? Key
    {
        get => _key;
        set { if (_key == value) return; _key = value; Notify(); Notify(nameof(HarmonyOpacity)); Notify(nameof(KeyBrush)); Notify(nameof(KeyEligible)); }
    }

    /// <summary>Camelot key chip background — same hue convention DJ apps use so
    /// the eye can scan keys without reading the codes. Hue/saturation/lightness
    /// come from the active theme's <see cref="CamelotPalette"/>, so switching
    /// theme retones the whole library at once.
    /// <para>Played tracks render the chip with ~30 % of the normal saturation
    /// so the eye is drawn to the still-unplayed rows. Combined with the
    /// italic+bold title styling, this makes "already in the set" rows visibly
    /// recede without hiding them.</para></summary>
    public IBrush KeyBrush
    {
        get
        {
            if (string.IsNullOrEmpty(_key)) return Brushes.Transparent;
            var p = _theme.Current.CamelotPalette;
            return p.KeyBrush(_key!, _harmonicKeys, saturationScale: _isPlayed ? 0.3 : 1.0);
        }
    }

    /// <summary>Refresh theme-derived properties after a theme switch. Called by
    /// <see cref="MainViewModel"/> when the active theme changes so we don't have
    /// to subscribe to a static event (which would pin TrackRow instances).</summary>
    public void RefreshThemeBindings() => Notify(nameof(KeyBrush));

    private string? _referenceKey;
    /// <summary>The active deck's Camelot key (or null/empty). When set, every
    /// row recomputes <see cref="HarmonyOpacity"/> so the library lights up the
    /// tracks that mix harmonically with what's currently playing.</summary>
    public string? ReferenceKey
    {
        get => _referenceKey;
        set
        {
            if (_referenceKey == value) return;
            _referenceKey = value;
            Notify();
            Notify(nameof(HarmonyOpacity));
            Notify(nameof(KeyEligible));
        }
    }

    /// <summary>Opacity to apply to the whole row based on Camelot compatibility
    /// with <see cref="ReferenceKey"/>. Kept for any callers that still want a
    /// fade-style signal; the library list now uses <see cref="KeyEligible"/>
    /// to highlight eligible chips with an outline instead of fading the rest.</summary>
    public double HarmonyOpacity =>
        (string.IsNullOrEmpty(_referenceKey) || string.IsNullOrEmpty(_key))
            ? 1.0
            : _harmonicKeys.Compatibility(_referenceKey!, _key!) switch
            {
                CamelotKeys.Harmony.Perfect     => 1.00,
                CamelotKeys.Harmony.Close       => 0.85,
                CamelotKeys.Harmony.EnergyBoost => 0.65,
                _                               => 0.30,
            };

    /// <summary>True when this track's key mixes harmonically with the active
    /// deck's key (Perfect / Close / EnergyBoost — anything but Far). Used to
    /// draw a primary-colour outline around eligible key chips so the user can
    /// scan the list for mix candidates without dimming everything else.</summary>
    public bool KeyEligible
    {
        get
        {
            if (string.IsNullOrEmpty(_referenceKey) || string.IsNullOrEmpty(_key)) return false;
            return _harmonicKeys.Compatibility(_referenceKey!, _key!) != CamelotKeys.Harmony.Far;
        }
    }

    public string DurationDisplay => $"{(int)Duration.TotalMinutes:00}:{Duration.Seconds:00}";

    private bool _isPlayed;
    /// <summary>True once this track has been loaded into a deck during the
    /// current session. Drives an italic artist/title in the library so the
    /// user can see at a glance what they've already touched. Reset only by
    /// app restart — the Session that owns the truth is process-scoped.</summary>
    public bool IsPlayed
    {
        get => _isPlayed;
        set
        {
            if (_isPlayed == value) return;
            _isPlayed = value;
            Notify();
            // KeyBrush desaturates when IsPlayed flips — fire so the chip
            // restyles immediately instead of waiting for the next theme switch.
            Notify(nameof(KeyBrush));
        }
    }

    private Guid _trackId;
    public Guid TrackId
    {
        get => _trackId;
        set { if (_trackId == value) return; _trackId = value; Notify(); }
    }

    private int _tagCount;
    public int TagCount
    {
        get => _tagCount;
        set
        {
            if (_tagCount == value) return;
            _tagCount = value;
            Notify();
            Notify(nameof(HasTags));
        }
    }

    public bool HasTags => _tagCount > 0;

    private IReadOnlyList<string> _tags = Array.Empty<string>();
    public IReadOnlyList<string> Tags
    {
        get => _tags;
        set
        {
            _tags = value ?? Array.Empty<string>();
            Notify();
            TagCount = _tags.Count;
            Notify(nameof(TagsTooltip));
        }
    }

    public string TagsTooltip => _tags.Count == 0 ? "" : string.Join(", ", _tags);

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
