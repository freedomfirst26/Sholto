using Sholto.Analysis;
using Sholto.Analysis.Analyzers;
using Sholto.Analysis.Processing;
using Sholto.Analysis.Reporting;
using Sholto.Analysis.Stems;

namespace Sholto.Audio;

/// <summary>
/// Runs the BPM/key/stem analysis pipeline for the track currently on a deck.
/// Extracted out of <see cref="TrackLoading"/> — see <see cref="TrackAnalysisRun"/>
/// for the implementation and why <see cref="TrackLoading.SwitchToStemMode"/>
/// deliberately stays behind on <c>TrackLoading</c> rather than moving here too.
/// </summary>
internal interface ITrackAnalysisRun
{
    /// <summary>
    /// Layered analysis cache (memory → db → compute). Constructor-injected —
    /// never null, never half-built.
    /// </summary>
    IAnalysisProvider AnalysisProvider { get; }

    /// <summary>
    /// Shared reporter — receives waveform / beats / stems progress events.
    /// Constructor-injected, same reasoning as <see cref="AnalysisProvider"/>.
    /// </summary>
    IAnalysisReporter Reporter { get; }

    /// <summary>Stem separation (demucs by default). Optional; playback just stays on
    /// the mixed track when the tool isn't installed.</summary>
    IStemAnalysisStep StemAnalyzer { get; }

    /// <summary>The current track's live analysis (peaks, BPM, key, stems, …).
    /// Settable so <see cref="TrackLoading"/> can reassign a fresh instance on
    /// every <c>BeginLoad</c>/<c>Load</c>/<c>LoadStreaming</c>/<c>Unload</c> —
    /// exactly as it did when this field lived directly on <c>TrackLoading</c>.</summary>
    TrackAnalysis Analysis { get; set; }

    /// <summary>Run BPM + key analysis on this track in the background. Decodes
    /// the file once and feeds both pipelines, then drops the decoded buffer.
    /// Called by <see cref="TrackLoading.LoadStreaming"/>; safe to call
    /// repeatedly (each invocation gets its own captured filePath).</summary>
    void KickOffAnalysisFor(string filePath);

    /// <summary>Basic (BPM/beats) analysis for the in-memory <see cref="TrackLoading.Load"/>
    /// path. Deck plays immediately; the beat grid appears when this lands.</summary>
    void KickOffBasicAnalysis(DecodedTrack track);

    /// <summary>Key estimation for the in-memory <see cref="TrackLoading.Load"/> path.
    /// Independent of beats/stems — reads the same decoded buffer.</summary>
    void KickOffKeyAnalysis(DecodedTrack track);

    /// <summary>Stem separation for the in-memory <see cref="TrackLoading.Load"/> path.
    /// Slower and isolated from playback; on completion, auto-switches the deck to
    /// stem-mix playback via the <c>onStemsReady</c> callback handed in at construction.</summary>
    void KickOffStemAnalysis(string filePath);

    /// <summary>Per-stem waveform peaks + vocal-region analysis, run once stems have
    /// landed and <see cref="TrackLoading.SwitchToStemMode"/> has swapped in the
    /// stem-mix provider. Called by <c>SwitchToStemMode</c> with the four decoded
    /// stem buffers it just built.</summary>
    void KickOffStemPeaksAnalysis(StemSamples stems);

    /// <summary>Raised on the analysis thread once an analysis stage completes.
    /// Relayed by <see cref="TrackLoading"/> exactly the way <c>TrackLoading</c>'s
    /// own <c>AnalysisUpdated</c> is relayed by <see cref="Deck"/>.</summary>
    event Action? AnalysisUpdated;
}
