using Microsoft.EntityFrameworkCore;
using Sholto.App.Library.Crates;
using Sholto.App.Storage;
using Sholto.App.Storage.Entities;

namespace Sholto.App.Storage.Tests;

public class CrateMembershipTests
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

    private static async Task<int> FileAsync(CrateService svc, string crate, params Guid[] ids)
    {
        int crateId = await svc.CreateAsync(crate);
        foreach (var id in ids) await svc.AddTrackAsync(crateId, id);
        return crateId;
    }

    [Fact]
    public async Task Returns_every_crate_except_all_tracks_with_its_members()
    {
        using var temp = new TempSqliteDatabase();
        var factory = await NewAsync(temp);
        var svc = new CrateService(factory);
        var t1 = await AddTrackAsync(factory, "t1");
        var t2 = await AddTrackAsync(factory, "t2");
        var t3 = await AddTrackAsync(factory, "t3");
        var allId = await FileAsync(svc, CrateNames.AllTracks, t1, t2, t3);
        var aId = await FileAsync(svc, "A", t1, t2);
        var bId = await FileAsync(svc, "B", t2, t3);

        var m = await svc.MembershipAsync();

        Assert.Equal(2, m.TracksByCrate.Count);
        Assert.Equal(new HashSet<Guid> { t1, t2 }, m.TracksByCrate[aId]);
        Assert.Equal(new HashSet<Guid> { t2, t3 }, m.TracksByCrate[bId]);
        Assert.False(m.TracksByCrate.ContainsKey(allId));
        Assert.Equal(allId, m.AllTracksId);
    }

    [Fact]
    public async Task Empty_crate_is_present_with_an_empty_set()
    {
        using var temp = new TempSqliteDatabase();
        var factory = await NewAsync(temp);
        var svc = new CrateService(factory);
        var t1 = await AddTrackAsync(factory, "t1");
        await FileAsync(svc, CrateNames.AllTracks, t1);
        var emptyId = await FileAsync(svc, "Empty");

        var m = await svc.MembershipAsync();

        Assert.Equal(new[] { emptyId }, m.TracksByCrate.Keys);
        Assert.Empty(m.TracksByCrate[emptyId]);
    }

    [Fact]
    public async Task No_all_tracks_crate_leaves_its_id_null()
    {
        using var temp = new TempSqliteDatabase();
        var factory = await NewAsync(temp);
        var svc = new CrateService(factory);
        var t1 = await AddTrackAsync(factory, "t1");
        var aId = await FileAsync(svc, "A", t1);

        var m = await svc.MembershipAsync();

        Assert.Null(m.AllTracksId);
        Assert.Equal(new HashSet<Guid> { t1 }, m.TracksByCrate[aId]);
    }
}
