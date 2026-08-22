using Microsoft.Data.Sqlite;
using Sholto.Storage;

namespace Sholto.Storage.Tests;

public class DatabaseBridgeTests
{
    private static string TempPath() => Path.Combine(
        Path.GetTempPath(), $"sholto-bridge-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task Missing_file_is_noop()
    {
        var p = TempPath();
        await DatabaseBridge.RunIfNeededAsync(p, default);
        Assert.False(File.Exists(p));
    }

    [Fact]
    public async Task Empty_v0_file_is_noop()
    {
        var p = TempPath();
        try
        {
            await using (var conn = new SqliteConnection($"Data Source={p}"))
            {
                await conn.OpenAsync();
                // Touch the file, leave PRAGMA user_version = 0.
            }
            await DatabaseBridge.RunIfNeededAsync(p, default);
            Assert.Equal(0, await ReadUserVersionAsync(p));
        }
        finally { if (File.Exists(p)) File.Delete(p); }
    }

    [Fact]
    public async Task Already_bridged_db_is_noop()
    {
        var p = TempPath();
        try
        {
            await using (var conn = new SqliteConnection($"Data Source={p}"))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE "__EFMigrationsHistory" (
                        "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                        "ProductVersion" TEXT NOT NULL);
                """;
                await cmd.ExecuteNonQueryAsync();
            }
            await DatabaseBridge.RunIfNeededAsync(p, default);
            // Should not throw, should not modify.
        }
        finally { if (File.Exists(p)) File.Delete(p); }
    }

    [Fact]
    public async Task Newer_schema_throws()
    {
        var p = TempPath();
        try
        {
            await using (var conn = new SqliteConnection($"Data Source={p}"))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA user_version = 99;";
                await cmd.ExecuteNonQueryAsync();
            }
            await Assert.ThrowsAsync<IncompatibleSchemaException>(
                () => DatabaseBridge.RunIfNeededAsync(p, default));
        }
        finally { if (File.Exists(p)) File.Delete(p); }
    }

    private static async Task<long> ReadUserVersionAsync(string path)
    {
        await using var conn = new SqliteConnection($"Data Source={path}");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return (long)(await cmd.ExecuteScalarAsync() ?? 0L);
    }

    [Fact]
    public async Task Lifts_v3_tracks_into_efcore_schema()
    {
        var p = TempPath();
        try
        {
            await V3FixtureBuilder.BuildAsync(p);
            await V3FixtureBuilder.InsertTrackAsync(p, new V3FixtureBuilder.SeedTrack(
                "/m/a.flac", 100, 1700, "A", "ArtistA", 200.0));
            await V3FixtureBuilder.InsertTrackAsync(p, new V3FixtureBuilder.SeedTrack(
                "/m/b.flac", 200, 1701, "B", "ArtistB", 300.0));
            await V3FixtureBuilder.InsertAnalysisAsync(p, "/m/a.flac", "basic",
                new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, 1700, 1900);
            await V3FixtureBuilder.InsertAnalysisAsync(p, "/m/a.flac", "key",
                new byte[] { 0x01, 0x02 }, 1700, 1901);
            await V3FixtureBuilder.InsertBpmOverrideAsync(p, "/m/a.flac", 2.0);
            await V3FixtureBuilder.InsertSettingAsync(p, "music_dir", "/m");

            await DatabaseBridge.RunIfNeededAsync(p, default);

            await using var conn = new SqliteConnection($"Data Source={p}");
            await conn.OpenAsync();

            Assert.Equal(2L, await ScalarAsync<long>(conn, "SELECT COUNT(*) FROM \"Tracks\";"));
            Assert.Equal(1L, await ScalarAsync<long>(conn, "SELECT COUNT(*) FROM \"BasicAnalyses\";"));
            Assert.Equal(1L, await ScalarAsync<long>(conn, "SELECT COUNT(*) FROM \"KeyAnalyses\";"));
            Assert.Equal(1L, await ScalarAsync<long>(conn, "SELECT COUNT(*) FROM \"BpmOverrides\";"));
            Assert.Equal("/m", await ScalarAsync<string>(conn,
                "SELECT \"Value\" FROM \"Settings\" WHERE \"Key\"='music_dir';"));

            var blob = (byte[])(await ScalarAsync<object>(conn,
                "SELECT \"Data\" FROM \"BasicAnalyses\" b JOIN \"Tracks\" t ON t.\"Id\"=b.\"TrackId\" WHERE t.\"Path\"='/m/a.flac';"))!;
            Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, blob);

            Assert.Equal(1L, await ScalarAsync<long>(conn,
                "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";"));

            Assert.Null(await ScalarAsync<object>(conn,
                "SELECT 1 FROM sqlite_master WHERE type='table' AND name='tracks';"));
            Assert.Null(await ScalarAsync<object>(conn,
                "SELECT 1 FROM sqlite_master WHERE type='table' AND name='analyses';"));
        }
        finally
        {
            if (File.Exists(p)) File.Delete(p);
            foreach (var b in Directory.GetFiles(Path.GetDirectoryName(p)!,
                Path.GetFileName(p) + ".pre-efcore-backup-*")) File.Delete(b);
        }
    }

    [Fact]
    public async Task Backup_file_is_written_before_lift()
    {
        var p = TempPath();
        try
        {
            await V3FixtureBuilder.BuildAsync(p);
            await DatabaseBridge.RunIfNeededAsync(p, default);
            var backups = Directory.GetFiles(Path.GetDirectoryName(p)!,
                Path.GetFileName(p) + ".pre-efcore-backup-*");
            Assert.Single(backups);
            foreach (var b in backups) File.Delete(b);
        }
        finally { if (File.Exists(p)) File.Delete(p); }
    }

    [Fact]
    public async Task Orphan_analyses_are_dropped()
    {
        var p = TempPath();
        try
        {
            await V3FixtureBuilder.BuildAsync(p);
            await V3FixtureBuilder.InsertAnalysisAsync(p, "/m/ghost.flac", "basic",
                new byte[] { 9 }, 1700, 1900);
            await DatabaseBridge.RunIfNeededAsync(p, default);

            await using var conn = new SqliteConnection($"Data Source={p}");
            await conn.OpenAsync();
            Assert.Equal(0L, await ScalarAsync<long>(conn,
                "SELECT COUNT(*) FROM \"BasicAnalyses\";"));
        }
        finally
        {
            if (File.Exists(p)) File.Delete(p);
            foreach (var b in Directory.GetFiles(Path.GetDirectoryName(p)!,
                Path.GetFileName(p) + ".pre-efcore-backup-*")) File.Delete(b);
        }
    }

    [Fact]
    public async Task Bridge_is_idempotent()
    {
        var p = TempPath();
        try
        {
            await V3FixtureBuilder.BuildAsync(p);
            await V3FixtureBuilder.InsertTrackAsync(p, new V3FixtureBuilder.SeedTrack(
                "/m/a.flac", 100, 1700, "A", "ArtistA", 200.0));
            await DatabaseBridge.RunIfNeededAsync(p, default);
            await DatabaseBridge.RunIfNeededAsync(p, default);   // must no-op

            await using var conn = new SqliteConnection($"Data Source={p}");
            await conn.OpenAsync();
            Assert.Equal(1L, await ScalarAsync<long>(conn, "SELECT COUNT(*) FROM \"Tracks\";"));
        }
        finally
        {
            if (File.Exists(p)) File.Delete(p);
            foreach (var b in Directory.GetFiles(Path.GetDirectoryName(p)!,
                Path.GetFileName(p) + ".pre-efcore-backup-*")) File.Delete(b);
        }
    }

    private static async Task<T?> ScalarAsync<T>(SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var raw = await cmd.ExecuteScalarAsync();
        if (raw is null || raw is DBNull) return default;
        return (T)raw;
    }
}
