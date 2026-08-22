using Microsoft.Data.Sqlite;

namespace Sholto.Storage;

/// <summary>
/// One-shot migration of the legacy raw-SQL database (PRAGMA user_version = 3)
/// onto the EF Core-managed schema. Runs before EF's own MigrateAsync. After
/// a successful bridge, __EFMigrationsHistory is seeded so EF's first
/// MigrateAsync is a no-op.
/// </summary>
public static class DatabaseBridge
{
    public static async Task RunIfNeededAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return;

        await using var conn = new SqliteConnection($"Data Source={path}");
        await conn.OpenAsync(ct);

        if (await HasEfHistoryTableAsync(conn, ct))
        {
            Console.WriteLine($"[Bridge] {path} already has __EFMigrationsHistory — skipping");
            return;
        }

        long version = await ReadUserVersionAsync(conn, ct);
        if (version == 0 && !await HasLegacyTracksTableAsync(conn, ct))
        {
            Console.WriteLine($"[Bridge] {path} is empty — leaving for EF to initialise");
            return;
        }
        if (version > 3)
        {
            throw new IncompatibleSchemaException(version,
                $"Database at {path} reports PRAGMA user_version = {version}, " +
                "which is newer than this build understands (max 3).");
        }
        if (version != 3)
        {
            throw new IncompatibleSchemaException(version,
                $"Database at {path} reports PRAGMA user_version = {version}; " +
                "expected 3 (the last legacy schema). Earlier versions are not supported.");
        }

        // v3 lift — implemented in Task 8.
        await LiftV3Async(conn, path, ct);
    }

    private static async Task<bool> HasEfHistoryTableAsync(SqliteConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory';";
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    private static async Task<bool> HasLegacyTracksTableAsync(SqliteConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name='tracks';";
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    private static async Task<long> ReadUserVersionAsync(SqliteConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return (long)(await cmd.ExecuteScalarAsync(ct) ?? 0L);
    }

    private const string InitialCreateMigrationId = "20260523143923_InitialCreate";
    private const string EfProductVersion = "10.0.0";

    private static async Task LiftV3Async(SqliteConnection conn, string path, CancellationToken ct)
    {
        // 1. Backup before any mutation.
        var backupPath = $"{path}.pre-efcore-backup-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        await conn.CloseAsync();
        File.Copy(path, backupPath);
        await conn.OpenAsync(ct);
        Console.WriteLine($"[Bridge] backup written: {backupPath}");

        // 2. Build file_path -> Guid map.
        var pathToId = new Dictionary<string, Guid>(StringComparer.Ordinal);
        await using (var read = conn.CreateCommand())
        {
            read.CommandText = "SELECT file_path FROM tracks;";
            await using var r = await read.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) pathToId[r.GetString(0)] = Guid.NewGuid();
        }
        Console.WriteLine($"[Bridge] {pathToId.Count} tracks to lift");

        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        // 3a. Stage data reads: tracks, analyses, bpm_overrides, settings all happen
        //     before we touch the schema. We've already built pathToId from tracks above.
        //     Now read analyses, bpm_overrides and settings into in-memory lists so we
        //     can drop legacy tables before creating new ones (SQLite table names are
        //     case-insensitive, so "Tracks" conflicts with "tracks" etc.).

        var analyses = new List<(string fp, string type, byte[] data, long mtime, long createdUnix)>();
        await using (var read = conn.CreateCommand())
        {
            read.Transaction = tx;
            read.CommandText = "SELECT file_path, analysis_type, data, file_mtime, created_at FROM analyses;";
            await using var r = await read.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                analyses.Add((r.GetString(0), r.GetString(1), (byte[])r["data"], r.GetInt64(3), r.GetInt64(4)));
        }

        var bpmRows = new List<(string fp, double mult)>();
        await using (var read = conn.CreateCommand())
        {
            read.Transaction = tx;
            read.CommandText = "SELECT file_path, multiplier FROM bpm_overrides;";
            await using var r = await read.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                bpmRows.Add((r.GetString(0), r.GetDouble(1)));
        }

        var trackRows = new List<(string fp, string title, string artist, long size, long mtime, double dur)>();
        await using (var read = conn.CreateCommand())
        {
            read.Transaction = tx;
            read.CommandText = "SELECT file_path, title, artist, file_size, file_mtime, duration_secs FROM tracks;";
            await using var r = await read.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                trackRows.Add((r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt64(3), r.GetInt64(4), r.GetDouble(5)));
        }

        var settingRows = new List<(string key, string value)>();
        await using (var read = conn.CreateCommand())
        {
            read.Transaction = tx;
            read.CommandText = "SELECT key, value FROM settings;";
            await using var r = await read.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                settingRows.Add((r.GetString(0), r.GetString(1)));
        }

        // 3b. Drop all legacy tables so new EF table names don't conflict.
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                DROP TABLE analyses;
                DROP TABLE bpm_overrides;
                DROP TABLE settings;
                DROP TABLE tracks;
            """;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // 3c. Create the new EF tables. DDL matches EF's InitialCreate output exactly.
        const string CreateSql = """
            CREATE TABLE "Settings" (
                "Key" TEXT NOT NULL CONSTRAINT "PK_Settings" PRIMARY KEY,
                "Value" TEXT NOT NULL
            );

            CREATE TABLE "Tracks" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Tracks" PRIMARY KEY,
                "Path" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "Artist" TEXT NOT NULL,
                "FileSize" INTEGER NOT NULL,
                "FileMtime" INTEGER NOT NULL,
                "DurationSecs" REAL NOT NULL
            );
            CREATE UNIQUE INDEX "IX_Tracks_Path" ON "Tracks" ("Path");

            CREATE TABLE "BasicAnalyses" (
                "TrackId" TEXT NOT NULL CONSTRAINT "PK_BasicAnalyses" PRIMARY KEY,
                "Data" BLOB NOT NULL,
                "FileMtime" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_BasicAnalyses_Tracks_TrackId" FOREIGN KEY ("TrackId") REFERENCES "Tracks" ("Id") ON DELETE CASCADE
            );

            CREATE TABLE "KeyAnalyses" (
                "TrackId" TEXT NOT NULL CONSTRAINT "PK_KeyAnalyses" PRIMARY KEY,
                "Data" BLOB NOT NULL,
                "FileMtime" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_KeyAnalyses_Tracks_TrackId" FOREIGN KEY ("TrackId") REFERENCES "Tracks" ("Id") ON DELETE CASCADE
            );

            CREATE TABLE "BpmOverrides" (
                "TrackId" TEXT NOT NULL CONSTRAINT "PK_BpmOverrides" PRIMARY KEY,
                "Multiplier" REAL NOT NULL,
                CONSTRAINT "FK_BpmOverrides_Tracks_TrackId" FOREIGN KEY ("TrackId") REFERENCES "Tracks" ("Id") ON DELETE CASCADE
            );

            CREATE TABLE "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );

            CREATE TABLE "__EFMigrationsLock" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK___EFMigrationsLock" PRIMARY KEY,
                "Timestamp" TEXT NOT NULL
            );
        """;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = CreateSql;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // 4. Copy tracks with assigned Guids (from in-memory list).
        await using (var ins = conn.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = """
                INSERT INTO "Tracks" ("Id","Path","Title","Artist","FileSize","FileMtime","DurationSecs")
                VALUES ($id, $p, $t, $a, $s, $m, $d);
            """;
            var pId = ins.Parameters.Add("$id", SqliteType.Text);
            var pP  = ins.Parameters.Add("$p",  SqliteType.Text);
            var pT  = ins.Parameters.Add("$t",  SqliteType.Text);
            var pA  = ins.Parameters.Add("$a",  SqliteType.Text);
            var pS  = ins.Parameters.Add("$s",  SqliteType.Integer);
            var pM  = ins.Parameters.Add("$m",  SqliteType.Integer);
            var pD  = ins.Parameters.Add("$d",  SqliteType.Real);

            foreach (var (fp, title, artist, size, mtime, dur) in trackRows)
            {
                pId.Value = pathToId[fp].ToString();
                pP.Value  = fp;
                pT.Value  = title;
                pA.Value  = artist;
                pS.Value  = size;
                pM.Value  = mtime;
                pD.Value  = dur;
                await ins.ExecuteNonQueryAsync(ct);
            }
        }

        // 5. Copy analyses, splitting by type. Orphans skipped.
        int orphans = 0;
        await using (var insBasic = conn.CreateCommand())
        await using (var insKey   = conn.CreateCommand())
        {
            insBasic.Transaction = insKey.Transaction = tx;
            insBasic.CommandText = """
                INSERT INTO "BasicAnalyses" ("TrackId","Data","FileMtime","CreatedAt")
                VALUES ($id, $d, $m, $c);
            """;
            insKey.CommandText = """
                INSERT INTO "KeyAnalyses" ("TrackId","Data","FileMtime","CreatedAt")
                VALUES ($id, $d, $m, $c);
            """;
            foreach (var cmd in new[] { insBasic, insKey })
            {
                cmd.Parameters.Add("$id", SqliteType.Text);
                cmd.Parameters.Add("$d",  SqliteType.Blob);
                cmd.Parameters.Add("$m",  SqliteType.Integer);
                cmd.Parameters.Add("$c",  SqliteType.Text);
            }

            foreach (var (fp, type, data, fileMtime, createdUnix) in analyses)
            {
                if (!pathToId.TryGetValue(fp, out var id)) { orphans++; continue; }

                var created = DateTimeOffset.FromUnixTimeSeconds(createdUnix).UtcDateTime
                    .ToString("yyyy-MM-dd HH:mm:ss");

                var cmd = type switch
                {
                    "basic" => insBasic,
                    "key"   => insKey,
                    _       => null,
                };
                if (cmd is null) { orphans++; continue; }
                cmd.Parameters["$id"].Value = id.ToString();
                cmd.Parameters["$d"].Value = data;
                cmd.Parameters["$m"].Value = fileMtime;
                cmd.Parameters["$c"].Value = created;
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }
        if (orphans > 0) Console.WriteLine($"[Bridge] dropped {orphans} orphan analysis row(s)");

        // 6. Copy bpm overrides.
        await using (var ins = conn.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = """
                INSERT INTO "BpmOverrides" ("TrackId","Multiplier") VALUES ($id, $m);
            """;
            ins.Parameters.Add("$id", SqliteType.Text);
            ins.Parameters.Add("$m",  SqliteType.Real);

            foreach (var (fp, mult) in bpmRows)
            {
                if (!pathToId.TryGetValue(fp, out var id)) continue;
                ins.Parameters["$id"].Value = id.ToString();
                ins.Parameters["$m"].Value  = mult;
                await ins.ExecuteNonQueryAsync(ct);
            }
        }

        // 7. Copy settings.
        await using (var ins = conn.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = """INSERT INTO "Settings" ("Key","Value") VALUES ($k, $v);""";
            ins.Parameters.Add("$k", SqliteType.Text);
            ins.Parameters.Add("$v", SqliteType.Text);

            foreach (var (key, value) in settingRows)
            {
                ins.Parameters["$k"].Value = key;
                ins.Parameters["$v"].Value = value;
                await ins.ExecuteNonQueryAsync(ct);
            }
        }

        // 9. Seed EF history; clear user_version (EF owns the schema cursor now).
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO "__EFMigrationsHistory" ("MigrationId","ProductVersion")
                VALUES ($mid, $pv);
                PRAGMA user_version = 0;
            """;
            cmd.Parameters.AddWithValue("$mid", InitialCreateMigrationId);
            cmd.Parameters.AddWithValue("$pv",  EfProductVersion);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        Console.WriteLine($"[Bridge] lift complete: {pathToId.Count} tracks → EF schema");
    }
}
