using Microsoft.Data.Sqlite;

namespace Sholto.App.Storage.Backup;

/// <summary>
/// Copies the library with SQLite's online backup rather than a file copy: the library runs in WAL mode, so
/// committed pages can still sit in <c>library.db-wal</c>, which a plain copy of <c>library.db</c> would miss.
/// The copy goes to a temporary name first and is renamed into place once complete, so a half-written copy is
/// never one of the backups kept. Connections are unpooled so neither file stays open after the copy.
/// </summary>
public sealed class LibraryBackup : ILibraryBackup
{
    /// <summary>How many <c>.bak-*</c> backups are kept beside the library, newest by last write time.</summary>
    private const int BackupsKept = 3;
    private const string BackupInfix = ".bak-";
    private const string TemporaryInfix = ".tmp-bak-";
    private const string BackupSearchSuffix = "*";

    public void BackUp(string databasePath, string lastAppliedMigrationId)
    {
        var backupPath = databasePath + BackupInfix + lastAppliedMigrationId;
        var temporaryPath = databasePath + TemporaryInfix + lastAppliedMigrationId;
        try
        {
            Copy(databasePath, temporaryPath);
            File.Move(temporaryPath, backupPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw new IOException($"could not back up the library to {backupPath}; no upgrade was run", ex);
        }
        DeleteOldBackups(databasePath);
    }

    private void Copy(string sourcePath, string destinationPath)
    {
        using var source = new SqliteConnection(ConnectionString(sourcePath, SqliteOpenMode.ReadWrite));
        using var destination = new SqliteConnection(ConnectionString(destinationPath, SqliteOpenMode.ReadWriteCreate));
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }

    private string ConnectionString(string path, SqliteOpenMode mode) =>
        new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false }.ToString();

    private void DeleteOldBackups(string databasePath)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        var stale = directory
            .EnumerateFiles(Path.GetFileName(databasePath) + BackupInfix + BackupSearchSuffix)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Skip(BackupsKept);
        foreach (var file in stale)
            file.Delete();
    }
}
