using Microsoft.EntityFrameworkCore;
using Sholto.App.Analysis.Harmony;
using Sholto.App.Library.Tags;
using Sholto.Data;

namespace Sholto.App.Storage;

/// <summary>
/// Builds the serializers and the Sqlite/EF-backed ports of a <see cref="DatabaseStack"/>
/// from a context factory. The concrete implementations never leave this assembly.
/// </summary>
public sealed class DatabaseStackFactory(IKeyFactory keyFactory, ILogFactory logs) : IDatabaseStackFactory
{
    private readonly IKeyFactory _keyFactory = keyFactory;
    private readonly ILogFactory _logs = logs;

    DatabaseStack IDatabaseStackFactory.Create(IDbContextFactory<SholtoDbContext> contexts)
    {
        var basicAnalysisSerializer = new BasicAnalysisSerializer();
        var keySerializer = new KeyAnalysisSerializer(_keyFactory);
        var dbLog = _logs.Create("DB");
        return new DatabaseStack(
            tags: new TagService(contexts, new TagNameNormalizer()),
            crates: new CrateService(contexts),
            markers: new MarkerService(contexts),
            hotCues: new HotCueService(contexts),
            tracks: new SqliteTrackCatalog(contexts),
            settings: new SqliteSettingsStore(contexts),
            tempoMultipliers: new SqliteTempoMultiplierStore(contexts),
            basicAnalyses: new BasicAnalysisStore(contexts, basicAnalysisSerializer, dbLog),
            keyAnalyses: new SqliteKeyAnalysisStore(contexts, keySerializer, dbLog),
            gridAdjustments: new SqliteGridAdjustmentStore(contexts));
    }
}
