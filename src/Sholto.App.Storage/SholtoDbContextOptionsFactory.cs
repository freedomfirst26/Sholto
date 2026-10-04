using Microsoft.EntityFrameworkCore;

namespace Sholto.App.Storage;

/// <summary>
/// Builds the Sqlite options, including the pragma connection interceptor, for the
/// library database. The interceptor never leaves this assembly.
/// </summary>
public sealed class SholtoDbContextOptionsFactory : ISholtoDbContextOptionsFactory
{
    DbContextOptions<SholtoDbContext> ISholtoDbContextOptionsFactory.Create(string dbPath) =>
        // No Cache=Shared: shared-cache serialises at table granularity and
        // returns SQLITE_LOCKED immediately on contention (not retryable by a
        // busy handler). WAL + busy_timeout (see SqlitePragmaInterceptor) is the
        // correct config for concurrent short-lived pooled connections.
        new DbContextOptionsBuilder<SholtoDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;
}
