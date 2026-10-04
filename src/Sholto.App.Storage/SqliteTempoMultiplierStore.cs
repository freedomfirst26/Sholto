using Microsoft.EntityFrameworkCore;
using Sholto.App.Analysis.Stores;
using Sholto.Storage.Entities;

namespace Sholto.App.Storage;

/// <summary>Tempo multipliers from the <c>BpmOverrides</c> table.</summary>
internal sealed class SqliteTempoMultiplierStore(IDbContextFactory<SholtoDbContext> factory) : ITempoMultiplierStore
{
    private readonly IDbContextFactory<SholtoDbContext> _factory = factory;

    public async Task<IReadOnlyDictionary<string, double>> GetAllAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.BpmOverrides
            .AsNoTracking()
            .Select(o => new { o.Track.Path, o.Multiplier })
            .ToDictionaryAsync(x => x.Path, x => x.Multiplier);
    }

    public async Task PutAsync(string path, double multiplier)
    {
        await using var db = _factory.CreateDbContext();
        var track = await db.Tracks.FirstOrDefaultAsync(t => t.Path == path);
        if (track is null) return;
        var existing = await db.BpmOverrides.FindAsync(track.Id);
        if (Math.Abs(multiplier - 1.0) < 0.0001)
        {
            if (existing is not null) db.BpmOverrides.Remove(existing);
        }
        else if (existing is null)
        {
            db.BpmOverrides.Add(new BpmOverride { TrackId = track.Id, Multiplier = multiplier });
        }
        else
        {
            existing.Multiplier = multiplier;
        }
        await db.SaveChangesAsync();
    }
}
