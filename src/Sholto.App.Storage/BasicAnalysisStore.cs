using Microsoft.EntityFrameworkCore;
using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Stores;
using Sholto.Storage.Entities;

namespace Sholto.App.Storage;

/// <summary>SQLite-backed store for BasicAnalysis. Survives app restarts.
/// Invalidates by comparing the stored FileMtime against the on-disk mtime.</summary>
internal sealed class BasicAnalysisStore(IDbContextFactory<SholtoDbContext> factory, IBasicAnalysisSerializer serializer) : IBasicAnalysisStore
{
    private readonly IDbContextFactory<SholtoDbContext> _factory = factory;
    private readonly IBasicAnalysisSerializer _serializer = serializer;
    public string Name => "database";

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
        return row is null ? null : _serializer.Decode(row);
    }

    public async Task PutAsync(string filePath, BasicAnalysis analysis)
    {
        if (!File.Exists(filePath)) return;
        long mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath)).ToUnixTimeSeconds();
        var blob = _serializer.Encode(analysis);

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

    public async Task<IReadOnlyDictionary<string, double>> GetDetectedBpmsAsync()
    {
        await using var db = _factory.CreateDbContext();
        var basicRows = await db.BasicAnalyses
            .AsNoTracking()
            .Select(b => new { b.Track.Path, b.Data })
            .ToListAsync();
        var bpms = new Dictionary<string, double>(basicRows.Count);
        foreach (var r in basicRows)
        {
            var basic = _serializer.Decode(r.Data);
            if (basic is not null) bpms[r.Path] = basic.Bpm;
        }
        return bpms;
    }
}
