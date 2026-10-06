using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Data;

namespace Sholto.App.Analysis.Analyzers.Segments;

/// <summary>Phrase-aware song structure: finds the phrase phase and cuts the track into
/// <see cref="SongSection"/>s on phrase lines.</summary>
public interface IPhraseSectionAnalyzer : IAnalyzer
{
    /// <summary>Returns the phrase grid and sections, with bars counted from the grid's first
    /// downbeat. An empty grid gives a default grid and no sections.</summary>
    (PhraseGrid Phrases, IReadOnlyList<SongSection> Sections) Analyze(WaveformPeaks peaks, Beatgrid grid, int sampleRate);
}
