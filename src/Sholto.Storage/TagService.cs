using Microsoft.EntityFrameworkCore;
using Sholto.Storage.Entities;

namespace Sholto.Storage;

public enum AddTagOutcome
{
    Added,
    AlreadyPresent,
    RejectedEmpty,
    RejectedTooLong,
    RejectedLimitReached,
    RejectedTrackNotFound,
}

public sealed record AddTagResult(AddTagOutcome Outcome, string? StoredName);

public sealed record TagSearchHit(string Name, int TrackCount);

public sealed class TagsChangedEventArgs : EventArgs
{
    public Guid TrackId { get; }
    public int NewCount { get; }
    public TagsChangedEventArgs(Guid trackId, int newCount)
    {
        TrackId = trackId;
        NewCount = newCount;
    }
}

/// <summary>All tag mutations + lookups go through this service. UI never
/// touches <see cref="SholtoDbContext"/> for tag operations directly.</summary>
public sealed class TagService
{
    public const int MaxTagsPerTrack = 128;

    private readonly IDbContextFactory<SholtoDbContext> _factory;

    public TagService(IDbContextFactory<SholtoDbContext> factory)
    {
        _factory = factory;
    }

    public event EventHandler<TagsChangedEventArgs>? TagsChanged;

    public async Task<IReadOnlyList<string>> GetTagsForTrackAsync(Guid trackId, CancellationToken ct)
    {
        await using var db = _factory.CreateDbContext();
        return await db.TrackTags
            .AsNoTracking()
            .Where(tt => tt.TrackId == trackId)
            .OrderBy(tt => tt.Tag.Name)
            .Select(tt => tt.Tag.Name)
            .ToListAsync(ct);
    }

    public async Task<AddTagResult> AddTagAsync(Guid trackId, string raw, CancellationToken ct)
    {
        var normalized = TagNameNormalizer.Normalize(raw);
        if (normalized is null)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new AddTagResult(AddTagOutcome.RejectedEmpty, null);
            return new AddTagResult(AddTagOutcome.RejectedTooLong, null);
        }

        await using var db = _factory.CreateDbContext();

        // Guard against rows whose TrackId never got hydrated (e.g. path drift
        // between TrackScanner output and Tracks.Path). Without this the join
        // insert below blows up with a FOREIGN KEY constraint failed.
        bool trackExists = await db.Tracks.AnyAsync(t => t.Id == trackId, ct);
        if (!trackExists)
            return new AddTagResult(AddTagOutcome.RejectedTrackNotFound, null);

        int currentCount = await db.TrackTags.CountAsync(tt => tt.TrackId == trackId, ct);
        if (currentCount >= MaxTagsPerTrack)
            return new AddTagResult(AddTagOutcome.RejectedLimitReached, null);

        var existingTag = await db.Tags.FirstOrDefaultAsync(t => t.Name == normalized, ct);
        if (existingTag is null)
        {
            existingTag = new Tag { Name = normalized, CreatedAt = DateTime.UtcNow };
            db.Tags.Add(existingTag);
            await db.SaveChangesAsync(ct);
        }

        bool alreadyJoined = await db.TrackTags.AnyAsync(
            tt => tt.TrackId == trackId && tt.TagId == existingTag.Id, ct);
        if (alreadyJoined)
            return new AddTagResult(AddTagOutcome.AlreadyPresent, existingTag.Name);

        db.TrackTags.Add(new TrackTag { TrackId = trackId, TagId = existingTag.Id });
        await db.SaveChangesAsync(ct);

        TagsChanged?.Invoke(this, new TagsChangedEventArgs(trackId, currentCount + 1));
        return new AddTagResult(AddTagOutcome.Added, existingTag.Name);
    }

    public async Task RemoveTagAsync(Guid trackId, string name, CancellationToken ct)
    {
        var normalized = TagNameNormalizer.Normalize(name);
        if (normalized is null) return;

        await using var db = _factory.CreateDbContext();
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Name == normalized, ct);
        if (tag is null) return;

        var join = await db.TrackTags.FirstOrDefaultAsync(
            tt => tt.TrackId == trackId && tt.TagId == tag.Id, ct);
        if (join is null) return;

        db.TrackTags.Remove(join);
        await db.SaveChangesAsync(ct);

        int newCount = await db.TrackTags.CountAsync(tt => tt.TrackId == trackId, ct);
        TagsChanged?.Invoke(this, new TagsChangedEventArgs(trackId, newCount));
    }

    /// <summary>The most-used tags, for the empty-query "here's what you can search
    /// for" preview. Ordered by track count desc, then name.</summary>
    public async Task<IReadOnlyList<TagSearchHit>> TopTagsAsync(int limit, CancellationToken ct)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Tags.AsNoTracking()
            .OrderByDescending(t => t.TrackTags.Count)
            .ThenBy(t => t.Name)
            .Select(t => new TagSearchHit(t.Name, t.TrackTags.Count))
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> AutocompleteAsync(string prefix, int limit, CancellationToken ct)
    {
        await using var db = _factory.CreateDbContext();
        var normalized = (prefix ?? "").Trim();
        IQueryable<Tag> q = db.Tags.AsNoTracking().OrderBy(t => t.Name);
        if (normalized.Length > 0)
            q = q.Where(t => EF.Functions.Like(t.Name, $"{normalized}%"));
        return await q.Take(limit).Select(t => t.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TagSearchHit>> SearchTagsAsync(string query, int limit, CancellationToken ct)
    {
        var trimmed = (query ?? "").Trim();
        if (trimmed.Length == 0) return Array.Empty<TagSearchHit>();

        await using var db = _factory.CreateDbContext();
        var prefix = await db.Tags.AsNoTracking()
            .Where(t => EF.Functions.Like(t.Name, $"{trimmed}%"))
            .OrderBy(t => t.Name)
            .Select(t => new TagSearchHit(t.Name, t.TrackTags.Count))
            .Take(limit)
            .ToListAsync(ct);

        if (prefix.Count >= limit) return prefix;

        var seen = new HashSet<string>(prefix.Select(h => h.Name), StringComparer.OrdinalIgnoreCase);
        var substring = await db.Tags.AsNoTracking()
            .Where(t => EF.Functions.Like(t.Name, $"%{trimmed}%") &&
                        !EF.Functions.Like(t.Name, $"{trimmed}%"))
            .OrderBy(t => t.Name)
            .Select(t => new TagSearchHit(t.Name, t.TrackTags.Count))
            .Take(limit - prefix.Count)
            .ToListAsync(ct);

        return prefix.Concat(substring.Where(h => !seen.Contains(h.Name))).ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetTrackIdsForTagAsync(string name, CancellationToken ct)
    {
        var normalized = TagNameNormalizer.Normalize(name);
        if (normalized is null) return Array.Empty<Guid>();

        await using var db = _factory.CreateDbContext();
        return await db.TrackTags
            .AsNoTracking()
            .Where(tt => tt.Tag.Name == normalized)
            .Select(tt => tt.TrackId)
            .ToListAsync(ct);
    }
}
