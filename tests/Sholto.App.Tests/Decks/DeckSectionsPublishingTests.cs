using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Library;
using Sholto.App.Tests.Segments;
using Sholto.Data;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>The phrase grid and sections a deck session computes on BasicReady and publishes on the bus.</summary>
public class DeckSectionsPublishingTests
{
    private readonly ScriptedPorts _ports = new(new TestDeckFactory().Create());
    private readonly DeckSessionRig _rig;

    public DeckSectionsPublishingTests()
    {
        _rig = new DeckSessionRig(_ports);
    }

    private SyntheticTrack Track(double bpm) =>
        new SyntheticTrackBuilder(bpm).AddStandardStructure().Build();

    private RecordingHandler<DeckSectionsChanged> Watch()
    {
        var handler = new RecordingHandler<DeckSectionsChanged>();
        _rig.Bus.Subscribe(handler);
        handler.Received.Clear();
        return handler;
    }

    [Fact]
    public void Basic_analysis_publishes_sections_phrase_grid_and_the_bar_grid()
    {
        var track = Track(128);
        var seen = Watch();

        _ports.Loading.Analysis.Set(track.Analysis);

        var e = seen.Received[^1];
        Assert.NotEmpty(e.Sections);
        Assert.Equal(_rig.Session.Sections.Count, e.Sections.Count);
        Assert.Equal(track.Grid.BarPeriodSec, e.BarPeriodSec, 6);
        Assert.Equal(track.Grid.FirstDownbeatSec, e.FirstDownbeatSec, 6);
        Assert.Equal(track.TotalBars, e.TotalBars);
        Assert.Equal(8, e.PhraseGrid.PhraseBars);
        Assert.Equal(_rig.Session.PhraseGrid.PhaseBar, e.PhraseGrid.PhaseBar);
        Assert.Contains(e.Sections, s => s.Kind == DeckSectionKind.Drop);
    }

    [Fact]
    public void A_grid_change_recomputes_the_sections_on_the_new_grid()
    {
        var seen = Watch();
        _ports.Loading.Analysis.Set(Track(128).Analysis);
        var before = seen.Received[^1];

        _ports.Loading.Analysis.Set(Track(120).Analysis);
        var after = seen.Received[^1];

        Assert.NotEqual(before.BarPeriodSec, after.BarPeriodSec);
        Assert.Equal(60.0 / 120 * 4, after.BarPeriodSec, 6);
    }

    [Fact]
    public void No_beatgrid_gives_an_empty_section_list()
    {
        var seen = Watch();
        var peaks = new WaveformPeaks(Min: [-0.5f], Max: [0.5f], Low: [0.1f], Mid: [0.2f], High: [0.3f], SamplesPerPeak: 1024, SampleRate: 48000);

        _ports.Loading.Analysis.Set(new BasicAnalysis(peaks, 0, [], []));

        Assert.Empty(seen.Received[^1].Sections);
        Assert.Empty(_rig.Session.Sections);
    }

    [Fact]
    public void Beginning_a_load_clears_the_previous_tracks_sections()
    {
        _ports.Loading.Analysis.Set(Track(128).Analysis);
        var seen = Watch();

        _rig.Session.BeginLoad(new Track("/music/a.mp3", "A", "Artist", TimeSpan.FromMinutes(3)));

        var e = seen.Received[^1];
        Assert.Empty(e.Sections);
        Assert.Equal(0, e.TotalBars);
        Assert.Empty(_rig.Session.Sections);
    }
}
