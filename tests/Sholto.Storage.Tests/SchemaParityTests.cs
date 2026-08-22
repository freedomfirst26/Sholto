using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sholto.Storage;

namespace Sholto.Storage.Tests;

public class SchemaParityTests
{
    private static string TempPath() => Path.Combine(
        Path.GetTempPath(), $"sholto-parity-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task Bridge_output_matches_InitialCreate_schema()
    {
        var bridged = TempPath();
        var efOnly  = TempPath();
        try
        {
            await V3FixtureBuilder.BuildAsync(bridged);
            await DatabaseBridge.RunIfNeededAsync(bridged, default);

            // Simulate production startup: after the bridge seeds InitialCreate,
            // MigrateAsync applies any subsequent migrations (e.g. AddTrackTags).
            var bridgedOptions = new DbContextOptionsBuilder<SholtoDbContext>()
                .UseSqlite($"Data Source={bridged}")
                .Options;
            await using (var ctx = new SholtoDbContext(bridgedOptions))
                await ctx.Database.MigrateAsync();

            var options = new DbContextOptionsBuilder<SholtoDbContext>()
                .UseSqlite($"Data Source={efOnly}")
                .Options;
            await using (var ctx = new SholtoDbContext(options))
                await ctx.Database.MigrateAsync();

            var bridgedSchema = await DumpSchemaAsync(bridged);
            var efSchema      = await DumpSchemaAsync(efOnly);

            Assert.Equal(NormaliseSchema(efSchema), NormaliseSchema(bridgedSchema));
        }
        finally
        {
            foreach (var p in new[] { bridged, efOnly })
                if (File.Exists(p)) File.Delete(p);
            foreach (var b in Directory.GetFiles(Path.GetTempPath(), "sholto-parity-*.pre-efcore-backup-*"))
                File.Delete(b);
        }
    }

    private static async Task<List<(string Type, string Name, string Sql)>> DumpSchemaAsync(string path)
    {
        var rows = new List<(string, string, string)>();
        await using var conn = new SqliteConnection($"Data Source={path}");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT type, name, COALESCE(sql, '') FROM sqlite_master
            WHERE name NOT LIKE 'sqlite_%'
            ORDER BY type, name;
        """;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            rows.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
        return rows;
    }

    private static List<(string Type, string Name, string Sql)> NormaliseSchema(
        List<(string Type, string Name, string Sql)> rows)
    {
        return rows
            .Where(x => !x.Name.StartsWith("sqlite_autoindex_", StringComparison.Ordinal))
            .Select(x => (x.Type, x.Name,
                System.Text.RegularExpressions.Regex.Replace(x.Sql, @"\s+", " ").TrimEnd(';').Trim()))
            .ToList();
    }
}
