using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Sholto.App.Storage.Backup;
using Sholto.Data;

namespace Sholto.App.Storage;

/// <summary>
/// Single entry point for opening the Sholto library database. Replaces the
/// old <c>DatabaseStack.OpenAsync</c>. Migrates the database and returns a
/// <see cref="DatabaseStack"/> (built by the injected factory) exposing only ports; every operation grabs a
/// short-lived <see cref="SholtoDbContext"/> from a pooled factory with no
/// shared mutable state. An existing library with pending migrations is backed up
/// through <see cref="ILibraryBackup"/> first; if the backup fails, nothing is migrated.
/// </summary>
public sealed class SholtoStorage(
    IDatabaseStackFactory databases, ISholtoDbContextOptionsFactory contextOptions, ILibraryBackup backup, ILogFactory logs)
    : ISholtoStorage
{
    private readonly IDatabaseStackFactory _databases = databases;
    private readonly ISholtoDbContextOptionsFactory _contextOptions = contextOptions;
    private readonly ILibraryBackup _backup = backup;
    private readonly ILog _log = logs.Create("Storage");

    public async Task<DatabaseStack> OpenAsync(
        string? overridePath = null,
        CancellationToken ct = default)
    {
        var path = overridePath ?? DefaultDbPath();
        var existed = File.Exists(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var options = _contextOptions.Create(path);

        await using (var bootstrap = new SholtoDbContext(options))
        {
            if (existed)
                await BackUpBeforeUpgradeAsync(bootstrap, path, ct);
            await bootstrap.Database.MigrateAsync(ct);
        }

        _log.Info($"opened {path}");
        return _databases.Create(new PooledDbContextFactory<SholtoDbContext>(options));
    }

    private async Task BackUpBeforeUpgradeAsync(SholtoDbContext bootstrap, string path, CancellationToken ct)
    {
        var pending = await bootstrap.Database.GetPendingMigrationsAsync(ct);
        if (!pending.Any()) return;
        var lastApplied = (await bootstrap.Database.GetAppliedMigrationsAsync(ct)).LastOrDefault();
        // No applied migration means no Sholto library has been written to the file yet: nothing to save.
        if (lastApplied is null) return;
        _backup.BackUp(path, lastApplied);
    }

    /// <summary>Default library database path: <c>%LOCALAPPDATA%\sholto\library.db</c> on
    /// Windows, otherwise <c>~/.local/share/sholto/library.db</c>.</summary>
    public string DefaultDbPath()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sholto", "library.db");
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "sholto", "library.db");
    }
}
