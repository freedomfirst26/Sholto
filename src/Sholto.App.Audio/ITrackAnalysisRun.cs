using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;

namespace Sholto.App.Audio;

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

    /// <summary>Run the full analysis pipeline for the in-memory <see cref="TrackLoading.Load"/>
    /// path: basic (BPM/beats) and key analysis concurrently, deck plays immediately and the
    /// beat grid/key appear when that lands; only THEN does stem separation start. The stems
    /// phase is sequenced after, not because it depends on the basic/key results (it doesn't —
    /// demucs opens the file itself), but because demucs saturates every core for 30-180s and
    /// would otherwise compete with madmom/key analysis exactly while the user is waiting for
    /// a beatgrid. See <c>TrackAnalysisRun.KickOffAnalysis</c> for the deliberate-scheduling
    /// comment — do not reorder this back to parallel.</summary>
    void KickOffAnalysis(DecodedTrack track);

    /// <summary>Raised on the analysis thread once an analysis stage completes.
    /// Relayed by <see cref="TrackLoading"/> exactly the way <c>TrackLoading</c>'s
    /// own <c>AnalysisUpdated</c> is relayed by <see cref="Deck"/>.</summary>
    event Action? AnalysisUpdated;
}
