using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Analyzers.Segments;

namespace Sholto.App.Decks;

/// <inheritdoc cref="ISectionLayoutFactory"/>
public sealed class SectionLayoutFactory(IPhraseSectionAnalyzer analyzer) : ISectionLayoutFactory
{
    private readonly IPhraseSectionAnalyzer _analyzer = analyzer;
    private readonly SectionLayout _empty = new(new Beatgrid(0, 0, 4, 0), new PhraseGrid(0), []);

    public SectionLayout Create(BasicAnalysis basic)
    {
        var grid = GridOf(basic);
        SectionLayout layout;
        if (grid.IsEmpty)
        {
            layout = new SectionLayout(grid, new PhraseGrid(0), []);
        }
        else
        {
            var (phrases, sections) = _analyzer.Analyze(basic.Peaks, grid, basic.Peaks.SampleRate);
            layout = new SectionLayout(grid, phrases, sections);
        }
        Console.WriteLine($"[Deck] song sections: {layout.Sections.Count} " +
            $"(peaks={basic.Peaks.Min.Length}, downbeats={basic.DownbeatTimes.Length}, phase={layout.Phrases.PhaseBar})");
        return layout;
    }

    public SectionLayout Empty() => _empty;

    /// <summary>The constant-spacing grid behind the analysis arrays, as long as the peaks (the track).</summary>
    private Beatgrid GridOf(BasicAnalysis basic) =>
        new(basic.BeatTimes, basic.DownbeatTimes, basic.Bpm,
            basic.Peaks.Min.Length * basic.Peaks.SecondsPerPeak);
}
