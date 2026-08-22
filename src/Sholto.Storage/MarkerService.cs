using Microsoft.EntityFrameworkCore;
using Sholto.Storage.Entities;

namespace Sholto.Storage;

/// <summary>CRUD for <see cref="Marker"/>s (Rekordbox-style memory cues) and their
/// cross-deck <see cref="MarkerLink"/>s. Short-lived contexts per call.</summary>
public sealed class MarkerService
{
    private readonly IDbContextFactory<SholtoDbContext> _factory;

    public MarkerService(IDbContextFactory<SholtoDbContext> factory) => _factory = factory;

    /// <summary>Drop a marker on a track at a position; returns its id.</summary>
    public async Task<int> AddAsync(Guid trackId, double positionSecs,
        MarkerKind kind = MarkerKind.Memory, string? label = null)
    {
        await using var db = _factory.CreateDbContext();
        var m = new Marker
        {
            TrackId = trackId,
            PositionSecs = positionSecs,
            Kind = kind,
            Label = label,
            CreatedAt = DateTime.UtcNow,
        };
        db.Markers.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    /// <summary>Markers on a track, earliest position first.</summary>
    public async Task<IReadOnlyList<Marker>> ListForTrackAsync(Guid trackId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Markers
            .AsNoTracking()
            .Where(m => m.TrackId == trackId)
            .OrderBy(m => m.PositionSecs)
            .ToListAsync();
    }

    public async Task RemoveAsync(int markerId)
    {
        await using var db = _factory.CreateDbContext();
        var m = await db.Markers.FindAsync(markerId);
        if (m is null) return;
        db.Markers.Remove(m);
        await db.SaveChangesAsync();
    }

    /// <summary>Link an OUT marker to an IN marker with a transition; returns link id.</summary>
    public async Task<int> LinkAsync(int fromMarkerId, int toMarkerId,
        TransitionType transition = TransitionType.Crossfade, double amount = 16)
    {
        await using var db = _factory.CreateDbContext();
        var link = new MarkerLink
        {
            FromMarkerId = fromMarkerId,
            ToMarkerId = toMarkerId,
            Transition = transition,
            Amount = amount,
            CreatedAt = DateTime.UtcNow,
        };
        db.MarkerLinks.Add(link);
        await db.SaveChangesAsync();
        return link.Id;
    }
}
