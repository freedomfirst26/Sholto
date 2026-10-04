namespace Sholto.App.Analysis.Stages;

/// <summary>
/// A stage runs several analyzers and/or steps in a fixed order and returns their
/// combined <typeparamref name="TResult"/>. A stage is not itself an analyzer
/// (see <see cref="Analyzers.IAnalyzer"/>), which computes one result.
/// </summary>
public interface IAnalysisStage<TResult>
{
    /// <summary>Whether this stage can run on this machine. Callers skip it when false.</summary>
    bool IsAvailable { get; }

    /// <summary>Run the stage on a decoded track.</summary>
    Task<TResult> RunAsync(DecodedTrack track, CancellationToken ct = default);
}
