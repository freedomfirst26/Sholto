using Sholto.Analysis;

namespace Sholto.Storage;

/// <summary>SQLite-backed cache. Survives app restarts; invalidates when file mtime changes.</summary>
public sealed class DatabaseAnalysisCache : IAnalysisCache
{
    private readonly SholtoDatabase _db;
    public string Name => "database";

    public DatabaseAnalysisCache(SholtoDatabase db) { _db = db; }

    public Task<BasicAnalysis?> TryGetAsync(string filePath) => _db.GetBasicAnalysisAsync(filePath);

    public Task PutAsync(string filePath, BasicAnalysis analysis) => _db.SaveBasicAnalysisAsync(filePath, analysis);
}
