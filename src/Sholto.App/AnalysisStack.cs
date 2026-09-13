using Microsoft.EntityFrameworkCore;
using Sholto.Analysis;
using Sholto.Analysis.Analyzers;
using Sholto.Analysis.Analyzers.Beats;
using Sholto.Analysis.Harmony;
using Sholto.Analysis.Analyzers.Keys;
using Sholto.Analysis.Processing;
using Sholto.Analysis.Reporting;
using Sholto.Analysis.Analyzers.Segments;
using Sholto.Analysis.Stores;
using Sholto.Analysis.Analyzers.Vocals;
using Sholto.Analysis.Analyzers.Waveform;
using Sholto.Storage;

namespace Sholto.App;

/// <summary>
/// The analysis-side collaborators: the shared reporter, the algorithm instances
/// (key/segment/vocal/beatgrid), and the analysis-provider + key/grid stores every
/// Deck reads through. Composed once, at startup, before the database exists — see
/// <see cref="AttachDatabase"/> for how the DB-backed tiers arrive later without
/// reassigning anything on <c>Deck</c> itself.
///
/// <para><b>Why the key/grid stores are "switchable."</b> <see cref="KeyStore"/> and
/// <see cref="GridStore"/> are real, never-null objects from the moment this class is
/// constructed, so <c>DeckFactory</c> can be handed real constructor arguments before
/// the window has even painted. Each one starts as a null-object wrapper and is
/// pointed at the real, Sqlite-backed store only once <see cref="AttachDatabase"/>
/// runs — every Deck already built against the wrapper picks up the swap on its next
/// lookup; nothing on Deck itself is ever reassigned.</para>
///
/// <para><b>RISK — read this before touching call order.</b> If
/// <see cref="AttachDatabase"/> is never called (composition root reordered, the call
/// dropped in a refactor, the DB open failing silently upstream), analysis quietly
/// degrades to memory-only: the in-process cache in <see cref="CachingAnalysisProvider"/>
/// still works, but nothing persists across restarts. The sharpest edge is
/// <see cref="GridStore"/> — the user's manual beat-grid nudges are the system of
/// record for a track's grid; nothing recomputes them from scratch. Lose the Sqlite
/// attach and every nudge silently stops persisting, with no error anywhere: playback
/// looks identical until the next launch, when the grid is back to the untouched
/// analysis. This risk already existed before this class — App used to build the
/// same stack in two places ~120 lines apart and attach separately — collecting both
/// steps here shortens the gap between them, it does not close it.</para>
/// </summary>
public sealed class AnalysisStack
{
    // The DB-backed tier each switchable upgrades to — built fresh inside
    // AttachDatabase, not stored as fields: nothing outside that one call needs to
    // reach them directly, and keeping them as fields was the exact anti-pattern
    // this class replaces (see the class doc above).
    private readonly SwitchableBasicAnalysisStore _switchableBasicCache = new();
    private readonly SwitchableKeyAnalysisStore _switchableKeyCache = new();
    private readonly SwitchableGridAdjustmentStore _switchableGridCache = new();

    /// <summary>Shared reporter, handed to DeckFactory as a real constructor
    /// argument (not a closure) — the same instance MainViewModel/ApplicationLeaves
    /// also read through.</summary>
    public IAnalysisReporter Reporter { get; }

    /// <summary>Cache-aside lookup for BasicAnalysis: an in-process memory tier
    /// (<see cref="CachingAnalysisProvider"/>) wraps a DB tier
    /// (<see cref="DbAnalysisProvider"/>, over the switchable DB cache — see
    /// <see cref="AttachDatabase"/>), which wraps compute-only
    /// <see cref="AnalysisProvider"/>.</summary>
    public IAnalysisProvider Provider { get; }

    /// <summary>Switchable key-analysis store — memory-only until
    /// <see cref="AttachDatabase"/> upgrades it.</summary>
    public IKeyAnalysisStore KeyStore { get; }

    /// <summary>Switchable grid-adjustment store — memory-only until
    /// <see cref="AttachDatabase"/> upgrades it. Holds the user's manual beat-grid
    /// nudges once attached; see the class doc's RISK note.</summary>
    public IGridAdjustmentStore GridStore { get; }

    public IWaveformPeakAnalyzer Peaks { get; }
    public IHarmonicKeys HarmonicKeys { get; }
    public IKeyAnalyzer KeyAnalyzer { get; }
    public ISongSegmentAnalyzer SongSegments { get; }
    public IVocalRegionAnalyzer VocalRegions { get; }
    public IBeatgridFitter BeatgridFitter { get; }

    // Pass 3v (2026-09-12): key/segment/vocal/beatgrid used to be static classes
    // (KeyAnalyzer, SongSegmentAnalyzer, VocalRegionAnalyzer, Beatgrid, CamelotKeys);
    // each is now composed once here, hand-wired like every other collaborator on
    // the composition root, so a test/Bench harness can substitute a fake instead of
    // the real compute. HarmonicKeys is shared by KeyAnalyzer (formats the Camelot
    // code) and every UI consumer that colours/compares keys.
    //
    // Takes only `beats` — the trunk hoist (2026-09-12) verified nothing this class
    // builds ever reads a raw stem step (it used to accept one, alongside `beats`,
    // for symmetry with the bundle it's built from; the parameter was discarded).
    // DeckFactory and MainViewModel read stems/demucsCache straight off
    // ExternalToolStack instead.
    public AnalysisStack(IBeatAnalysisStep beats)
    {
        Reporter = new AnalysisReporter(new[] { AnalysisSteps.Beats });

        Peaks = new WaveformPeakAnalyzer();
        HarmonicKeys = new CamelotKeys();
        KeyAnalyzer = new KeyAnalyzer(HarmonicKeys);
        SongSegments = new SongSegmentAnalyzer();
        VocalRegions = new VocalRegionAnalyzer();
        BeatgridFitter = new BeatgridFitter();

        KeyStore = _switchableKeyCache;
        GridStore = _switchableGridCache;

        var basicAnalyzer = new BasicAnalyzer(beatAnalyzer: beats, peakAnalyzer: Peaks, beatgridFitter: BeatgridFitter, reporter: Reporter);

        Provider = new CachingAnalysisProvider(new DbAnalysisProvider(
            new AnalysisProvider(
                compute: (track, ct) =>
                    basicAnalyzer.ComputeAsync(track, ct: ct)),
            store: _switchableBasicCache));
    }

    /// <summary>
    /// Build the three DB-backed tiers and point the switchable wrappers at them —
    /// the ONE mutation in this whole class. Every Deck already built against
    /// <see cref="KeyStore"/>/<see cref="GridStore"/>/<see cref="Provider"/> picks up
    /// the upgrade on its next lookup; nothing on Deck is ever reassigned. Call this
    /// once, as soon as the database is open — see the class doc's RISK note for what
    /// happens if it is never called.
    /// </summary>
    public void AttachDatabase(IDbContextFactory<SholtoDbContext> factory)
    {
        _switchableBasicCache.Attach(new BasicAnalysisStore(factory));
        _switchableKeyCache.Attach(new SqliteKeyAnalysisStore(factory));
        _switchableGridCache.Attach(new SqliteGridAdjustmentStore(factory));
    }
}
