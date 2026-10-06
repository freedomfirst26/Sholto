using Microsoft.EntityFrameworkCore;
using Sholto.App.Library.Crates;
using Sholto.App.Storage;
using Sholto.Storage.Entities;

namespace Sholto.App.Storage.Tests;

public class CrateMatesTests
{
    private static async Task<(IDbContextFactory<SholtoDbContext> factory, string dbPath)> NewAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sholto-crates-{Guid.NewGuid():N}.db");
        var factory = await new ContextCapturingStorage().OpenAsync(dbPath);
        return (factory, dbPath);
    }

    private static void Cleanup(string dbPath)
    {
        if (File.Exists(dbPath)) File.Delete(dbPath);
    }

    private static async Task<Guid> AddTrackAsync(IDbContextFactory<SholtoDbContext> factory, string name)
    {
        var id = Guid.NewGuid();
        await using var db = factory.CreateDbContext();
        db.Tracks.Add(new Track { Id = id, Path = $"/m/{name}.flac", Title = name, Artist = "A" });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task FileAsync(CrateService svc, string crate, params Guid[] ids)
    {
        int crateId = await svc.CreateAsync(crate);
        foreach (var id in ids) await svc.AddTrackAsync(crateId, id);
    }

    [Fact]
    public async Task Track_in_crate_and_all_tracks_returns_crate_members_only()
    {
        var (factory, dbPath) = await NewAsync();
        try
        {
            var svc = new CrateService(factory);
            var a = await AddTrackAsync(factory, "a");
            var b = await AddTrackAsync(factory, "b");
            var c = await AddTrackAsync(factory, "c");
            await FileAsync(svc, CrateNames.AllTracks, a, b, c);
            await FileAsync(svc, "Featurecast", a, b);

            var mates = await svc.CrateMatesAsync(a);

            Assert.Equal(new[] { "Featurecast" }, mates.CrateNames);
            Assert.Equal(new HashSet<Guid> { a, b }, mates.TrackIds);
        }
        finally { Cleanup(dbPath); }
    }

    [Fact]
    public async Task Track_in_two_crates_returns_union_and_sorted_names()
    {
        var (factory, dbPath) = await NewAsync();
        try
        {
            var svc = new CrateService(factory);
            var a = await AddTrackAsync(factory, "a");
            var b = await AddTrackAsync(factory, "b");
            var c = await AddTrackAsync(factory, "c");
            var d = await AddTrackAsync(factory, "d");
            await FileAsync(svc, CrateNames.AllTracks, a, b, c, d);
            await FileAsync(svc, "Zed", a, b);
            await FileAsync(svc, "Alpha", a, c);

            var mates = await svc.CrateMatesAsync(a);

            Assert.Equal(new[] { "Alpha", "Zed" }, mates.CrateNames);
            Assert.Equal(new HashSet<Guid> { a, b, c }, mates.TrackIds);
        }
        finally { Cleanup(dbPath); }
    }

    [Fact]
    public async Task Track_only_in_all_tracks_returns_empty()
    {
        var (factory, dbPath) = await NewAsync();
        try
        {
            var svc = new CrateService(factory);
            var a = await AddTrackAsync(factory, "a");
            var b = await AddTrackAsync(factory, "b");
            await FileAsync(svc, CrateNames.AllTracks, a, b);

            var mates = await svc.CrateMatesAsync(a);

            Assert.Empty(mates.CrateNames);
            Assert.Empty(mates.TrackIds);
        }
        finally { Cleanup(dbPath); }
    }

    [Fact]
    public async Task Unknown_id_returns_empty()
    {
        var (factory, dbPath) = await NewAsync();
        try
        {
            var svc = new CrateService(factory);
            var a = await AddTrackAsync(factory, "a");
            await FileAsync(svc, "Featurecast", a);

            var mates = await svc.CrateMatesAsync(Guid.NewGuid());

            Assert.Empty(mates.CrateNames);
            Assert.Empty(mates.TrackIds);
        }
        finally { Cleanup(dbPath); }
    }
}
