using Sholto.App.Lifecycle;
using Microsoft.Extensions.Options;
using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Harmony;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stages;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Stores;
using Sholto.App.Analysis.Analyzers.Vocals;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Data;

namespace Sholto.Host;

/// <summary>
/// Builds the <see cref="AnalysisStack"/>. The key/grid/basic stores are deferred
/// stores over projections of <see cref="ILibraryDatabase.Opened"/>.
///
/// <para>The <see cref="IKeyFactory"/> is handed in by the composition root because the
/// storage factory needs the same instance.</para>
/// </summary>
public sealed class AnalysisStackFactory(IKeyFactory keyFactory, IOptions<WaveformBandOptions> waveformBands) : IAnalysisStackFactory
{
    private readonly IKeyFactory _keyFactory = keyFactory;
    private readonly IOptions<WaveformBandOptions> _waveformBands = waveformBands;

    // Pass 3v (2026-09-12): key/segment/vocal/beatgrid used to be static classes
    // (KeyAnalyzer, PhraseSectionAnalyzer, VocalRegionAnalyzer, Beatgrid);
    // each is now composed once here, hand-wired like every other collaborator on
    // the composition root, so a test/Bench harness can substitute a fake instead of
    // the real compute.
    //
    // Takes only `beats` — the trunk hoist (2026-09-12) verified nothing this class
    // builds ever reads a raw stem step (it used to accept one, alongside `beats`,
    // for symmetry with the bundle it's built from; the parameter was discarded).
    // DeckFactory and MainViewModel read stems/demucsCache straight off
    // ExternalToolStack instead.
    public AnalysisStack Build(IBeatAnalysisStep beats, ILibraryDatabase libraryDatabase)
    {
        // Project the opened database into each store port without blocking; a null
        // database (open failed) projects to null, which the deferred stores treat as
        // "no persistence".
        var opened = libraryDatabase.Opened;
        async Task<IBasicAnalysisStore?> Basic() => (await opened)?.BasicAnalyses;
        async Task<IKeyAnalysisStore?> Key() => (await opened)?.KeyAnalyses;
        async Task<IGridAdjustmentStore?> Grid() => (await opened)?.GridAdjustments;
        var deferredBasicCache = new DeferredBasicAnalysisStore(Basic());
        var deferredKeyCache = new DeferredKeyAnalysisStore(Key());
        var deferredGridCache = new DeferredGridAdjustmentStore(Grid());

        var reporter = new AnalysisReporter(new[] { AnalysisSteps.Beats });

        var beatgridFactory = new BeatgridFactory();
        var peaks = new WaveformPeakAnalyzer(new WaveformBandSplitterFactory(new BiquadFactory(), _waveformBands), new WaveformPeaksFactory());
        var keyAnalyzer = new KeyAnalyzer();
        var sectionOptions = new PhraseSectionOptions();
        var songSegments = new PhraseSectionAnalyzer(
            new BarFeatureExtractor(sectionOptions), new PhraseSectionLabeler(sectionOptions), sectionOptions);
        var vocalRegions = new VocalRegionAnalyzer();
        var beatgridAnalyzer = new BeatgridAnalyzer(beatgridFactory);

        var basicAnalysisStage = new BasicAnalysisStage(beatAnalyzer: beats, peakAnalyzer: peaks, beatgridAnalyzer: beatgridAnalyzer, reporter: reporter);

        var provider = new CachingAnalysisProvider(new DbAnalysisProvider(
            new StagedAnalysisProvider(basicAnalysisStage),
            store: deferredBasicCache));

        return new AnalysisStack(
            reporter,
            provider,
            deferredKeyCache,
            deferredGridCache,
            peaks,
            _keyFactory,
            keyAnalyzer,
            songSegments,
            vocalRegions,
            beatgridFactory);
    }
}
