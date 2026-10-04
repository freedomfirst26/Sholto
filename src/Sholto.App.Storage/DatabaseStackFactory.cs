using Microsoft.EntityFrameworkCore;
using Sholto.App.Analysis.Harmony;
using Sholto.App.Library.Tags;

namespace Sholto.App.Storage;

/// <summary>
/// Builds the serializers and the Sqlite/EF-backed ports of a <see cref="DatabaseStack"/>
/// from a context factory. The concrete implementations never leave this assembly.
/// </summary>
public sealed class DatabaseStackFactory(IKeyFactory keyFactory) : IDatabaseStackFactory
{
    DatabaseStack IDatabaseStackFactory.Create(IDbContextFactory<SholtoDbContext> contexts)
    {
        var basicAnalysisSerializer = new BasicAnalysisSerializer();
        var keySerializer = new KeyAnalysisSerializer(keyFactory);
        return new DatabaseStack(
            tags: new TagService(contexts, new TagNameNormalizer()),
            crates: new CrateService(contexts),
            markers: new MarkerService(contexts),
            tracks: new SqliteTrackCatalog(contexts),
            settings: new SqliteSettingsStore(contexts),
            tempoMultipliers: new SqliteTempoMultiplierStore(contexts),
            basicAnalyses: new BasicAnalysisStore(contexts, basicAnalysisSerializer),
            keyAnalyses: new SqliteKeyAnalysisStore(contexts, keySerializer),
            gridAdjustments: new SqliteGridAdjustmentStore(contexts));
    }
}
