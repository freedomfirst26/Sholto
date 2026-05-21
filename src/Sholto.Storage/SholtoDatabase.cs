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

    public static async Task<SholtoDatabase> OpenAsync(string? overridePath = null)
    {
        string path;
        if (overridePath is not null)
        {
            path = overridePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        }
        else
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "sholto");
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "library.db");
        }

        var conn = new SqliteConnection($"Data Source={path}");
        await conn.OpenAsync();

        await RunMigrationsAsync(conn);

        return new SholtoDatabase(conn, path);
    }

    /// <summary>Ordered list of schema migrations. Index N corresponds to schema
    /// version N+1 — i.e. after running Migrations[0], PRAGMA user_version = 1.
    /// Append new migrations only; never edit or reorder existing ones.</summary>
    private static readonly string[] Migrations = new[]
    {
        // v1: initial schema (tracks + analyses + bpm overrides).
        """
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
        """,

        // v2: generic app settings (music dir, output device, future prefs).
        """
        CREATE TABLE IF NOT EXISTS settings (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        """,

        // v3: wipe pre-beatgrid basic analyses. They stored raw madmom downbeats
        // (per-bar detections, often non-equidistant). Fresh analyses store a
        // synthesized constant-spacing beatgrid under the same row, so deleting
        // forces re-derivation the next time each track is touched.
        """
        DELETE FROM analyses WHERE analysis_type = 'basic';
        """,
    };

    /// <summary>Bring the DB up to the latest schema version using PRAGMA
    /// user_version as the schema cursor. Each pending migration runs inside
    /// its own transaction so a failure leaves the DB at the prior version
    /// instead of half-applied.</summary>
    private static async Task RunMigrationsAsync(SqliteConnection conn)
    {
        long current;
        await using (var read = conn.CreateCommand())
        {
            read.CommandText = "PRAGMA user_version;";
            current = (long)(await read.ExecuteScalarAsync() ?? 0L);
        }

        int target = Migrations.Length;
        if (current == target)
        {
            Console.WriteLine($"[Migrations] schema already at v{current} — nothing to do");
            return;
        }
        if (current > target)
        {
            // DB was written by a newer build. We don't have a down-migration
            // story, so flag loudly rather than silently mis-reading rows.
            Console.WriteLine(
                $"[Migrations] WARNING: DB schema v{current} is newer than this build's v{target}. " +
                $"Continuing without migrating — newer columns/tables may be ignored.");
            return;
        }

        Console.WriteLine($"[Migrations] schema v{current} → v{target}; {target - current} migration(s) to apply");

        for (int i = (int)current; i < Migrations.Length; i++)
        {
            int targetVersion = i + 1;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Console.WriteLine($"[Migrations] applying v{targetVersion}…");
            try
            {
                await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync();
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = Migrations[i];
                    await cmd.ExecuteNonQueryAsync();
                }
                // PRAGMA doesn't accept parameters; the value is a hard-coded int
                // from our own array index, so there's no injection surface.
                await using (var bump = conn.CreateCommand())
                {
                    bump.Transaction = tx;
                    bump.CommandText = $"PRAGMA user_version = {targetVersion};";
                    await bump.ExecuteNonQueryAsync();
                }
                await tx.CommitAsync();
                sw.Stop();
                Console.WriteLine($"[Migrations] v{targetVersion} applied in {sw.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                sw.Stop();
                Console.WriteLine($"[Migrations] v{targetVersion} FAILED after {sw.ElapsedMilliseconds} ms: {ex.Message}");
                throw;
            }
        }

        Console.WriteLine($"[Migrations] done — schema at v{target}");
    }

    /// <summary>Current schema version reported by PRAGMA user_version. Exposed
    /// for tests; production code shouldn't need it.</summary>
    public async Task<long> GetSchemaVersionAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = "PRAGMA user_version;";
            return (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        }
        finally { _lock.Release(); }
    }

    /// <summary>Read a value from the generic settings table. Returns null if
    /// the key has never been set.</summary>
    public async Task<string?> GetSettingAsync(string key)
    {
        await _lock.WaitAsync();
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key = $k;";
            cmd.Parameters.AddWithValue("$k", key);
            var result = await cmd.ExecuteScalarAsync();
            return result as string;
        }
        finally { _lock.Release(); }
    }

    /// <summary>Upsert a setting. Passing null deletes the row so "unset" and
    /// "empty string" stay distinguishable.</summary>
    public async Task SetSettingAsync(string key, string? value)
    {
        await _lock.WaitAsync();
        try
        {
            await using var cmd = _conn.CreateCommand();
            if (value is null)
            {
                cmd.CommandText = "DELETE FROM settings WHERE key = $k;";
                cmd.Parameters.AddWithValue("$k", key);
            }
            else
            {
                cmd.CommandText = """
                    INSERT INTO settings (key, value) VALUES ($k, $v)
                    ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """;
                cmd.Parameters.AddWithValue("$k", key);
                cmd.Parameters.AddWithValue("$v", value);
            }
            await cmd.ExecuteNonQueryAsync();
        }
        finally { _lock.Release(); }
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
            Console.WriteLine($"[DB] saved basic analysis ({blob.Length} bytes) for {Path.GetFileName(filePath)}");
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

    // ── KeyAnalysis ──────────────────────────────────────────────────────────
    // Same `analyses` table as BasicAnalysis, distinguished by analysis_type='key'.
    // The schema already keys on (file_path, analysis_type) so multiple analyses
    // per track coexist without any migration.

    public async Task<KeyAnalysis?> GetKeyAnalysisAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        long mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath)).ToUnixTimeSeconds();

        await _lock.WaitAsync();
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT data FROM analyses WHERE file_path = $p AND analysis_type = 'key' AND file_mtime = $m";
            cmd.Parameters.AddWithValue("$p", filePath);
            cmd.Parameters.AddWithValue("$m", mtime);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            var blob = (byte[])reader["data"];
            return KeyAnalysisCodec.Decode(blob);
        }
        finally { _lock.Release(); }
    }

    public async Task SaveKeyAnalysisAsync(string filePath, KeyAnalysis key)
    {
        if (!File.Exists(filePath)) return;
        long mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath)).ToUnixTimeSeconds();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var blob = KeyAnalysisCodec.Encode(key);

        await _lock.WaitAsync();
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO analyses (file_path, analysis_type, data, file_mtime, created_at)
                VALUES ($p, 'key', $d, $m, $c)
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
            Console.WriteLine($"[DB] saved key {key.Camelot} for {Path.GetFileName(filePath)}");
        }
        finally { _lock.Release(); }
    }

    /// <summary>Return Camelot code for every already-analysed track — used to uplift
    /// the library list at startup so users see keys without waiting for a re-decode.</summary>
    public async Task<Dictionary<string, string>> GetAllKeysAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var result = new Dictionary<string, string>();
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT file_path, data FROM analyses WHERE analysis_type = 'key'";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var path = reader.GetString(0);
                var blob = (byte[])reader["data"];
                var key = KeyAnalysisCodec.Decode(blob);
                if (key is not null && !string.IsNullOrEmpty(key.Camelot))
                    result[path] = key.Camelot;
            }
            return result;
        }
        finally { _lock.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _conn.DisposeAsync();
        _lock.Dispose();
    }
}
