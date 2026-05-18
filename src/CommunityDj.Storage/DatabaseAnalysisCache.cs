using CommunityDj.Analysis;

namespace CommunityDj.Storage;

/// <summary>SQLite-backed cache. Survives app restarts; invalidates when file mtime changes.</summary>
public sealed class DatabaseAnalysisCache : IAnalysisCache
{
    private readonly CommunityDjDatabase _db;
    public string Name => "database";

    public DatabaseAnalysisCache(CommunityDjDatabase db) { _db = db; }

    public Task<BasicAnalysis?> TryGetAsync(string filePath) => _db.GetBasicAnalysisAsync(filePath);

    public Task PutAsync(string filePath, BasicAnalysis analysis) => _db.SaveBasicAnalysisAsync(filePath, analysis);
}
