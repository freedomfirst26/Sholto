using Microsoft.EntityFrameworkCore;
using Sholto.Analysis;
using Sholto.Storage.Entities;

namespace Sholto.Storage;

/// <summary>SQLite-backed cache for BasicAnalysis. Survives app restarts.
/// Invalidates by comparing the stored FileMtime against the on-disk mtime.</summary>
public sealed class BasicAnalysisCache : IAnalysisCache
{
    private readonly IDbContextFactory<SholtoDbContext> _factory;
    public string Name => "database";

    public BasicAnalysisCache(IDbContextFactory<SholtoDbContext> factory) { _factory = factory; }

    public async Task<BasicAnalysis?> TryGetAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        long mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath)).ToUnixTimeSeconds();

        await using var db = _factory.CreateDbContext();
        var row = await db.BasicAnalyses
            .AsNoTracking()
            .Where(b => b.Track.Path == filePath && b.FileMtime == mtime)
            .Select(b => b.Data)
            .FirstOrDefaultAsync();
        return row is null ? null : AnalysisCodec.Decode(row);
    }

    public async Task PutAsync(string filePath, BasicAnalysis analysis)
    {
        if (!File.Exists(filePath)) return;
        long mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath)).ToUnixTimeSeconds();
        var blob = AnalysisCodec.Encode(analysis);

        await using var db = _factory.CreateDbContext();
        var track = await db.Tracks.FirstOrDefaultAsync(t => t.Path == filePath);
        if (track is null) return;

        var existing = await db.BasicAnalyses.FindAsync(track.Id);
        if (existing is null)
        {
            db.BasicAnalyses.Add(new BasicAnalysisRecord
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
        Console.WriteLine($"[DB] saved basic analysis ({blob.Length} bytes) for {Path.GetFileName(filePath)}");
    }
}
