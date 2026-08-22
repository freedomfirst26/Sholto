using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sholto.Storage;

// Only used by `dotnet ef migrations` / `dotnet ef database update` at design time.
// In production the context is built inside SholtoStorage.OpenAsync.
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SholtoDbContext>
{
    public SholtoDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SholtoDbContext>()
            .UseSqlite("Data Source=design-time-placeholder.db")
            .Options;
        return new SholtoDbContext(options);
    }
}
