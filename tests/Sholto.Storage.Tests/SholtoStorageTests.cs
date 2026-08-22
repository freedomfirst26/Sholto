using Microsoft.EntityFrameworkCore;
using Sholto.Storage;
using Sholto.Storage.Entities;

namespace Sholto.Storage.Tests;

public class SholtoStorageTests
{
    [Fact]
    public async Task OpenAsync_on_fresh_path_creates_schema_and_returns_usable_factory()
    {
        var p = Path.Combine(Path.GetTempPath(), $"sholto-open-{Guid.NewGuid():N}.db");
        try
        {
            var factory = await SholtoStorage.OpenAsync(p);
            await using var db = factory.CreateDbContext();
            db.Tracks.Add(new Track { Path = "/a.flac", Title = "T", Artist = "A" });
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.Tracks.CountAsync());
        }
        finally { if (File.Exists(p)) File.Delete(p); }
    }

    [Fact]
    public async Task OpenAsync_on_v3_path_bridges_and_returns_factory()
    {
        var p = Path.Combine(Path.GetTempPath(), $"sholto-open-v3-{Guid.NewGuid():N}.db");
        try
        {
            await V3FixtureBuilder.BuildAsync(p);
            await V3FixtureBuilder.InsertTrackAsync(p,
                new V3FixtureBuilder.SeedTrack("/a.flac", 1, 1, "T", "A", 100));
            var factory = await SholtoStorage.OpenAsync(p);
            await using var db = factory.CreateDbContext();
            Assert.Equal(1, await db.Tracks.CountAsync());
            Assert.Equal("/a.flac", (await db.Tracks.FirstAsync()).Path);
        }
        finally
        {
            if (File.Exists(p)) File.Delete(p);
            foreach (var b in Directory.GetFiles(Path.GetTempPath(),
                Path.GetFileName(p) + ".pre-efcore-backup-*")) File.Delete(b);
        }
    }

    [Fact]
    public async Task BasicAnalysisCache_roundtrip_via_factory()
    {
        var p = Path.Combine(Path.GetTempPath(), $"sholto-cache-{Guid.NewGuid():N}.db");
        var trackFile = Path.Combine(Path.GetTempPath(), $"sholto-cache-{Guid.NewGuid():N}.flac");
        try
        {
            await File.WriteAllBytesAsync(trackFile, new byte[] { 1, 2, 3 });
            var factory = await SholtoStorage.OpenAsync(p);

            await using (var db = factory.CreateDbContext())
            {
                db.Tracks.Add(new Track { Path = trackFile, Title = "T", Artist = "A" });
                await db.SaveChangesAsync();
            }

            var cache = new BasicAnalysisCache(factory);
            Assert.Null(await cache.TryGetAsync(trackFile));   // miss

            var analysis = new Sholto.Analysis.BasicAnalysis(
                Sholto.Analysis.WaveformPeaks.Empty, 128.0, new double[] { 0.0 }, new double[] { 0.0 });
            await cache.PutAsync(trackFile, analysis);
            var hit = await cache.TryGetAsync(trackFile);
            Assert.NotNull(hit);
            Assert.Equal(128.0, hit!.Bpm);
        }
        finally
        {
            if (File.Exists(p)) File.Delete(p);
            if (File.Exists(trackFile)) File.Delete(trackFile);
        }
    }
}
