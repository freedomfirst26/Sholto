using Microsoft.EntityFrameworkCore;
using Sholto.App.Library.Markers;
using Sholto.App.Storage.Entities;
using Sholto.Data;

namespace Sholto.App.Storage;

/// <summary>Hot cues stored as <see cref="MarkerKind.Hot"/> <see cref="Marker"/>s with a pad slot and colour.
/// Short-lived contexts per call.</summary>
internal sealed class HotCueService(IDbContextFactory<SholtoDbContext> factory) : IHotCueStore
{
    private readonly IDbContextFactory<SholtoDbContext> _factory = factory;

    public async Task<IReadOnlyList<HotCue>> ListAsync(Guid trackId)
    {
        await using var db = _factory.CreateDbContext();
        var rows = await db.Markers
            .AsNoTracking()
            .Where(m => m.TrackId == trackId && m.Kind == MarkerKind.Hot && m.Slot != null)
            .OrderBy(m => m.Slot)
            .ToListAsync();
        return rows.Select(m => new HotCue(m.Slot!.Value, m.PositionSecs, (CueColour)(m.Colour ?? 0))).ToList();
    }

    public async Task SetAsync(Guid trackId, HotCue cue)
    {
        await using var db = _factory.CreateDbContext();
        var old = await db.Markers
            .Where(m => m.TrackId == trackId && m.Kind == MarkerKind.Hot && m.Slot == cue.Pad)
            .ToListAsync();
        db.Markers.RemoveRange(old);
        db.Markers.Add(new Marker
        {
            TrackId = trackId,
            PositionSecs = cue.PositionSec,
            Kind = MarkerKind.Hot,
            Slot = cue.Pad,
            Colour = (int)cue.Colour,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid trackId, int pad)
    {
        await using var db = _factory.CreateDbContext();
        var old = await db.Markers
            .Where(m => m.TrackId == trackId && m.Kind == MarkerKind.Hot && m.Slot == pad)
            .ToListAsync();
        if (old.Count == 0) return;
        db.Markers.RemoveRange(old);
        await db.SaveChangesAsync();
    }
}
