using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Sholto.App.Storage;

/// <summary>
/// Single entry point for opening the Sholto library database. Replaces the
/// old <c>DatabaseStack.OpenAsync</c>. Migrates the database and returns a
/// <see cref="DatabaseStack"/> (built by the injected factory) exposing only ports; every operation grabs a
/// short-lived <see cref="SholtoDbContext"/> from a pooled factory with no
/// shared mutable state.
/// </summary>
public sealed class SholtoStorage(IDatabaseStackFactory databases, ISholtoDbContextOptionsFactory contextOptions)
{
    private readonly IDatabaseStackFactory _databases = databases;
    private readonly ISholtoDbContextOptionsFactory _contextOptions = contextOptions;

    public async Task<DatabaseStack> OpenAsync(
        string? overridePath = null,
        CancellationToken ct = default)
    {
        var path = overridePath ?? DefaultDbPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var options = _contextOptions.Create(path);

        await using (var bootstrap = new SholtoDbContext(options))
            await bootstrap.Database.MigrateAsync(ct);

        Console.WriteLine($"[Storage] opened {path}");
        return _databases.Create(new PooledDbContextFactory<SholtoDbContext>(options));
    }

    public string DefaultDbPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "sholto", "library.db");
}
