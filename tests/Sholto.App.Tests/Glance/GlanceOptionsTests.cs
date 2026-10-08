using Microsoft.Extensions.Options;
using Sholto.App.Glance;
using Sholto.Data;

namespace Sholto.App.Tests.Glance;

/// <summary>The Glance fit cut-offs keep the values they had before they became options, and moving one moves the verdict.</summary>
public class GlanceOptionsTests
{
    private readonly GlanceTrackBuilder _b = new();

    [Fact]
    public void Defaults_equal_the_former_constants()
    {
        var options = new GlanceOptions();

        Assert.Equal(250, options.MaxResults);
        Assert.Equal(6.0, options.ClashTempoPercent);
        Assert.Equal(2.0, options.GoodTempoPercent);
        Assert.Equal(4.0, options.UsableTempoPercent);
    }

    [Fact]
    public void A_wider_clash_cutoff_turns_a_clash_into_usable()
    {
        var reference = new GlanceReference(0, _b.Key("8A"), 128, "/m/ref.mp3");
        var track = _b.Track("t", "8A", 137);

        var defaults = new FitScorer(Options.Create(new GlanceOptions())).Score(track, reference);
        var wider = new FitScorer(Options.Create(new GlanceOptions { ClashTempoPercent = 10 })).Score(track, reference);

        Assert.Equal(FitLevel.Clash, defaults.Level);
        Assert.Equal(FitLevel.Usable, wider.Level);
    }
}
