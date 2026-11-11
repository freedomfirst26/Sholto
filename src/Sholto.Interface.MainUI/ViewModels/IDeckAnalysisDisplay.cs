using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>What one deck shows from the loaded track's analysis: peaks, beats, key, and the band energy at
/// the playhead.</summary>
public interface IDeckAnalysisDisplay
{
    void Set(DeckAnalysis? analysis);

    /// <summary>The loaded track's analysis as of the last content event: an immutable snapshot, replaced
    /// each time an analysis step lands; null until the first content event.</summary>
    DeckAnalysis? Analysis { get; }

    /// <summary>Waveform peaks for rendering: always the mixed frequency-band peaks (low / mid / high) from
    /// basic analysis. The silhouette is the full mix and does NOT change when stems are muted: stem state
    /// is shown by the DRMS / VOX / INST chips and the audio, so the arrangement stays recognizable at a
    /// glance.</summary>
    WaveformPeaks Peaks { get; }

    /// <summary>Always the basic mixed peaks (or none until basic analysis lands). Used by
    /// <see cref="Controls.WaveformControl"/> as the time-mapping reference so the beatgrid keeps scrolling
    /// even when every stem is muted.</summary>
    WaveformPeaks GridPeaks { get; }

    /// <summary>Vocal-presence regions derived from the isolated vocal stem, or null until stems land.</summary>
    IReadOnlyList<VocalRegion>? VocalRegions { get; }

    double[] BeatTimes { get; }
    double[] DownbeatTimes { get; }
    bool HasAnalysis { get; }

    /// <summary>True once Demucs stems have landed; until then the stem chip row hides.</summary>
    bool HasStems { get; }

    bool HasKey { get; }

    /// <summary>Camelot code for the loaded track (e.g. "8B"), or empty if key analysis hasn't completed.</summary>
    string Camelot { get; }

    /// <summary>Avalonia brush coloured by the Camelot key: used to tint the deck's key chip so the same
    /// colour shows here and in the library row. Pulls hue/sat/lightness from the active theme's
    /// CamelotPalette so theme switches retone live.</summary>
    Avalonia.Media.IBrush KeyBrush { get; }

    /// <summary>Coarse frequency content at the playhead (0..1 along the track), straight from the band
    /// peaks: no FFT, no extra buffers. Allocates nothing.</summary>
    float LowAt(double playPosition);
    float MidAt(double playPosition);
    float HighAt(double playPosition);
}
