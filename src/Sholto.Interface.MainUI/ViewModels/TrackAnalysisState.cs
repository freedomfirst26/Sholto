namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The single, mutually-exclusive analysis state of a track row — one
/// decoration in the ANALYZED column, never two.</summary>
public enum TrackAnalysisState
{
    /// <summary>Nothing computed yet.</summary>
    Unanalyzed,
    /// <summary>An analysis step is currently running for this track.</summary>
    Analyzing,
    /// <summary>BPM/beats + stems are done.</summary>
    Analyzed,
    /// <summary>An analysis step reported a failure and the track did not finish.
    /// Distinct from <see cref="Unanalyzed"/> on purpose: a broken external analyser
    /// used to look exactly like a track nobody had got round to yet.</summary>
    Failed,
}
