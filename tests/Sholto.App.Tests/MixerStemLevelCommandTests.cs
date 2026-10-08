using Sholto.App.Decks;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>SetStemLevel through the real deck sessions: the level reaches the right deck's stem and is
/// published on <see cref="StemLevelChanged"/> for the screen.</summary>
public class MixerStemLevelCommandTests
{
    private static readonly Origin KnobOrigin = new(InterfaceIds.Bench, "test", "knob");

    private readonly DeckSessionRig _rig1 = new(new ScriptedPorts(new TestDeckFactory().Create(), stems: new SpyStemControl()));
    private readonly DeckSessionRig _rig2 = new(new ScriptedPorts(new TestDeckFactory().Create(), stems: new SpyStemControl()));
    private readonly MixerCommandHandlers _handlers;

    public MixerStemLevelCommandTests() =>
        _handlers = new MixerCommandHandlers(new DeckPair(_rig1.Session, _rig2.Session), new StubMixer());

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void A_stem_level_command_publishes_the_new_level_for_that_deck_and_stem(int stem)
    {
        var seen = new RecordingHandler<StemLevelChanged>();
        var other = new RecordingHandler<StemLevelChanged>();
        using var sub = _rig2.Bus.Subscribe(seen);
        using var otherSub = _rig1.Bus.Subscribe(other);
        seen.Received.Clear();
        other.Received.Clear();

        _handlers.Handle(new SetStemLevel(1, stem, 0.4, KnobOrigin));

        // Each rig's session is index 0 on its own bus; the command for deck 1 must reach the second rig only.
        Assert.Equal([new StemLevelChanged(0, stem, 0.4)], seen.Received);
        Assert.Empty(other.Received);
    }
}
