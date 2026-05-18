using OpenDJ.Analysis;

namespace OpenDJ.Storage;

/// <summary>SQLite-backed cache. Survives app restarts; invalidates when file mtime changes.</summary>
public sealed class DatabaseAnalysisCache : IAnalysisCache
{
    private readonly OpenDjDatabase _db;
    public string Name => "database";

    public DatabaseAnalysisCache(OpenDjDatabase db) { _db = db; }

    public Task<BasicAnalysis?> TryGetAsync(string filePath) => _db.GetBasicAnalysisAsync(filePath);

    public Task PutAsync(string filePath, BasicAnalysis analysis) => _db.SaveBasicAnalysisAsync(filePath, analysis);
}
