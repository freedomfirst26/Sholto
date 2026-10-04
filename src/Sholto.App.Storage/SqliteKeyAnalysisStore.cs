using Microsoft.EntityFrameworkCore;
using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Stores;
using Sholto.Storage.Entities;

namespace Sholto.App.Storage;

/// <summary>SQLite-backed store for KeyAnalysis — the system of record, not a
/// cache; see <see cref="IKeyAnalysisStore"/>. Same shape as
/// <see cref="BasicAnalysisStore"/>, different serializer + table.</summary>
internal sealed class SqliteKeyAnalysisStore(IDbContextFactory<SholtoDbContext> factory, IKeyAnalysisSerializer serializer) : IKeyAnalysisStore
{
    private readonly IDbContextFactory<SholtoDbContext> _factory = factory;

    private readonly IKeyAnalysisSerializer _serializer = serializer;

    public async Task<KeyAnalysis?> TryGetAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        long mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath)).ToUnixTimeSeconds();

        await using var db = _factory.CreateDbContext();
        var row = await db.KeyAnalyses
            .AsNoTracking()
            .Where(k => k.Track.Path == filePath && k.FileMtime == mtime)
            .Select(k => k.Data)
            .FirstOrDefaultAsync();
        return row is null ? null : _serializer.Decode(row);
    }

    public async Task<IReadOnlyDictionary<string, KeyAnalysis>> GetAllAsync()
    {
        await using var db = _factory.CreateDbContext();
        var rows = await db.KeyAnalyses
            .AsNoTracking()
            .Select(k => new { k.Track.Path, k.Data })
            .ToListAsync();
        var result = new Dictionary<string, KeyAnalysis>(rows.Count);
        foreach (var r in rows)
        {
            var key = _serializer.Decode(r.Data);
            if (key is not null) result[r.Path] = key;
        }
        return result;
    }

    public async Task PutAsync(string filePath, KeyAnalysis key)
    {
        if (!File.Exists(filePath)) return;
        long mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath)).ToUnixTimeSeconds();
        var blob = _serializer.Encode(key);

        await using var db = _factory.CreateDbContext();
        var track = await db.Tracks.FirstOrDefaultAsync(t => t.Path == filePath);
        if (track is null) return;

        var existing = await db.KeyAnalyses.FindAsync(track.Id);
        if (existing is null)
        {
            db.KeyAnalyses.Add(new KeyAnalysisRecord
            {
                TrackId = track.Id,
                Data = blob,
                FileMtime = mtime,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Data = blob;
            existing.FileMtime = mtime;
            existing.CreatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        Console.WriteLine($"[DB] saved key {key.Key?.ToCamelot()} for {Path.GetFileName(filePath)}");
    }
}
