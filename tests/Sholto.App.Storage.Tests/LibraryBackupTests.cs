using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sholto.App.Storage;
using Sholto.App.Storage.Backup;

namespace Sholto.App.Storage.Tests;

/// <summary>Opening a library through <see cref="SholtoStorage"/> backs it up before any migration runs.</summary>
public class LibraryBackupTests
{
    private const string WalOnlyTrackPath = "/wal-only.flac";
    private const string WalOnlyTrackId = "0b5c4d1e-3f2a-4b6c-8d7e-9f0a1b2c3d4e";
    private const string SchemaSql =
        "SELECT group_concat(sql, ';') FROM (SELECT sql FROM sqlite_master WHERE sql IS NOT NULL ORDER BY name);";
    private const string BackupPattern = "library.db.bak-*";
    private const int BackupsKept = 3;
    private const int OlderBackups = 4;

    [Fact]
    public async Task Opening_a_library_one_migration_behind_backs_it_up_with_its_WAL_rows_first()
    {
        using var temp = new TempSqliteDatabase();
        var (previous, latest) = await CreateOneMigrationBehindAsync(temp.Path);

        // A row committed but not checkpointed: it lives only in library.db-wal while this connection is open.
        await using var holder = OpenUnpooled(temp.Path);
        Execute(holder, "PRAGMA wal_autocheckpoint=0;");
        Execute(holder, "INSERT INTO Tracks (Id, Path, Title, Artist, FileSize, FileMtime, DurationSecs) " +
            $"VALUES ('{WalOnlyTrackId}', '{WalOnlyTrackPath}', 'T', 'A', 0, 0, 0);");
        Assert.Equal(0, CountWalOnlyTrackInMainFileAlone(temp.Path));
        var schemaBefore = Scalar<string>(holder, SchemaSql);

        await new ContextCapturingStorage().OpenAsync(temp.Path);

        var backupPath = temp.Path + ".bak-" + previous;
        Assert.True(File.Exists(backupPath));
        await using var backup = OpenUnpooled(backupPath);
        Assert.Equal(1, Scalar(backup, $"SELECT COUNT(*) FROM Tracks WHERE Path = '{WalOnlyTrackPath}';"));
        Assert.Equal(previous, Scalar<string>(backup,
            "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1;"));
        Assert.Equal(0, Scalar(backup, $"SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = '{latest}';"));
        Assert.Equal(schemaBefore, Scalar<string>(backup, SchemaSql));
        Assert.NotEqual(schemaBefore, Scalar<string>(holder, SchemaSql));
    }

    [Fact]
    public async Task Only_the_three_newest_backups_remain_after_an_upgrade()
    {
        using var temp = new TempSqliteDatabase();
        var (previous, _) = await CreateOneMigrationBehindAsync(temp.Path);
        var directory = Path.GetDirectoryName(temp.Path)!;
        var oldest = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var age = 0; age < OlderBackups; age++)
        {
            var older = Path.Combine(directory, $"library.db.bak-older{age}");
            await File.WriteAllTextAsync(older, "older backup");
            File.SetLastWriteTimeUtc(older, oldest.AddDays(age));
        }

        await new ContextCapturingStorage().OpenAsync(temp.Path);

        var remaining = Directory.GetFiles(directory, BackupPattern).Select(Path.GetFileName).Order().ToArray();
        Assert.Equal(BackupsKept, remaining.Length);
        Assert.Equal(new[] { "library.db.bak-" + previous, "library.db.bak-older2", "library.db.bak-older3" }
            .Order().ToArray(), remaining);
    }

    [Fact]
    public async Task A_backup_that_cannot_be_written_fails_the_open_and_runs_no_migration()
    {
        using var temp = new TempSqliteDatabase();
        var (previous, _) = await CreateOneMigrationBehindAsync(temp.Path);
        var applied = await AppliedMigrationsAsync(temp.Path);
        var backupPath = temp.Path + ".bak-" + previous;
        Directory.CreateDirectory(backupPath);

        var error = await Assert.ThrowsAsync<IOException>(() => new ContextCapturingStorage().OpenAsync(temp.Path));

        Assert.Contains(backupPath, error.Message);
        Assert.Equal(applied, await AppliedMigrationsAsync(temp.Path));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(temp.Path)!, "library.db.tmp-bak-*"));
    }

    [Fact]
    public async Task Opening_a_brand_new_library_writes_no_backup()
    {
        using var temp = new TempSqliteDatabase();

        await new ContextCapturingStorage().OpenAsync(temp.Path);

        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(temp.Path)!, BackupPattern));
    }

    [Fact]
    public async Task Opening_an_up_to_date_library_writes_no_backup()
    {
        using var temp = new TempSqliteDatabase();
        await new ContextCapturingStorage().OpenAsync(temp.Path);
        SqliteConnection.ClearAllPools();

        await new ContextCapturingStorage().OpenAsync(temp.Path);

        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(temp.Path)!, BackupPattern));
    }

    /// <summary>Migrates a new library to the second-newest migration and checkpoints it, so its schema is in
    /// the main file. Returns the second-newest and newest migration ids.</summary>
    private async Task<(string Previous, string Latest)> CreateOneMigrationBehindAsync(string path)
    {
        await using (var db = new SholtoDbContext(((ISholtoDbContextOptionsFactory)new SholtoDbContextOptionsFactory()).Create(path)))
        {
            var all = db.Database.GetMigrations().ToArray();
            await db.GetService<IMigrator>().MigrateAsync(all[^2]);
            SqliteConnection.ClearAllPools();
            return (all[^2], all[^1]);
        }
    }

    private async Task<string[]> AppliedMigrationsAsync(string path)
    {
        await using var db = new SholtoDbContext(((ISholtoDbContextOptionsFactory)new SholtoDbContextOptionsFactory()).Create(path));
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        SqliteConnection.ClearAllPools();
        return applied;
    }

    /// <summary>Copies only the main database file — no WAL — and counts the WAL-only row there.</summary>
    private long CountWalOnlyTrackInMainFileAlone(string path)
    {
        var mainOnly = path + ".main-only";
        File.Copy(path, mainOnly);
        try
        {
            using var copy = OpenUnpooled(mainOnly);
            return Scalar(copy, $"SELECT COUNT(*) FROM Tracks WHERE Path = '{WalOnlyTrackPath}';");
        }
        finally
        {
            foreach (var file in new[] { mainOnly, mainOnly + "-wal", mainOnly + "-shm" })
                if (File.Exists(file)) File.Delete(file);
        }
    }

    private SqliteConnection OpenUnpooled(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    private void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private long Scalar(SqliteConnection connection, string sql) => Scalar<long>(connection, sql);

    private T Scalar<T>(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)command.ExecuteScalar()!;
    }
}
