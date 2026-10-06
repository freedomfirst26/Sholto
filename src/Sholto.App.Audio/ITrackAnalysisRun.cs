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
    /// exactly as it did when this field lived directly on <c>TrackLoading</c>.
    /// Assigning it supersedes the run in flight: that run is cancelled (its madmom/demucs
    /// process is killed) and any result it still delivers is dropped.</summary>
    TrackAnalysis Analysis { get; set; }

    /// <summary>Run BPM + key analysis on this track in the background. Decodes
    /// the file once and feeds both pipelines, then drops the decoded buffer.
    /// Called by <see cref="TrackLoading.LoadStreaming"/>; safe to call
    /// repeatedly (each invocation gets its own captured filePath).</summary>
    void KickOffAnalysisFor(string filePath);

    /// <summary>Start the path-only part of analysis while the file is still decoding: the basic
    /// cache lookup (and, on a miss, the beat tracker) and, when stems may overlap basic analysis,
    /// the stem run. The prestart belongs to the current generation; the next <see cref="Analysis"/>
    /// assignment cancels it. Nothing it produces is applied until <see cref="KickOffAnalysis"/>
    /// for the same path adopts it. App thread.</summary>
    void Prestart(string filePath);

    /// <summary>Whether a live prestart for <paramref name="filePath"/> is waiting to be adopted.</summary>
    bool HasPrestartFor(string filePath);

    /// <summary>Run the full analysis pipeline for the in-memory <see cref="TrackLoading.Load"/>
    /// path. Adopts the prestart for the same path if one is waiting. Basic (BPM/beats) and key
    /// analysis run concurrently, so the deck plays immediately and the beat grid/key appear when
    /// that lands. Stems overlap them on CUDA and otherwise follow basic, because demucs on the CPU
    /// saturates every core for 30-180s and would starve madmom while the user waits for a
    /// beatgrid. See <c>TrackAnalysisRun.KickOffAnalysis</c>.</summary>
    void KickOffAnalysis(DecodedTrack track);

    /// <summary>Raised on the app thread once an analysis stage completes.
    /// Relayed by <see cref="TrackLoading"/> exactly the way <c>TrackLoading</c>'s
    /// own <c>AnalysisUpdated</c> is relayed by <see cref="Deck"/>.</summary>
    event Action? AnalysisUpdated;
}
