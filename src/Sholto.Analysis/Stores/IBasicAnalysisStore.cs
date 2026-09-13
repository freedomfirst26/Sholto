using Sholto.Analysis.Analyzers;

namespace Sholto.Analysis.Stores;

/// <summary>
/// Persistence port for <see cref="BasicAnalysis"/>, keyed by file path. The only
/// implementation is the SQLite-backed store in <c>Sholto.Storage</c>; tiering
/// (in-process memory ahead of it, this store behind) is expressed by the
/// decorator chain <c>CachingAnalysisProvider(DbAnalysisProvider(AnalysisProvider))</c>,
/// not by anything on this interface.
/// </summary>
public interface IBasicAnalysisStore
{
    /// <summary>Human label for logs ("memory", "database", ...).</summary>
    string Name { get; }

    /// <summary>Return the stored analysis for <paramref name="filePath"/> or null if missing.</summary>
    Task<BasicAnalysis?> TryGetAsync(string filePath);

    /// <summary>Persist the analysis.</summary>
    Task PutAsync(string filePath, BasicAnalysis analysis);
}
