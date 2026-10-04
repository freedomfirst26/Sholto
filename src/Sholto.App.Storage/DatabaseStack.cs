using Sholto.App.Analysis.Stores;
using Sholto.App.Library.Catalog;
using Sholto.App.Library.Crates;
using Sholto.App.Library.Markers;
using Sholto.App.Library.Tags;
using Sholto.App.Settings;

namespace Sholto.App.Storage;

/// <summary>
/// An opened library database, seen only through the consumer-owned ports. Built by
/// <see cref="DatabaseStackFactory"/>; the concrete Sqlite/EF implementations never
/// leave this assembly.
/// </summary>
public sealed class DatabaseStack(
    ITagService tags,
    ICrateService crates,
    IMarkerService markers,
    ITrackCatalog tracks,
    ISettingsStore settings,
    ITempoMultiplierStore tempoMultipliers,
    IBasicAnalysisStore basicAnalyses,
    IKeyAnalysisStore keyAnalyses,
    IGridAdjustmentStore gridAdjustments)
{
    public ITagService Tags { get; } = tags;
    public ICrateService Crates { get; } = crates;
    public IMarkerService Markers { get; } = markers;
    public ITrackCatalog Tracks { get; } = tracks;
    public ISettingsStore Settings { get; } = settings;
    public ITempoMultiplierStore TempoMultipliers { get; } = tempoMultipliers;
    public IBasicAnalysisStore BasicAnalyses { get; } = basicAnalyses;
    public IKeyAnalysisStore KeyAnalyses { get; } = keyAnalyses;
    public IGridAdjustmentStore GridAdjustments { get; } = gridAdjustments;
}
