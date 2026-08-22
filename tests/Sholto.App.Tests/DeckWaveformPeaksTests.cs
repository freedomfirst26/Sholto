using Sholto.Analysis;
using Sholto.App.ViewModels;
using Sholto.Audio;

namespace Sholto.App.Tests;

public class DeckWaveformPeaksTests
{
    // Frequency-band peaks from basic analysis, deliberately distinct from the
    // per-stem peaks below so a stem-merge would produce a different result.
    private static BasicAnalysis MakeBasic() => new(
        new WaveformPeaks(
            Min:  [-0.5f],
            Max:  [ 0.5f],
            Low:  [ 0.1f],
            Mid:  [ 0.2f],
            High: [ 0.3f],
            SamplesPerPeak: 1024),
        Bpm: 128.0,
        BeatTimes: [],
        DownbeatTimes: []);

    private static StemPeaks MakeStems()
    {
        WaveformPeaks Band(float v) => new([-v], [v], [v], [v], [v], 1024);
        return new StemPeaks(Drums: Band(0.9f), Vocals: Band(0.8f), Bass: Band(0.7f), Other: Band(0.6f));
    }

    // Analyses are set BEFORE the view model subscribes, so no analysis-ready
    // events fire against a live subscription (keeps the test off the Avalonia
    // dispatcher).
    private static DeckViewModel MakeLoadedDeck()
    {
        var player = new Deck { Reporter = new AnalysisReporter() };
        player.Analysis.Set(MakeBasic());
        player.Analysis.Set(MakeStems());
        return new DeckViewModel(player);
    }

    [Fact]
    public void Peaks_AreFrequencyBands_NotStemMerge()
    {
        var deck = MakeLoadedDeck();
        // Peaks must be the basic frequency peaks even though stem peaks exist.
        Assert.Same(deck.Analysis.Basic!.Peaks, deck.Peaks);
    }

    [Fact]
    public void Peaks_Unchanged_WhenStemsToggled()
    {
        var deck = MakeLoadedDeck();
        var before = deck.Peaks;

        deck.DrumsActive = false;
        deck.VocalsActive = false;
        deck.InstrumentalActive = false;

        // Silhouette is the full mix regardless of which stems are muted.
        Assert.Same(before, deck.Peaks);
    }
}
