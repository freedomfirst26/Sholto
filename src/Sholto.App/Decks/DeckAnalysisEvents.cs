using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Stems;
using Sholto.App.Audio;
using Sholto.Data;

namespace Sholto.App.Decks;

/// <summary>See <see cref="IDeckAnalysisEvents"/>. Analysis events arrive on worker threads and are posted
/// onto the app thread through <see cref="IAppThread"/> before anything is raised.</summary>
public sealed class DeckAnalysisEvents(ITrackLoading loading, ISectionLayoutFactory sectionLayouts, IAppThread appThread) : IDeckAnalysisEvents
{
    private readonly ITrackLoading _loading = loading;
    private readonly ISectionLayoutFactory _sectionLayouts = sectionLayouts;
    private readonly IAppThread _appThread = appThread;

    private SectionLayout _layout = sectionLayouts.Empty();
    // The TrackAnalysis instance we are subscribed to, so we can unsubscribe when Loading.Analysis is
    // replaced (each BeginLoad creates a fresh TrackAnalysis on the player).
    private TrackAnalysis? _subscribedAnalysis;

    public event Action<DeckChange>? Changed;

    public SectionLayout Layout => _layout;

    /// <summary>(Re)subscribe to per-type events on <c>Loading.Analysis</c>. Called at construction and
    /// again after each load, because the player swaps in a fresh <see cref="TrackAnalysis"/> instance per
    /// track. The previously-subscribed instance is detached, otherwise old completed analyses would still
    /// notify against the wrong deck state and handlers would leak.</summary>
    public void Rebind()
    {
        if (_subscribedAnalysis is not null)
        {
            _subscribedAnalysis.BasicReady -= OnBasicReady;
            _subscribedAnalysis.KeyReady -= OnKeyReady;
            _subscribedAnalysis.StemsReady -= OnStemsReady;
            _subscribedAnalysis.VocalRegionsReady -= OnVocalRegionsReady;
        }
        _subscribedAnalysis = _loading.Analysis;
        _subscribedAnalysis.BasicReady += OnBasicReady;
        _subscribedAnalysis.KeyReady += OnKeyReady;
        _subscribedAnalysis.StemsReady += OnStemsReady;
        _subscribedAnalysis.VocalRegionsReady += OnVocalRegionsReady;
    }

    public void ClearLayout()
    {
        _layout = _sectionLayouts.Empty();
        Raise(DeckChange.Sections);
    }

    private void Raise(DeckChange change) => Changed?.Invoke(change);

    // Each per-type handler re-raises only what DEPENDS on that analysis type.
    private void OnBasicReady(BasicAnalysis _) => _appThread.Post(ApplyBasicReady);
    private void OnKeyReady(KeyAnalysis _) => _appThread.Post(RaiseKeyReady);
    private void OnStemsReady(StemPaths _) => _appThread.Post(RaiseStemsReady);
    private void OnVocalRegionsReady(IReadOnlyList<VocalRegion> _) => _appThread.Post(RaiseVocalRegionsReady);

    private void ApplyBasicReady()
    {
        // Recompute the phrase grid and sections from the peaks and the CURRENT grid. BasicReady also fires
        // for a grid nudge or BPM edit, so sections follow the grid; none are persisted.
        var basic = _loading.Analysis.Basic;
        if (basic is not null)
        {
            _layout = _sectionLayouts.Create(basic);
            Raise(DeckChange.Sections);
        }
        Raise(DeckChange.BasicReady);
    }

    private void RaiseKeyReady() => Raise(DeckChange.KeyReady);
    private void RaiseStemsReady() => Raise(DeckChange.StemsReady);
    private void RaiseVocalRegionsReady() => Raise(DeckChange.VocalRegionsReady);
}
