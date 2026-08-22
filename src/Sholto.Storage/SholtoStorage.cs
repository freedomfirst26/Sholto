using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Sholto.Storage;

/// <summary>
/// Single entry point for opening the Sholto library database. Replaces the
/// old <c>SholtoDatabase.OpenAsync</c>. Returns a pooled context factory so
/// every operation can grab a short-lived <see cref="SholtoDbContext"/> with
/// no shared mutable state.
/// </summary>
public static class SholtoStorage
{
    public static async Task<IDbContextFactory<SholtoDbContext>> OpenAsync(
        string? overridePath = null,
        CancellationToken ct = default)
    {
        var path = overridePath ?? DefaultDbPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await DatabaseBridge.RunIfNeededAsync(path, ct);

        // No Cache=Shared: shared-cache serialises at table granularity and
        // returns SQLITE_LOCKED immediately on contention (not retryable by a
        // busy handler). WAL + busy_timeout (see SqlitePragmaInterceptor) is the
        // correct config for concurrent short-lived pooled connections.
        var options = new DbContextOptionsBuilder<SholtoDbContext>()
            .UseSqlite($"Data Source={path}")
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        await using (var bootstrap = new SholtoDbContext(options))
            await bootstrap.Database.MigrateAsync(ct);

        Console.WriteLine($"[Storage] opened {path}");
        return new PooledDbContextFactory<SholtoDbContext>(options);
    }

    public static string DefaultDbPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "sholto", "library.db");
}
