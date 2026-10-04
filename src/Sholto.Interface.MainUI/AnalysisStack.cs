using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Analyzers.Vocals;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Analysis.Harmony;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stores;

namespace Sholto.Interface.MainUI;

/// <summary>
/// The analysis-side collaborators: the shared reporter, the algorithm instances
/// (key/segment/vocal/beatgrid), and the analysis-provider + key/grid stores every
/// Deck reads through. Composed once, at startup, over the library database's
/// <c>Opened</c> task: the key/grid/basic stores are deferred stores that await it, so
/// they are real, never-null objects before the window has painted and nothing needs
/// attaching afterwards. A database that failed to open means no persistence.
/// </summary>
/// <param name="reporter">Shared reporter, handed to DeckFactory as a real constructor argument — the same instance MainViewModel/ViewModelStack also read through.</param>
/// <param name="provider">Cache-aside lookup for BasicAnalysis: in-process memory tier over a DB tier (over the deferred DB store) over compute-only AnalysisProvider.</param>
/// <param name="keyStore">Deferred key-analysis store — forwards once the database has opened.</param>
/// <param name="gridStore">Deferred grid-adjustment store — forwards once the database has opened. Holds the user's manual beat-grid nudges.</param>
public sealed class AnalysisStack(
    IAnalysisReporter reporter,
    IAnalysisProvider provider,
    IKeyAnalysisStore keyStore,
    IGridAdjustmentStore gridStore,
    IWaveformPeakAnalyzer peaks,
    IKeyFactory keyFactory,
    IKeyAnalyzer keyAnalyzer,
    ISongSegmentAnalyzer songSegments,
    IVocalRegionAnalyzer vocalRegions,
    IBeatgridFactory beatgridFactory)
{
    public IAnalysisReporter Reporter { get; } = reporter;
    public IAnalysisProvider Provider { get; } = provider;
    public IKeyAnalysisStore KeyStore { get; } = keyStore;
    public IGridAdjustmentStore GridStore { get; } = gridStore;
    public IWaveformPeakAnalyzer Peaks { get; } = peaks;
    public IKeyFactory KeyFactory { get; } = keyFactory;
    public IKeyAnalyzer KeyAnalyzer { get; } = keyAnalyzer;
    public ISongSegmentAnalyzer SongSegments { get; } = songSegments;
    public IVocalRegionAnalyzer VocalRegions { get; } = vocalRegions;
    public IBeatgridFactory BeatgridFactory { get; } = beatgridFactory;
}
