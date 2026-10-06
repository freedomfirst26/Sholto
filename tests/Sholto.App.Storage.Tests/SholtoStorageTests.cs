using Microsoft.EntityFrameworkCore;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Storage;
using Sholto.Storage.Entities;
using Sholto.Data;

namespace Sholto.App.Storage.Tests;

public class SholtoStorageTests
{
    [Fact]
    public async Task OpenAsync_on_fresh_path_creates_schema_and_returns_usable_factory()
    {
        var p = Path.Combine(Path.GetTempPath(), $"sholto-open-{Guid.NewGuid():N}.db");
        try
        {
            var factory = await new ContextCapturingStorage().OpenAsync(p);
            await using var db = factory.CreateDbContext();
            db.Tracks.Add(new Track { Path = "/a.flac", Title = "T", Artist = "A" });
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.Tracks.CountAsync());
        }
        finally { if (File.Exists(p)) File.Delete(p); }
    }

    [Fact]
    public async Task BasicAnalysisCache_roundtrip_via_factory()
    {
        var p = Path.Combine(Path.GetTempPath(), $"sholto-cache-{Guid.NewGuid():N}.db");
        var trackFile = Path.Combine(Path.GetTempPath(), $"sholto-cache-{Guid.NewGuid():N}.flac");
        try
        {
            await File.WriteAllBytesAsync(trackFile, new byte[] { 1, 2, 3 });
            var factory = await new ContextCapturingStorage().OpenAsync(p);

            await using (var db = factory.CreateDbContext())
            {
                db.Tracks.Add(new Track { Path = trackFile, Title = "T", Artist = "A" });
                await db.SaveChangesAsync();
            }

            var cache = new BasicAnalysisStore(factory, new BasicAnalysisSerializer());
            Assert.Null(await cache.TryGetAsync(trackFile));   // miss

            var analysis = new BasicAnalysis(
                new WaveformPeaksFactory().None(), 128.0, new double[] { 0.0 }, new double[] { 0.0 });
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

    [Fact]
    public void Serializer_Decode_PreviousVersionBlob_ReturnsNullNotThrow()
    {
        var ser = new BasicAnalysisSerializer();
        var analysis = new BasicAnalysis(
            new WaveformPeaksFactory().None(), 128.0, new double[] { 0.0 }, new double[] { 0.0 });
        byte[] blob = ser.Encode(analysis);
        Assert.NotNull(ser.Decode(blob));

        // Rewrite the version header to v3, as a pre-fix cached blob would carry.
        BitConverter.GetBytes(BasicAnalysisSerializer.Version - 1).CopyTo(blob, 0);
        Assert.Null(ser.Decode(blob));
    }
}
