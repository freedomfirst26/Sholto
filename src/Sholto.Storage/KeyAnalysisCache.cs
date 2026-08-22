using Microsoft.EntityFrameworkCore;
using Sholto.Analysis;
using Sholto.Storage.Entities;

namespace Sholto.Storage;

/// <summary>SQLite-backed cache for KeyAnalysis. Same shape as
/// <see cref="BasicAnalysisCache"/>, different codec + table.</summary>
public sealed class KeyAnalysisCache
{
    private readonly IDbContextFactory<SholtoDbContext> _factory;

    public KeyAnalysisCache(IDbContextFactory<SholtoDbContext> factory) { _factory = factory; }

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
        return row is null ? null : KeyAnalysisCodec.Decode(row);
    }

    public async Task PutAsync(string filePath, KeyAnalysis key)
    {
        if (!File.Exists(filePath)) return;
        long mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath)).ToUnixTimeSeconds();
        var blob = KeyAnalysisCodec.Encode(key);

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
        Console.WriteLine($"[DB] saved key {key.Camelot} for {Path.GetFileName(filePath)}");
    }
}
