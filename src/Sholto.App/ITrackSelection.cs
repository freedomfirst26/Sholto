using System;
using System.Threading.Tasks;
using Sholto.Analysis;
using Sholto.Library;

namespace Sholto.App;

/// <summary>The library-browse knob's selection state: the highlighted track, and the
/// double-click / long-press re-analyze path. Named for what it exposes — the selected
/// track — rather than the browsing activity itself. One of the four real roles
/// extracted from the old <c>IDeckHost</c> — see the split plan in
/// <c>~/Projects/sholto.md</c>. <see cref="ViewModels.MainViewModel"/> implements this
/// directly.</summary>
public interface ITrackSelection
{
    Track? SelectedTrack { get; }
    void OnBrowseRotated(int delta);
    event Action? ReanalyzeSelectedRequested;

    /// <summary>Misplaced — parked here deliberately, not fixed. The dependency
    /// arrow runs backwards: the caller (Orchestrator) already holds decoding,
    /// analysis and persistence, yet has to hand them to the ViewModel so the
    /// ViewModel can orchestrate them, instead of the Orchestrator orchestrating
    /// them itself. Fixing that is a real redesign, out of scope for the
    /// IDeckHost split — see "OnBrowseHeldAsync has the arrow backwards" in the
    /// split plan (<c>~/Projects/sholto.md</c>). <see cref="ITrackSelection"/> is the
    /// least-wrong of the four new interfaces to carry it, since it's the
    /// browse-hold path's own member.</summary>
    Task OnBrowseHeldAsync(
        Func<Track, float[]> decodeTrack,
        IAnalysisProvider analysisProvider,
        Func<string, KeyAnalysis, Task>? saveKey = null);

    /// <summary>Misplaced for the same reason as <see cref="OnBrowseHeldAsync"/>:
    /// persisted per-track state (a BPM override) read through the ViewModel
    /// instead of from storage directly. Same out-of-scope redesign.</summary>
    double GetBpmMultiplierFor(string filePath);
}
