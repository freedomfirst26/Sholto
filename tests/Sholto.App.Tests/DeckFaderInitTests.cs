using Sholto.Analysis;
using Sholto.App.Theming;
using Sholto.App.ViewModels;
using Sholto.Audio;
using Xunit;

namespace Sholto.App.Tests;

public class DeckFaderInitTests
{
    [Fact]
    public void NewDeck_StartsFaderDown_SoNothingPlaysUntilPickedUp()
    {
        AvaloniaTestApp.EnsureStarted();
        var session = new NullExternalTool();
        var player = new Deck(new AudioFileDecoder([]), new DemucsStemAnalyzer(session), NullLoopDebug.Instance) { Reporter = new AnalysisReporter(Array.Empty<string>()) };
        var deck = new DeckViewModel(player, new ThemeContext());

        // Master-path gain (channel × crossfade) starts at 0 — a freshly loaded
        // deck is silent until the fader is brought up, rather than blasting at
        // full because the software defaulted ahead of the physical fader.
        Assert.Equal(0f, player.MasterGain);
    }

    [Fact]
    public void BringingFaderUp_AppliesGain()
    {
        AvaloniaTestApp.EnsureStarted();
        var session = new NullExternalTool();
        var player = new Deck(new AudioFileDecoder([]), new DemucsStemAnalyzer(session), NullLoopDebug.Instance) { Reporter = new AnalysisReporter(Array.Empty<string>()) };
        var deck = new DeckViewModel(player, new ThemeContext());

        deck.ChannelGain = 1.0;
        Assert.Equal(1f, player.MasterGain);
    }
}
