using Microsoft.Data.Sqlite;
using Sholto.Analysis;
using Sholto.Library;

namespace Sholto.Storage;

/// <summary>
/// SQLite-backed track + analysis cache. Single file at ~/.local/share/sholto/library.db.
/// Reused by every part of the app that needs to know "have we already analyzed this file?".
/// Thread-safe via a single connection guarded by an async lock.
/// </summary>
public sealed class SholtoDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _conn;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public string DatabasePath { get; }

    private SholtoDatabase(SqliteConnection conn, string path)
    {
        _conn = conn;
        DatabasePath = path;
    }

    public static async Task<SholtoDatabase> OpenAsync()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "sholto");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "library.db");

        var conn = new SqliteConnection($"Data Source={path}");
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS tracks (
                    file_path     TEXT PRIMARY KEY,
                    file_size     INTEGER NOT NULL,
                    file_mtime    INTEGER NOT NULL,
                    title         TEXT NOT NULL,
                    artist        TEXT NOT NULL,
                    duration_secs REAL NOT NULL
                );
                CREATE TABLE IF NOT EXISTS analyses (
                    file_path     TEXT NOT NULL,
                    analysis_type TEXT NOT NULL,
                    data          BLOB NOT NULL,
                    file_mtime    INTEGER NOT NULL,
                    created_at    INTEGER NOT NULL,
                    PRIMARY KEY (file_path, analysis_type)
                );
                CREATE TABLE IF NOT EXISTS bpm_overrides (
                    file_path  TEXT PRIMARY KEY,
                    multiplier REAL NOT NULL
                );
            """;
            await cmd.ExecuteNonQueryAsync();
        }

        return new SholtoDatabase(conn, path);
    }

    public async Task UpsertTrackAsync(Track track)
    {
        var info = new FileInfo(track.FilePath);
        if (!info.Exists) return;

        await _lock.WaitAsync();
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO tracks (file_path, file_size, file_mtime, title, artist, duration_secs)
                VALUES ($path, $size, $mtime, $title, $artist, $dur)
                ON CONFLICT(file_path) DO UPDATE SET
                    file_size = excluded.file_size,
                    file_mtime = excluded.file_mtime,
                    title = excluded.title,
                    artist = excluded.artist,
                    duration_secs = excluded.duration_secs;
            """;
            cmd.Parameters.AddWithValue("$path", track.FilePath);
            cmd.Parameters.AddWithValue("$size", info.Length);
            cmd.Parameters.AddWithValue("$mtime", new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("$title", track.Title);
            cmd.Parameters.AddWithValue("$artist", track.Artist);
            cmd.Parameters.AddWithValue("$dur", track.Duration.TotalSeconds);
            await cmd.ExecuteNonQueryAsync();
        }
        finally { _lock.Release(); }
    }

    /// <summary>Return cached BasicAnalysis if its file_mtime still matches the file on disk.</summary>
    public async Task<BasicAnalysis?> GetBasicAnalysisAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        long mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath)).ToUnixTimeSeconds();

        await _lock.WaitAsync();
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT data FROM analyses WHERE file_path = $p AND analysis_type = 'basic' AND file_mtime = $m";
            cmd.Parameters.AddWithValue("$p", filePath);
            cmd.Parameters.AddWithValue("$m", mtime);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            var blob = (byte[])reader["data"];
            return AnalysisCodec.Decode(blob);
        }
        finally { _lock.Release(); }
    }

    public async Task SaveBasicAnalysisAsync(string filePath, BasicAnalysis basic)
    {
        if (!File.Exists(filePath)) return;
        long mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath)).ToUnixTimeSeconds();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var blob = AnalysisCodec.Encode(basic);

        await _lock.WaitAsync();
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO analyses (file_path, analysis_type, data, file_mtime, created_at)
                VALUES ($p, 'basic', $d, $m, $c)
                ON CONFLICT(file_path, analysis_type) DO UPDATE SET
                    data = excluded.data,
                    file_mtime = excluded.file_mtime,
                    created_at = excluded.created_at;
            """;
            cmd.Parameters.AddWithValue("$p", filePath);
            cmd.Parameters.AddWithValue("$d", blob);
            cmd.Parameters.AddWithValue("$m", mtime);
            cmd.Parameters.AddWithValue("$c", now);
            await cmd.ExecuteNonQueryAsync();
        }
        finally { _lock.Release(); }
    }

    /// <summary>Return BPM for tracks already analysed — used to uplift the track list.</summary>
    public async Task<Dictionary<string, double>> GetAllBpmsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var result = new Dictionary<string, double>();
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT file_path, data FROM analyses WHERE analysis_type = 'basic'";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var path = reader.GetString(0);
                var blob = (byte[])reader["data"];
                var basic = AnalysisCodec.Decode(blob);
                if (basic is not null) result[path] = basic.Bpm;
            }
            return result;
        }
        finally { _lock.Release(); }
    }

    public async Task<Dictionary<string, double>> GetAllBpmMultipliersAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var result = new Dictionary<string, double>();
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT file_path, multiplier FROM bpm_overrides";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result[reader.GetString(0)] = reader.GetDouble(1);
            return result;
        }
        finally { _lock.Release(); }
    }

    public async Task UpsertBpmMultiplierAsync(string filePath, double multiplier)
    {
        await _lock.WaitAsync();
        try
        {
            await using var cmd = _conn.CreateCommand();
            if (Math.Abs(multiplier - 1.0) < 0.0001)
            {
                // Unity = the default. Delete the row instead of storing 1.0 so the
                // table only carries actual overrides.
                cmd.CommandText = "DELETE FROM bpm_overrides WHERE file_path = $p";
                cmd.Parameters.AddWithValue("$p", filePath);
            }
            else
            {
                cmd.CommandText = """
                    INSERT INTO bpm_overrides (file_path, multiplier) VALUES ($p, $m)
                    ON CONFLICT(file_path) DO UPDATE SET multiplier = excluded.multiplier;
                    """;
                cmd.Parameters.AddWithValue("$p", filePath);
                cmd.Parameters.AddWithValue("$m", multiplier);
            }
            await cmd.ExecuteNonQueryAsync();
        }
        finally { _lock.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _conn.DisposeAsync();
        _lock.Dispose();
    }
}
