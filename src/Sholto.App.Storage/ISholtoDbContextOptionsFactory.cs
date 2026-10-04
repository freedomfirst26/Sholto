using Microsoft.EntityFrameworkCore;

namespace Sholto.App.Storage;

/// <summary>
/// Builds the Sqlite options for the library database. Public so <see cref="SholtoStorage"/>
/// can take it; its member is internal because it names the internal
/// <see cref="SholtoDbContext"/>, and <see cref="SholtoDbContextOptionsFactory"/>
/// implements it explicitly.
/// </summary>
public interface ISholtoDbContextOptionsFactory
{
    internal DbContextOptions<SholtoDbContext> Create(string dbPath);
}
