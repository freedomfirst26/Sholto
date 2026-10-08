namespace Sholto.App.Analysis.Stems;

/// <summary>Separates one track into stems on the shared demucs slot: waits its turn behind any other run
/// (every deck load and every re-analysis), and answers from the cache without running demucs when the track's
/// stems already exist. Reports progress, failure and cancellation on the analysis reporter as a deck load does.</summary>
public interface IStemSeparator
{
    /// <summary>False when demucs is not installed; a caller skips stems then.</summary>
    bool IsAvailable { get; }

    /// <summary>The track's stem files, once separated (or found cached). Throws on failure after reporting it;
    /// throws <see cref="OperationCanceledException"/> after reporting the step cancelled.</summary>
    Task<StemPaths> SeparateAsync(string filePath, CancellationToken ct = default);
}
