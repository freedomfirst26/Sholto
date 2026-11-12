namespace Sholto.App.Storage.Backup;

/// <summary>Saves a complete copy of the library database beside it before a schema upgrade.</summary>
public interface ILibraryBackup
{
    /// <summary>Writes a complete copy of <paramref name="databasePath"/>, committed WAL pages included, to
    /// <c>&lt;database&gt;.bak-&lt;lastAppliedMigrationId&gt;</c> beside it, then keeps only the newest backups.
    /// Throws an <see cref="IOException"/> naming the backup path when the copy cannot be written.</summary>
    void BackUp(string databasePath, string lastAppliedMigrationId);
}
