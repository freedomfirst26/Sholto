using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Sholto.Storage;

/// <summary>
/// Applies the SQLite pragmas that make a pooled, multi-connection desktop app
/// safe. Runs on every connection the factory opens.
///
/// <para><b>Why this exists.</b> The library scan, key/beat analysis, and tag
/// writes all grab short-lived contexts from the same pooled factory and can
/// write concurrently. Under the default rollback journal that overlap throws
/// <c>SQLITE_BUSY</c>/<c>SQLITE_LOCKED</c> ("database is locked"), which the
/// analysis back-fill was silently swallowing — so a track re-analysed during
/// the startup scan never persisted and got recomputed on every launch.</para>
///
/// <list type="bullet">
///   <item><c>journal_mode=WAL</c> — readers never block the single writer, and
///     the setting persists in the database header. Cheap to re-assert per open.</item>
///   <item><c>busy_timeout</c> — a contended writer waits and retries for up to
///     this many ms instead of failing immediately.</item>
///   <item><c>synchronous=NORMAL</c> — the safe, standard pairing with WAL.</item>
/// </list>
/// </summary>
public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    private const int BusyTimeoutMs = 5000;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => Apply(connection);

    public override Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken ct = default)
    {
        Apply(connection);
        return Task.CompletedTask;
    }

    private static void Apply(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            $"PRAGMA busy_timeout={BusyTimeoutMs}; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        cmd.ExecuteNonQuery();
    }
}
