using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Decks;
using Sholto.App.Tests.Segments;
using Sholto.Data;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>The section layout built from basic analysis: sections on a real grid, none on an empty one.</summary>
public class SectionLayoutFactoryTests
{
    private readonly SectionLayoutFactory _factory = new(new PhraseSectionAnalyzer(
        new BarFeatureExtractor(new PhraseSectionOptions()),
        new PhraseSectionLabeler(new PhraseSectionOptions()),
        new PhraseSectionOptions()));

    [Fact]
    public void A_128_bpm_track_gets_sections_on_a_1_875_second_bar_grid()
    {
        var track = new SyntheticTrackBuilder(128).AddStandardStructure().Build();

        var layout = _factory.Create(track.Analysis);

        Assert.NotEmpty(layout.Sections);
        Assert.Equal(1.875, layout.Grid.BarPeriodSec, 6);
    }

    [Fact]
    public void A_basic_analysis_with_no_beatgrid_gets_no_sections()
    {
        var peaks = new WaveformPeaks(Min: [-0.5f], Max: [0.5f], Low: [0.1f], Mid: [0.2f], High: [0.3f], SamplesPerPeak: 1024, SampleRate: 48000);

        var layout = _factory.Create(new BasicAnalysis(peaks, 0, [], []));

        Assert.Empty(layout.Sections);
        Assert.True(layout.Grid.IsEmpty);
    }

    [Fact]
    public void The_empty_layout_has_an_empty_grid_and_no_sections()
    {
        var layout = _factory.Empty();

        Assert.True(layout.Grid.IsEmpty);
        Assert.Empty(layout.Sections);
    }
}
