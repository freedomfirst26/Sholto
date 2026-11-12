using Microsoft.EntityFrameworkCore;
using Sholto.App.Settings;
using Sholto.App.Storage;
using Sholto.App.Storage.Entities;

namespace Sholto.App.Storage.Tests;

public class SettingsRepositoryTests
{
    [Fact]
    public async Task FreshDb_CanReadAndWriteSettings()
    {
        using var temp = new TempSqliteDatabase();
        var path = temp.Path;
        var factory = await new ContextCapturingStorage().OpenAsync(path);
        await using var db = factory.CreateDbContext();
        // A freshly created DB has an empty settings table — no exception means
        // the schema was applied successfully.
        Assert.Equal(0, await db.Settings.CountAsync());
    }

    [Fact]
    public async Task Settings_RoundTripAndOverwrite()
    {
        using var temp = new TempSqliteDatabase();
        var path = temp.Path;
        var factory = await new ContextCapturingStorage().OpenAsync(path);

        await using (var db = factory.CreateDbContext())
        {
            Assert.Null(await db.Settings.FindAsync(SettingsKeys.MusicDir));

            db.Settings.Add(new Setting { Key = SettingsKeys.MusicDir, Value = "/tmp/music" });
            await db.SaveChangesAsync();
            Assert.Equal("/tmp/music", (await db.Settings.FindAsync(SettingsKeys.MusicDir))!.Value);

            var row = await db.Settings.FindAsync(SettingsKeys.MusicDir);
            row!.Value = "/srv/music";
            await db.SaveChangesAsync();
            Assert.Equal("/srv/music", (await db.Settings.FindAsync(SettingsKeys.MusicDir))!.Value);
        }
    }

    [Fact]
    public async Task Settings_RemoveDeletesRow()
    {
        using var temp = new TempSqliteDatabase();
        var path = temp.Path;
        var factory = await new ContextCapturingStorage().OpenAsync(path);

        await using (var db = factory.CreateDbContext())
        {
            db.Settings.Add(new Setting { Key = SettingsKeys.OutputDevice, Value = "Some Device" });
            await db.SaveChangesAsync();
            Assert.NotNull(await db.Settings.FindAsync(SettingsKeys.OutputDevice));

            var row = await db.Settings.FindAsync(SettingsKeys.OutputDevice);
            db.Settings.Remove(row!);
            await db.SaveChangesAsync();
            Assert.Null(await db.Settings.FindAsync(SettingsKeys.OutputDevice));
        }
    }

    [Fact]
    public async Task Settings_PersistAcrossReopen()
    {
        using var temp = new TempSqliteDatabase();
        var path = temp.Path;
        var factory1 = await new ContextCapturingStorage().OpenAsync(path);
        await using (var db = factory1.CreateDbContext())
        {
            db.Settings.Add(new Setting { Key = SettingsKeys.MusicDir, Value = "/media/data/music" });
            await db.SaveChangesAsync();
        }

        var factory2 = await new ContextCapturingStorage().OpenAsync(path);
        await using (var db = factory2.CreateDbContext())
        {
            Assert.Equal("/media/data/music",
                (await db.Settings.FindAsync(SettingsKeys.MusicDir))!.Value);
        }
    }

    [Fact]
    public async Task OpenAsync_IsIdempotentOnReopen()
    {
        using var temp = new TempSqliteDatabase();
        var path = temp.Path;
        // Open twice; the second call should not throw and the DB should be usable.
        await new ContextCapturingStorage().OpenAsync(path);
        var factory2 = await new ContextCapturingStorage().OpenAsync(path);
        await using var db = factory2.CreateDbContext();
        Assert.Equal(0, await db.Settings.CountAsync());
    }
}
