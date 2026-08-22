using Microsoft.EntityFrameworkCore;
using Sholto.Storage.Entities;

namespace Sholto.Storage;

/// <summary>A crate and its current track count — for list/search UIs.</summary>
public sealed record CrateSummary(int Id, string Name, int TrackCount);

/// <summary>CRUD for <see cref="Crate"/>s and their track membership. Short-lived
/// contexts per call, mirroring <see cref="TagService"/>.</summary>
public sealed class CrateService
{
    private readonly IDbContextFactory<SholtoDbContext> _factory;

    public CrateService(IDbContextFactory<SholtoDbContext> factory) => _factory = factory;

    /// <summary>Create a crate (or return the existing one with that name, case-
    /// insensitive). Returns the crate id.</summary>
    public async Task<int> CreateAsync(string name)
    {
        name = name.Trim();
        await using var db = _factory.CreateDbContext();
        var existing = await db.Crates.FirstOrDefaultAsync(c => c.Name == name);
        if (existing is not null) return existing.Id;

        var crate = new Crate { Name = name, CreatedAt = DateTime.UtcNow };
        db.Crates.Add(crate);
        await db.SaveChangesAsync();
        return crate.Id;
    }

    /// <summary>All crates with their track counts, newest first.</summary>
    public async Task<IReadOnlyList<CrateSummary>> ListAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Crates
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CrateSummary(c.Id, c.Name, c.CrateTracks.Count))
            .ToListAsync();
    }

    /// <summary>Crates whose name contains <paramref name="query"/> (case-insensitive).</summary>
    public async Task<IReadOnlyList<CrateSummary>> SearchAsync(string query)
    {
        query = query.Trim();
        if (query.Length == 0) return await ListAsync();
        await using var db = _factory.CreateDbContext();
        return await db.Crates
            .Where(c => EF.Functions.Like(c.Name, $"%{query}%"))
            .OrderBy(c => c.Name)
            .Select(c => new CrateSummary(c.Id, c.Name, c.CrateTracks.Count))
            .ToListAsync();
    }

    /// <summary>Append a track to a crate at the next position. No-op if already in
    /// the crate. Returns true if it was added.</summary>
    public async Task<bool> AddTrackAsync(int crateId, Guid trackId)
    {
        await using var db = _factory.CreateDbContext();
        bool present = await db.CrateTracks.AnyAsync(x => x.CrateId == crateId && x.TrackId == trackId);
        if (present) return false;

        int nextPos = await db.CrateTracks.Where(x => x.CrateId == crateId)
            .Select(x => (int?)x.Position).MaxAsync() + 1 ?? 0;
        db.CrateTracks.Add(new CrateTrack { CrateId = crateId, TrackId = trackId, Position = nextPos });
        await db.SaveChangesAsync();
        return true;
    }

    public async Task RemoveTrackAsync(int crateId, Guid trackId)
    {
        await using var db = _factory.CreateDbContext();
        var row = await db.CrateTracks.FindAsync(crateId, trackId);
        if (row is null) return;
        db.CrateTracks.Remove(row);
        await db.SaveChangesAsync();
    }

    /// <summary>Track ids in a crate, in stored order.</summary>
    public async Task<IReadOnlyList<Guid>> TrackIdsAsync(int crateId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.CrateTracks
            .Where(x => x.CrateId == crateId)
            .OrderBy(x => x.Position)
            .Select(x => x.TrackId)
            .ToListAsync();
    }
}
