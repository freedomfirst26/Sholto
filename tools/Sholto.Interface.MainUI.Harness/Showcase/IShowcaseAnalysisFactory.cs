using Sholto.App.Analysis.Analyzers;

namespace Sholto.Interface.MainUI.Harness.Showcase;

/// <summary>Builds a believable <see cref="BasicAnalysis"/> for a track the harness loaded but cannot
/// analyse (it has no madmom and no decoder), so a screenshot shows a deck with a waveform, a beatgrid,
/// section blocks and a BPM instead of an empty deck.</summary>
public interface IShowcaseAnalysisFactory
{
    /// <summary>An analysis covering <paramref name="seconds"/> of audio: waveform peaks, BPM, every beat
    /// and every downbeat, all on one consistent grid.</summary>
    BasicAnalysis Create(double seconds);
}
