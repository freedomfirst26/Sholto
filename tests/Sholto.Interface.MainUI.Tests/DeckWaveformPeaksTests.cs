using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Audio;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

public class DeckWaveformPeaksTests
{
    // Frequency-band peaks from basic analysis. Per-stem peaks (StemPeaks) no longer
    // exist, so there is no stem-merge source left to set alongside them; the
    // assertions below still pin Peaks to the basic frequency bands.
    private DeckAnalysis MakeBasic() => new(
        new WaveformPeaks(
            Min:  [-0.5f],
            Max:  [ 0.5f],
            Low:  [ 0.1f],
            Mid:  [ 0.2f],
            High: [ 0.3f],
            SamplesPerPeak: 1024, SampleRate: 48000),
        128.0, [], [], null, false, null);

    // The deck view model is a projection of the deck's content event: the analysis arrives on the bus,
    // so publishing it is what "a loaded deck" means here.
    private (DeckViewModel Deck, DataBus Bus) MakeLoadedDeck()
    {
        AvaloniaTestApp.EnsureStarted();
        var bus = new DataBus(new ThrowingFailureSink());
        var deck = new DeckViewModel(0, bus, bus, new ThemeStackFactory().Build().Context, new NoPeaksFactory(), new DiscBloomFactory(new ManualFrameClock()));
        bus.Publish(new DeckContentChanged(
            0, new DeckTrack("/music/a.mp3", "Alpha", "Zed", TimeSpan.FromMinutes(3)),
            DeckLoadState.Loaded, true, MakeBasic()));
        return (deck, bus);
    }

    [Fact]
    public void Peaks_AreFrequencyBands_NotStemMerge()
    {
        var (deck, _) = MakeLoadedDeck();
        // Peaks must be the basic frequency peaks.
        Assert.Same(deck.Analysis!.Peaks, deck.Peaks);
    }

    [Fact]
    public void Peaks_Unchanged_WhenStemsToggled()
    {
        var (deck, bus) = MakeLoadedDeck();
        var before = deck.Peaks;

        bus.Publish(new StemMuteChanged(0, Stem: 0, Muted: true));
        bus.Publish(new StemMuteChanged(0, Stem: 1, Muted: true));
        bus.Publish(new StemMuteChanged(0, Stem: 2, Muted: true));
        Assert.False(deck.DrumsActive);
        Assert.False(deck.VocalsActive);
        Assert.False(deck.InstrumentalActive);

        // Silhouette is the full mix regardless of which stems are muted.
        Assert.Same(before, deck.Peaks);
    }
}
