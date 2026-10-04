using Microsoft.EntityFrameworkCore;
using Sholto.App.Library.Catalog;
using EfTrack = Sholto.Storage.Entities.Track;
using LibTrack = Sholto.App.Library.Track;

namespace Sholto.App.Storage;

/// <summary>Track catalog backed by the <c>Tracks</c> table. Short-lived context per call.</summary>
internal sealed class SqliteTrackCatalog(IDbContextFactory<SholtoDbContext> factory) : ITrackCatalog
{
    private readonly IDbContextFactory<SholtoDbContext> _factory = factory;

    public async Task<TrackUpsertResult> UpsertAsync(IReadOnlyList<LibTrack> tracks)
    {
        await using var db = _factory.CreateDbContext();

        var newPaths = new List<string>();
        foreach (var t in tracks)
        {
            var info = new FileInfo(t.FilePath);
            if (!info.Exists) continue;
            long size = info.Length;
            long mtime = new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds();

            var existing = await db.Tracks.FirstOrDefaultAsync(x => x.Path == t.FilePath);
            if (existing is null)
            {
                newPaths.Add(t.FilePath);
                db.Tracks.Add(new EfTrack
                {
                    Path = t.FilePath,
                    Title = t.Title,
                    Artist = t.Artist,
                    FileSize = size,
                    FileMtime = mtime,
                    DurationSecs = t.Duration.TotalSeconds,
                });
            }
            else
            {
                existing.Title = t.Title;
                existing.Artist = t.Artist;
                existing.FileSize = size;
                existing.FileMtime = mtime;
                existing.DurationSecs = t.Duration.TotalSeconds;
            }
        }
        await db.SaveChangesAsync();

        var pathToId = await db.Tracks.AsNoTracking()
            .Select(t => new { t.Path, t.Id })
            .ToDictionaryAsync(x => x.Path, x => x.Id);

        var newIds = newPaths
            .Where(pathToId.ContainsKey)
            .Select(p => pathToId[p])
            .ToList();

        return new TrackUpsertResult(pathToId, newIds);
    }
}
