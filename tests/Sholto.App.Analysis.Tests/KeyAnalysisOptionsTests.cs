using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers.Keys;

namespace Sholto.App.Analysis.Tests;

public class KeyAnalysisOptionsTests
{
    [Fact]
    public void Defaults_match_the_values_the_analyzer_always_used()
    {
        var options = new KeyAnalysisOptions();

        Assert.Equal(440.0, options.ReferencePitchHz);
        Assert.Equal(36, options.FirstMidiNote);
        Assert.Equal(95, options.LastMidiNote);
        Assert.Equal(32, options.FramesPerChunk);
    }

    [Fact]
    public void A_filter_range_that_ends_below_its_start_is_refused()
    {
        var options = Options.Create(new KeyAnalysisOptions { FirstMidiNote = 60, LastMidiNote = 59 });

        Assert.Throws<ArgumentException>(() => new KeyAnalyzer(options));
    }

    [Fact]
    public void A_chunk_of_no_frames_is_refused()
    {
        var options = Options.Create(new KeyAnalysisOptions { FramesPerChunk = 0 });

        Assert.Throws<ArgumentException>(() => new KeyAnalyzer(options));
    }
}
