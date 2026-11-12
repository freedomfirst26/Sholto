using Microsoft.EntityFrameworkCore;
using Sholto.App.Library.Crates;
using Sholto.App.Storage;
using Sholto.App.Storage.Entities;

namespace Sholto.App.Storage.Tests;

public class CrateMatesTests
{
    private static Task<IDbContextFactory<SholtoDbContext>> NewAsync(TempSqliteDatabase temp) =>
        new ContextCapturingStorage().OpenAsync(temp.Path);

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
        using var temp = new TempSqliteDatabase();
        var factory = await NewAsync(temp);
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

    [Fact]
    public async Task Track_in_two_crates_returns_union_and_sorted_names()
    {
        using var temp = new TempSqliteDatabase();
        var factory = await NewAsync(temp);
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

    [Fact]
    public async Task Track_only_in_all_tracks_returns_empty()
    {
        using var temp = new TempSqliteDatabase();
        var factory = await NewAsync(temp);
        var svc = new CrateService(factory);
        var a = await AddTrackAsync(factory, "a");
        var b = await AddTrackAsync(factory, "b");
        await FileAsync(svc, CrateNames.AllTracks, a, b);

        var mates = await svc.CrateMatesAsync(a);

        Assert.Empty(mates.CrateNames);
        Assert.Empty(mates.TrackIds);
    }

    [Fact]
    public async Task Unknown_id_returns_empty()
    {
        using var temp = new TempSqliteDatabase();
        var factory = await NewAsync(temp);
        var svc = new CrateService(factory);
        var a = await AddTrackAsync(factory, "a");
        await FileAsync(svc, "Featurecast", a);

        var mates = await svc.CrateMatesAsync(Guid.NewGuid());

        Assert.Empty(mates.CrateNames);
        Assert.Empty(mates.TrackIds);
    }
}
