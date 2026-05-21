using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Sholto.Analysis;
using Sholto.App.Theming;
using Sholto.Library;

namespace Sholto.App.ViewModels;

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

    public TrackRow(Track track) { Track = track; }

    private double? _bpm;
    /// <summary>Raw BPM as detected by madmom (or null pre-analysis).</summary>
    public double? Bpm
    {
        get => _bpm;
        set { if (Math.Abs((_bpm ?? -1) - (value ?? -1)) < 0.05) return; _bpm = value; Notify(); Notify(nameof(BpmDisplay)); Notify(nameof(Analyzed)); }
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
        set { if (_stemsReady == value) return; _stemsReady = value; Notify(); Notify(nameof(Analyzed)); }
    }

    private bool _isAnalyzing;
    /// <summary>True while any analysis step is running for this track. Drives the
    /// spinner in the ANALYZED column.</summary>
    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        set { if (_isAnalyzing == value) return; _isAnalyzing = value; Notify(); }
    }

    /// <summary>Fully analyzed: basic (BPM + beats) AND stems on disk.
    /// Drives the gray checkmark in the ANALYZED column.</summary>
    public bool Analyzed => _bpm is not null && _stemsReady;

    private string? _key;
    public string? Key
    {
        get => _key;
        set { if (_key == value) return; _key = value; Notify(); Notify(nameof(HarmonyOpacity)); Notify(nameof(KeyBrush)); Notify(nameof(KeyEligible)); }
    }

    /// <summary>Camelot key chip background — same hue convention DJ apps use so
    /// the eye can scan keys without reading the codes. Hue/saturation/lightness
    /// come from the active theme's <see cref="CamelotPalette"/>, so switching
    /// theme retones the whole library at once.</summary>
    public IBrush KeyBrush
    {
        get
        {
            if (string.IsNullOrEmpty(_key)) return Brushes.Transparent;
            var p = ThemeContext.Current.CamelotPalette;
            uint rgb = CamelotKeys.Rgb(_key!, p.HueOffset, p.Saturation, p.MajorLightness, p.MinorLightness);
            return new SolidColorBrush(unchecked((uint)0xFF000000 | rgb));
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
            : CamelotKeys.Compatibility(_referenceKey!, _key!) switch
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
            return CamelotKeys.Compatibility(_referenceKey!, _key!) != CamelotKeys.Harmony.Far;
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
        set { if (_isPlayed == value) return; _isPlayed = value; Notify(); }
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
