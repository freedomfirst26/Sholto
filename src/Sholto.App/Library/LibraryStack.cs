using Sholto.App.Analysis.Stores;
using Sholto.App.Library.Catalog;
using Sholto.App.Library.Tags;

namespace Sholto.App.Library;

/// <summary>The persistence ports a library scan reads and writes through, handed to
/// <see cref="ILibrarySession.ScanAsync"/> by the lifecycle once the database is open.</summary>
public sealed class LibraryStack(
    ITrackCatalog tracks,
    ITagService tags,
    IBasicAnalysisStore basicAnalyses,
    ITempoMultiplierStore tempoMultipliers)
{
    public ITrackCatalog Tracks { get; } = tracks;
    public ITagService Tags { get; } = tags;
    public IBasicAnalysisStore BasicAnalyses { get; } = basicAnalyses;
    public ITempoMultiplierStore TempoMultipliers { get; } = tempoMultipliers;
}
