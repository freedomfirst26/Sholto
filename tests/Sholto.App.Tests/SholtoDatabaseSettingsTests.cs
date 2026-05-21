using Sholto.Storage;

namespace Sholto.App.Tests;

public class SholtoDatabaseSettingsTests
{
    private static string NewTempDbPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sholto-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "library.db");
    }

    [Fact]
    public async Task FreshDb_IsAtLatestSchemaVersion()
    {
        var path = NewTempDbPath();
        await using var db = await SholtoDatabase.OpenAsync(path);

        var version = await db.GetSchemaVersionAsync();
        Assert.True(version >= 2, $"expected schema version >= 2, got {version}");
    }

    [Fact]
    public async Task Settings_RoundTripAndOverwrite()
    {
        var path = NewTempDbPath();
        await using var db = await SholtoDatabase.OpenAsync(path);

        Assert.Null(await db.GetSettingAsync(SettingsKeys.MusicDir));

        await db.SetSettingAsync(SettingsKeys.MusicDir, "/tmp/music");
        Assert.Equal("/tmp/music", await db.GetSettingAsync(SettingsKeys.MusicDir));

        await db.SetSettingAsync(SettingsKeys.MusicDir, "/srv/music");
        Assert.Equal("/srv/music", await db.GetSettingAsync(SettingsKeys.MusicDir));
    }

    [Fact]
    public async Task Settings_NullDeletesRow()
    {
        var path = NewTempDbPath();
        await using var db = await SholtoDatabase.OpenAsync(path);

        await db.SetSettingAsync(SettingsKeys.OutputDevice, "Some Device");
        Assert.NotNull(await db.GetSettingAsync(SettingsKeys.OutputDevice));

        await db.SetSettingAsync(SettingsKeys.OutputDevice, null);
        Assert.Null(await db.GetSettingAsync(SettingsKeys.OutputDevice));
    }

    [Fact]
    public async Task Settings_PersistAcrossReopen()
    {
        var path = NewTempDbPath();
        await using (var db = await SholtoDatabase.OpenAsync(path))
        {
            await db.SetSettingAsync(SettingsKeys.MusicDir, "/media/data/music");
        }
        await using (var db = await SholtoDatabase.OpenAsync(path))
        {
            Assert.Equal("/media/data/music", await db.GetSettingAsync(SettingsKeys.MusicDir));
        }
    }

    [Fact]
    public async Task Migrations_AreIdempotentOnReopen()
    {
        var path = NewTempDbPath();
        long v1, v2;
        await using (var db = await SholtoDatabase.OpenAsync(path)) v1 = await db.GetSchemaVersionAsync();
        await using (var db = await SholtoDatabase.OpenAsync(path)) v2 = await db.GetSchemaVersionAsync();
        Assert.Equal(v1, v2);
    }
}
