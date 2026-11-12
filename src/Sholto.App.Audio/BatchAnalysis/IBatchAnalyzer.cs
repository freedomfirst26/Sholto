namespace Sholto.App.Audio.BatchAnalysis;

/// <summary>Headless decode + beat + key analysis for a list of tracks, a bounded number at a time.
/// Stem separation and deck-load analysis are not part of it.</summary>
public interface IBatchAnalyzer
{
    /// <summary>
    /// Analyse every path, at most <see cref="BatchAnalysisOptions.MaxParallelTracks"/> at once.
    /// Each track reports <see cref="BatchTrackState.Started"/> and then exactly one of
    /// <see cref="BatchTrackState.Finished"/>, <see cref="BatchTrackState.Skipped"/> or
    /// <see cref="BatchTrackState.Failed"/>; one track failing does not stop the others.
    /// <paramref name="progress"/> is called from worker threads: the caller marshals.
    /// Cancelling <paramref name="ct"/> starts no further tracks, cancels the ones running (killing
    /// their beat-tracker processes) and ends the call with <see cref="OperationCanceledException"/>.
    /// </summary>
    Task RunAsync(IReadOnlyList<string> paths, IProgress<BatchTrackProgress> progress, CancellationToken ct);
}
