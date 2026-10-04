using Microsoft.EntityFrameworkCore;
using Sholto.App.Analysis.Harmony;
using Sholto.App.Storage;

namespace Sholto.App.Storage.Tests;

/// <summary>Opens a library database through the real <see cref="SholtoStorage"/>, so
/// migrations run exactly as they do in the app, and hands back the raw context factory
/// the tests seed and inspect directly. <see cref="SholtoStorage.OpenAsync"/> only
/// exposes ports, so this captures the factory on its way into
/// <see cref="IDatabaseStackFactory"/>.</summary>
internal sealed class ContextCapturingStorage : IDatabaseStackFactory
{
    private readonly IDatabaseStackFactory _real = new DatabaseStackFactory(new KeyFactory());
    private IDbContextFactory<SholtoDbContext>? _contexts;

    public async Task<IDbContextFactory<SholtoDbContext>> OpenAsync(string path)
    {
        await new SholtoStorage(this, new SholtoDbContextOptionsFactory()).OpenAsync(path);
        return _contexts ?? throw new InvalidOperationException("SholtoStorage did not build a database stack.");
    }

    DatabaseStack IDatabaseStackFactory.Create(IDbContextFactory<SholtoDbContext> contexts)
    {
        _contexts = contexts;
        return _real.Create(contexts);
    }
}
