namespace Sholto.Analysis;

/// <summary>
/// Read-write store for one analysis type, keyed by file path. Implementations:
///   - <see cref="MemoryAnalysisCache"/>  — in-process, fastest, lost on app exit
///   - <see cref="DatabaseAnalysisCache"/> — SQLite, survives restart
/// More tiers can be plugged in (network cache, S3, …) without changing callers.
/// </summary>
public interface IAnalysisCache
{
    /// <summary>Human label for logs ("memory", "database", ...).</summary>
    string Name { get; }

    /// <summary>Return the cached analysis for <paramref name="filePath"/> or null if missing.</summary>
    Task<BasicAnalysis?> TryGetAsync(string filePath);

    /// <summary>Persist the analysis. Lower tiers will pick it up on the next miss.</summary>
    Task PutAsync(string filePath, BasicAnalysis analysis);
}
