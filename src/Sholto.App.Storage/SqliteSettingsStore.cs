using Microsoft.EntityFrameworkCore;
using Sholto.App.Settings;
using Sholto.Storage.Entities;

namespace Sholto.App.Storage;

/// <summary>Settings backed by the <c>settings</c> table. Short-lived context per call.</summary>
internal sealed class SqliteSettingsStore(IDbContextFactory<SholtoDbContext> factory) : ISettingsStore
{
    private readonly IDbContextFactory<SholtoDbContext> _factory = factory;

    public async Task<string?> GetAsync(string key)
    {
        await using var db = _factory.CreateDbContext();
        return (await db.Settings.FindAsync(key))?.Value;
    }

    public async Task SetAsync(string key, string value)
    {
        await using var db = _factory.CreateDbContext();
        var row = await db.Settings.FindAsync(key);
        if (row is null) db.Settings.Add(new Setting { Key = key, Value = value });
        else row.Value = value;
        await db.SaveChangesAsync();
    }
}
