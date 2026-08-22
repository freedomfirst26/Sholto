using Sholto.Analysis;
using Sholto.App.ViewModels;
using Sholto.Audio;
using Xunit;

namespace Sholto.App.Tests;

public class DeckFaderInitTests
{
    [Fact]
    public void NewDeck_StartsFaderDown_SoNothingPlaysUntilPickedUp()
    {
        var player = new DeckPlayer { Reporter = new AnalysisReporter() };
        var deck = new DeckViewModel(player);

        // Master-path gain (channel × crossfade) starts at 0 — a freshly loaded
        // deck is silent until the fader is brought up, rather than blasting at
        // full because the software defaulted ahead of the physical fader.
        Assert.Equal(0f, player.MasterGain);
    }

    [Fact]
    public void BringingFaderUp_AppliesGain()
    {
        var player = new DeckPlayer { Reporter = new AnalysisReporter() };
        var deck = new DeckViewModel(player);

        deck.ChannelGain = 1.0;
        Assert.Equal(1f, player.MasterGain);
    }
}
