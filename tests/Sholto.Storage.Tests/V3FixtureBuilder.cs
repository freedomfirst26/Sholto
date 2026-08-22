using Microsoft.Data.Sqlite;

namespace Sholto.Storage.Tests;

/// <summary>
/// Builds a fresh SQLite file at <paramref name="path"/> matching the schema
/// the production app shipped under PRAGMA user_version = 3 — i.e. the
/// raw-SQL schema in the old SholtoDatabase.Migrations array. The DDL below
/// is copied verbatim from those three migrations so we never test against
/// a drifted approximation.
/// </summary>
internal static class V3FixtureBuilder
{
    public static async Task BuildAsync(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        await using var conn = new SqliteConnection($"Data Source={path}");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE tracks (
                file_path     TEXT PRIMARY KEY,
                file_size     INTEGER NOT NULL,
                file_mtime    INTEGER NOT NULL,
                title         TEXT NOT NULL,
                artist        TEXT NOT NULL,
                duration_secs REAL NOT NULL
            );
            CREATE TABLE analyses (
                file_path     TEXT NOT NULL,
                analysis_type TEXT NOT NULL,
                data          BLOB NOT NULL,
                file_mtime    INTEGER NOT NULL,
                created_at    INTEGER NOT NULL,
                PRIMARY KEY (file_path, analysis_type)
            );
            CREATE TABLE bpm_overrides (
                file_path  TEXT PRIMARY KEY,
                multiplier REAL NOT NULL
            );
            CREATE TABLE settings (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            PRAGMA user_version = 3;
        """;
        await cmd.ExecuteNonQueryAsync();
    }

    public sealed record SeedTrack(
        string FilePath, long Size, long Mtime, string Title, string Artist, double DurationSecs);

    public static async Task InsertTrackAsync(string path, SeedTrack t)
    {
        await using var conn = new SqliteConnection($"Data Source={path}");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO tracks (file_path, file_size, file_mtime, title, artist, duration_secs)
            VALUES ($p, $s, $m, $t, $a, $d);
        """;
        cmd.Parameters.AddWithValue("$p", t.FilePath);
        cmd.Parameters.AddWithValue("$s", t.Size);
        cmd.Parameters.AddWithValue("$m", t.Mtime);
        cmd.Parameters.AddWithValue("$t", t.Title);
        cmd.Parameters.AddWithValue("$a", t.Artist);
        cmd.Parameters.AddWithValue("$d", t.DurationSecs);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task InsertAnalysisAsync(
        string path, string filePath, string type, byte[] data, long fileMtime, long createdAt)
    {
        await using var conn = new SqliteConnection($"Data Source={path}");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO analyses (file_path, analysis_type, data, file_mtime, created_at)
            VALUES ($p, $t, $d, $m, $c);
        """;
        cmd.Parameters.AddWithValue("$p", filePath);
        cmd.Parameters.AddWithValue("$t", type);
        cmd.Parameters.AddWithValue("$d", data);
        cmd.Parameters.AddWithValue("$m", fileMtime);
        cmd.Parameters.AddWithValue("$c", createdAt);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task InsertBpmOverrideAsync(string path, string filePath, double mult)
    {
        await using var conn = new SqliteConnection($"Data Source={path}");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO bpm_overrides (file_path, multiplier) VALUES ($p, $m);";
        cmd.Parameters.AddWithValue("$p", filePath);
        cmd.Parameters.AddWithValue("$m", mult);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task InsertSettingAsync(string path, string key, string value)
    {
        await using var conn = new SqliteConnection($"Data Source={path}");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO settings (key, value) VALUES ($k, $v);";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        await cmd.ExecuteNonQueryAsync();
    }
}
