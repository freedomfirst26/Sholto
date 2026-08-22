using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sholto.Storage;
using Sholto.Storage.Entities;

namespace Sholto.Storage.Tests;

public class SholtoDbContextTests
{
    private static (SqliteConnection keeper, DbContextOptions<SholtoDbContext> options) NewInMemory()
    {
        var keeper = new SqliteConnection("Data Source=:memory:");
        keeper.Open();
        var options = new DbContextOptionsBuilder<SholtoDbContext>()
            .UseSqlite(keeper)
            .Options;
        using (var db = new SholtoDbContext(options))
            db.Database.EnsureCreated();
        return (keeper, options);
    }

    [Fact]
    public async Task Track_with_nested_analyses_roundtrips()
    {
        var (keeper, options) = NewInMemory();
        try
        {
            var trackId = Guid.NewGuid();
            await using (var db = new SholtoDbContext(options))
            {
                db.Tracks.Add(new Track
                {
                    Id = trackId,
                    Path = "/music/foo.flac",
                    Title = "Foo",
                    Artist = "Bar",
                    FileSize = 1234,
                    FileMtime = 1700000000,
                    DurationSecs = 240.5,
                    BasicAnalysis = new BasicAnalysisRecord
                    {
                        Data = new byte[] { 1, 2, 3 },
                        FileMtime = 1700000000,
                        CreatedAt = new DateTime(2026, 5, 23, 12, 0, 0, DateTimeKind.Utc),
                    },
                    KeyAnalysis = new KeyAnalysisRecord
                    {
                        Data = new byte[] { 4, 5, 6 },
                        FileMtime = 1700000000,
                        CreatedAt = new DateTime(2026, 5, 23, 12, 0, 1, DateTimeKind.Utc),
                    },
                    BpmOverride = new BpmOverride { Multiplier = 2.0 },
                });
                await db.SaveChangesAsync();
            }

            await using (var db = new SholtoDbContext(options))
            {
                var loaded = await db.Tracks
                    .Include(t => t.BasicAnalysis)
                    .Include(t => t.KeyAnalysis)
                    .Include(t => t.BpmOverride)
                    .SingleAsync(t => t.Id == trackId);
                Assert.Equal("/music/foo.flac", loaded.Path);
                Assert.Equal(new byte[] { 1, 2, 3 }, loaded.BasicAnalysis!.Data);
                Assert.Equal(new byte[] { 4, 5, 6 }, loaded.KeyAnalysis!.Data);
                Assert.Equal(2.0, loaded.BpmOverride!.Multiplier);
            }
        }
        finally { keeper.Dispose(); }
    }

    [Fact]
    public async Task Setting_upsert_by_key()
    {
        var (keeper, options) = NewInMemory();
        try
        {
            await using (var db = new SholtoDbContext(options))
            {
                db.Settings.Add(new Setting { Key = "music_dir", Value = "/a" });
                await db.SaveChangesAsync();
            }
            await using (var db = new SholtoDbContext(options))
            {
                var s = await db.Settings.FindAsync("music_dir");
                Assert.Equal("/a", s!.Value);
            }
        }
        finally { keeper.Dispose(); }
    }

    [Fact]
    public async Task Unique_path_index_rejects_duplicate()
    {
        var (keeper, options) = NewInMemory();
        try
        {
            await using (var db = new SholtoDbContext(options))
            {
                db.Tracks.Add(new Track { Path = "/a.flac", Title = "T", Artist = "A" });
                db.Tracks.Add(new Track { Path = "/a.flac", Title = "T2", Artist = "A2" });
                await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            }
        }
        finally { keeper.Dispose(); }
    }
}
