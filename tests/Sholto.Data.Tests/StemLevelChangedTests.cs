using Sholto.Data;

namespace Sholto.Data.Tests;

public class StemLevelChangedTests
{
    [Fact]
    public void Each_deck_and_stem_gets_its_own_slot()
    {
        var slots = new HashSet<int>();
        for (var deck = 0; deck < 2; deck++)
            for (var stem = 0; stem < StemMuteChanged.StemsPerDeck; stem++)
                Assert.True(slots.Add(new StemLevelChanged(deck, stem, 1.0).Slot));
    }

    [Fact]
    public void It_carries_the_deck_stem_and_level()
    {
        var e = new StemLevelChanged(1, 2, 0.4);

        Assert.Equal((1, 2, 0.4), (e.Deck, e.Stem, e.Level));
    }

    [Fact]
    public void The_bus_replays_the_last_level_of_a_stem_to_a_late_subscriber()
    {
        var bus = new DataBus(new RecordingFailureSink());
        bus.Publish(new StemLevelChanged(0, 1, 0.9));
        bus.Publish(new StemLevelChanged(0, 1, 0.5));
        var seen = new RecordingHandler<StemLevelChanged>();

        using var sub = bus.Subscribe(seen);

        Assert.Equal([new StemLevelChanged(0, 1, 0.5)], seen.Received);
    }
}
