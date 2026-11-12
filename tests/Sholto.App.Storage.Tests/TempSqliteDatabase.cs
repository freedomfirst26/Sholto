using Microsoft.Data.Sqlite;

namespace Sholto.App.Storage.Tests;

/// <summary>Owns one throwaway SQLite database file for a test. <see cref="Path"/> is a
/// unique <c>library.db</c> in its own fresh directory under the temp folder.
/// <see cref="Dispose"/> first releases every pooled connection — on Windows a pooled
/// connection keeps the file locked, so deleting it would fail — then deletes the
/// database and its WAL side files (<c>-wal</c>, <c>-shm</c>) and the directory.</summary>
internal sealed class TempSqliteDatabase : IDisposable
{
    private readonly string _directory;

    public TempSqliteDatabase()
    {
        _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sholto-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        Path = System.IO.Path.Combine(_directory, "library.db");
    }

    /// <summary>The database file path; nothing exists there until a test opens it.</summary>
    public string Path { get; }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { Path, Path + "-wal", Path + "-shm" })
            if (File.Exists(file)) File.Delete(file);
        if (Directory.Exists(_directory) && !Directory.EnumerateFileSystemEntries(_directory).Any())
            Directory.Delete(_directory);
    }
}
