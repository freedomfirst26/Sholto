using Sholto.Analysis.Processing;

namespace Sholto.Analysis;

/// <summary>
/// Beat / downbeat detection for a track. Role-named rather than tool-named: the only
/// implementation today is <see cref="MadmomBeatAnalysisStep"/> (madmom's
/// DBNDownBeatTracker), but a fake for tests, a cached decorator, or a different beat
/// tracker entirely should all be substitutable through this without the name lying.
/// Required — callers have no fallback if this fails or isn't installed.
/// </summary>
public interface IBeatAnalysisStep : IAnalysisStep
{
    /// <summary>Analyse the file and return its raw beat/downbeat detections. No
    /// reporter — this is required and has no UI-visible step of its own; a failure
    /// here throws and is surfaced by the caller.</summary>
    Task<DetectedBeats> AnalyzeAsync(
        string filePath, CancellationToken ct = default);
}
