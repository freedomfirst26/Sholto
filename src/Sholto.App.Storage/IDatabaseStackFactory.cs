using Microsoft.EntityFrameworkCore;

namespace Sholto.App.Storage;

/// <summary>
/// Builds everything <see cref="SholtoStorage"/> would otherwise construct itself: the
/// opened <see cref="DatabaseStack"/> over a context factory.
/// Public so <see cref="SholtoStorage"/> can take it; its members are internal because
/// they name the internal <see cref="SholtoDbContext"/>, and <see cref="DatabaseStackFactory"/>
/// implements them explicitly.
/// </summary>
public interface IDatabaseStackFactory
{
    internal DatabaseStack Create(IDbContextFactory<SholtoDbContext> contexts);
}
