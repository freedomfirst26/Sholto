using Microsoft.EntityFrameworkCore;
using Sholto.Storage;
using Sholto.Storage.Entities;

namespace Sholto.Storage.Tests;

public class TagServiceTests
{
    private static async Task<(IDbContextFactory<SholtoDbContext> factory, Guid trackId, string dbPath)> NewWithOneTrackAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sholto-tags-{Guid.NewGuid():N}.db");
        var factory = await SholtoStorage.OpenAsync(dbPath);
        var trackId = Guid.NewGuid();
        await using (var db = factory.CreateDbContext())
        {
            db.Tracks.Add(new Track { Id = trackId, Path = "/m/a.flac", Title = "T", Artist = "A" });
            await db.SaveChangesAsync();
        }
        return (factory, trackId, dbPath);
    }

    private static void Cleanup(string dbPath)
    {
        if (File.Exists(dbPath)) File.Delete(dbPath);
    }

    [Fact]
    public async Task AddTag_normalises_and_stores()
    {
        var (factory, trackId, dbPath) = await NewWithOneTrackAsync();
        try
        {
            var svc = new TagService(factory);
            var result = await svc.AddTagAsync(trackId, "  Deep House  ", default);
            Assert.Equal(AddTagOutcome.Added, result.Outcome);
            Assert.Equal("Deep House", result.StoredName);

            var tags = await svc.GetTagsForTrackAsync(trackId, default);
            Assert.Equal(new[] { "Deep House" }, tags);
        }
        finally { Cleanup(dbPath); }
    }

    [Fact]
    public async Task AddTag_dedups_case_insensitively_keeping_first_seen_casing()
    {
        var (factory, trackId, dbPath) = await NewWithOneTrackAsync();
        try
        {
            var svc = new TagService(factory);
            var first  = await svc.AddTagAsync(trackId, "Deep House", default);
            var second = await svc.AddTagAsync(trackId, "deep house", default);
            Assert.Equal(AddTagOutcome.Added,         first.Outcome);
            Assert.Equal(AddTagOutcome.AlreadyPresent, second.Outcome);
            Assert.Equal("Deep House", second.StoredName);

            await using var db = factory.CreateDbContext();
            Assert.Equal(1, await db.Tags.CountAsync());
        }
        finally { Cleanup(dbPath); }
    }

    [Fact]
    public async Task AddTag_rejects_empty_and_too_long()
    {
        var (factory, trackId, dbPath) = await NewWithOneTrackAsync();
        try
        {
            var svc = new TagService(factory);
            Assert.Equal(AddTagOutcome.RejectedEmpty,  (await svc.AddTagAsync(trackId, "",      default)).Outcome);
            Assert.Equal(AddTagOutcome.RejectedEmpty,  (await svc.AddTagAsync(trackId, "   ",   default)).Outcome);
            Assert.Equal(AddTagOutcome.RejectedTooLong,(await svc.AddTagAsync(trackId, new string('x', 101), default)).Outcome);
        }
        finally { Cleanup(dbPath); }
    }

    [Fact]
    public async Task AddTag_enforces_per_track_cap()
    {
        var (factory, trackId, dbPath) = await NewWithOneTrackAsync();
        try
        {
            var svc = new TagService(factory);
            for (int i = 0; i < TagService.MaxTagsPerTrack; i++)
                Assert.Equal(AddTagOutcome.Added, (await svc.AddTagAsync(trackId, $"tag{i}", default)).Outcome);

            var overflow = await svc.AddTagAsync(trackId, "one-too-many", default);
            Assert.Equal(AddTagOutcome.RejectedLimitReached, overflow.Outcome);
        }
        finally { Cleanup(dbPath); }
    }

    [Fact]
    public async Task RemoveTag_removes_join_row_and_keeps_tag_row()
    {
        var (factory, trackId, dbPath) = await NewWithOneTrackAsync();
        try
        {
            var svc = new TagService(factory);
            await svc.AddTagAsync(trackId, "Deep House", default);
            await svc.RemoveTagAsync(trackId, "deep house", default);

            Assert.Empty(await svc.GetTagsForTrackAsync(trackId, default));
            await using var db = factory.CreateDbContext();
            Assert.Equal(1, await db.Tags.CountAsync());
            Assert.Equal(0, await db.TrackTags.CountAsync());
        }
        finally { Cleanup(dbPath); }
    }

    [Fact]
    public async Task TagsChanged_fires_with_new_count()
    {
        var (factory, trackId, dbPath) = await NewWithOneTrackAsync();
        try
        {
            var svc = new TagService(factory);
            var events = new List<TagsChangedEventArgs>();
            svc.TagsChanged += (_, e) => events.Add(e);

            await svc.AddTagAsync(trackId, "a", default);
            await svc.AddTagAsync(trackId, "b", default);
            await svc.RemoveTagAsync(trackId, "a", default);

            Assert.Equal(3, events.Count);
            Assert.Equal(1, events[0].NewCount);
            Assert.Equal(2, events[1].NewCount);
            Assert.Equal(1, events[2].NewCount);
            Assert.All(events, e => Assert.Equal(trackId, e.TrackId));
        }
        finally { Cleanup(dbPath); }
    }

    [Fact]
    public async Task Autocomplete_returns_prefix_matches_alphabetically()
    {
        var (factory, trackId, dbPath) = await NewWithOneTrackAsync();
        try
        {
            var svc = new TagService(factory);
            foreach (var n in new[] { "deep house", "deep dub", "drum and bass", "techno" })
                await svc.AddTagAsync(trackId, n, default);

            var hits = await svc.AutocompleteAsync("deep", 10, default);
            Assert.Equal(new[] { "deep dub", "deep house" }, hits);
        }
        finally { Cleanup(dbPath); }
    }

    [Fact]
    public async Task SearchTags_returns_hits_with_track_counts()
    {
        var (factory, trackIdA, dbPath) = await NewWithOneTrackAsync();
        try
        {
            await using (var db = factory.CreateDbContext())
            {
                db.Tracks.Add(new Track { Id = Guid.NewGuid(), Path = "/m/b.flac", Title = "T2", Artist = "A2" });
                await db.SaveChangesAsync();
            }
            Guid trackIdB;
            await using (var db = factory.CreateDbContext())
                trackIdB = (await db.Tracks.FirstAsync(t => t.Path == "/m/b.flac")).Id;

            var svc = new TagService(factory);
            await svc.AddTagAsync(trackIdA, "deep house", default);
            await svc.AddTagAsync(trackIdB, "deep house", default);
            await svc.AddTagAsync(trackIdA, "techno", default);

            var hits = await svc.SearchTagsAsync("deep", 10, default);
            Assert.Single(hits);
            Assert.Equal("deep house", hits[0].Name);
            Assert.Equal(2, hits[0].TrackCount);
        }
        finally { Cleanup(dbPath); }
    }

    [Fact]
    public async Task GetTrackIdsForTag_returns_only_matching_tracks()
    {
        var (factory, trackId, dbPath) = await NewWithOneTrackAsync();
        try
        {
            var svc = new TagService(factory);
            await svc.AddTagAsync(trackId, "deep house", default);

            var ids = await svc.GetTrackIdsForTagAsync("Deep House", default);
            Assert.Equal(new[] { trackId }, ids);
            Assert.Empty(await svc.GetTrackIdsForTagAsync("nonexistent", default));
        }
        finally { Cleanup(dbPath); }
    }
}
