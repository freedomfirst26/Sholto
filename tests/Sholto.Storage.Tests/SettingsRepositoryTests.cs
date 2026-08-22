using Microsoft.EntityFrameworkCore;
using Sholto.Storage;
using Sholto.Storage.Entities;

namespace Sholto.Storage.Tests;

public class SettingsRepositoryTests
{
    private static string NewTempDbPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sholto-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "library.db");
    }

    [Fact]
    public async Task FreshDb_CanReadAndWriteSettings()
    {
        var path = NewTempDbPath();
        try
        {
            var factory = await SholtoStorage.OpenAsync(path);
            await using var db = factory.CreateDbContext();
            // A freshly created DB has an empty settings table — no exception means
            // the schema was applied successfully.
            Assert.Equal(0, await db.Settings.CountAsync());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Settings_RoundTripAndOverwrite()
    {
        var path = NewTempDbPath();
        try
        {
            var factory = await SholtoStorage.OpenAsync(path);

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
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Settings_RemoveDeletesRow()
    {
        var path = NewTempDbPath();
        try
        {
            var factory = await SholtoStorage.OpenAsync(path);

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
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Settings_PersistAcrossReopen()
    {
        var path = NewTempDbPath();
        try
        {
            var factory1 = await SholtoStorage.OpenAsync(path);
            await using (var db = factory1.CreateDbContext())
            {
                db.Settings.Add(new Setting { Key = SettingsKeys.MusicDir, Value = "/media/data/music" });
                await db.SaveChangesAsync();
            }

            var factory2 = await SholtoStorage.OpenAsync(path);
            await using (var db = factory2.CreateDbContext())
            {
                Assert.Equal("/media/data/music",
                    (await db.Settings.FindAsync(SettingsKeys.MusicDir))!.Value);
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task OpenAsync_IsIdempotentOnReopen()
    {
        var path = NewTempDbPath();
        try
        {
            // Open twice; the second call should not throw and the DB should be usable.
            await SholtoStorage.OpenAsync(path);
            var factory2 = await SholtoStorage.OpenAsync(path);
            await using var db = factory2.CreateDbContext();
            Assert.Equal(0, await db.Settings.CountAsync());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
