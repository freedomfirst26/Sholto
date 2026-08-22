using Microsoft.EntityFrameworkCore;
using Sholto.Storage.Entities;

namespace Sholto.Storage;

/// <summary>SQLite-backed store for per-track manual beatgrid corrections
/// (BPM override + phase offset). Tiny rows, keyed by track path. A missing
/// row = the track has never been hand-tuned.</summary>
public sealed class GridAdjustmentCache
{
    private readonly IDbContextFactory<SholtoDbContext> _factory;

    public GridAdjustmentCache(IDbContextFactory<SholtoDbContext> factory) { _factory = factory; }

    /// <summary>Returns (bpmOverride, offsetSec) for the track, or null if no
    /// adjustment has been saved.</summary>
    public async Task<(double? BpmOverride, double OffsetSec)?> TryGetAsync(string filePath)
    {
        await using var db = _factory.CreateDbContext();
        var row = await db.GridAdjustments
            .AsNoTracking()
            .Where(g => g.Track.Path == filePath)
            .Select(g => new { g.BpmOverride, g.OffsetSec })
            .FirstOrDefaultAsync();
        return row is null ? null : (row.BpmOverride, row.OffsetSec);
    }

    /// <summary>Upsert the adjustment for a track. Pass bpmOverride=null to mean
    /// "use detected BPM" while still recording a phase offset.</summary>
    public async Task PutAsync(string filePath, double? bpmOverride, double offsetSec)
    {
        await using var db = _factory.CreateDbContext();
        var track = await db.Tracks.FirstOrDefaultAsync(t => t.Path == filePath);
        if (track is null) return;

        var existing = await db.GridAdjustments.FindAsync(track.Id);
        if (existing is null)
        {
            db.GridAdjustments.Add(new GridAdjustment
            {
                TrackId = track.Id,
                BpmOverride = bpmOverride,
                OffsetSec = offsetSec,
            });
        }
        else
        {
            existing.BpmOverride = bpmOverride;
            existing.OffsetSec = offsetSec;
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Remove any saved adjustment for the track — resets its grid to
    /// the madmom detection on next load.</summary>
    public async Task ClearAsync(string filePath)
    {
        await using var db = _factory.CreateDbContext();
        var track = await db.Tracks.FirstOrDefaultAsync(t => t.Path == filePath);
        if (track is null) return;
        var existing = await db.GridAdjustments.FindAsync(track.Id);
        if (existing is not null)
        {
            db.GridAdjustments.Remove(existing);
            await db.SaveChangesAsync();
        }
    }
}
