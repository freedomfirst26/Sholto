using Sholto.App.Decks;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>The stem pads through the real deck sessions: a toggle mutes the stem on the session, publishes
/// it and pushes it to that deck's audio exactly once.</summary>
public class PadCommandHandlersTests
{
    private static readonly Origin PadOrigin = new(InterfaceIds.Bench, "test", "pad");

    private readonly SpyStemControl _spy1 = new();
    private readonly SpyStemControl _spy2 = new();
    private readonly DeckSessionRig _rig1;
    private readonly DeckSessionRig _rig2;
    private readonly PadCommandHandlers _handlers;

    public PadCommandHandlersTests()
    {
        _rig1 = new DeckSessionRig(new ScriptedPorts(new TestDeckFactory().Create(), stems: _spy1));
        _rig2 = new DeckSessionRig(new ScriptedPorts(new TestDeckFactory().Create(), stems: _spy2));
        var pair = new DeckPair(_rig1.Session, _rig2.Session);
        _handlers = new PadCommandHandlers(pair, new CueRouting(pair, new RecordingMasterCueOutput(), _rig1.Bus));
    }

    [Fact]
    public void ToggleStem_mutes_the_named_stem_publishes_it_and_pushes_it_to_the_audio_once()
    {
        var stems = new RecordingHandler<StemMuteChanged>();
        using var sub = _rig1.Bus.Subscribe(stems);
        stems.Received.Clear();

        _handlers.Handle(new ToggleStem(0, 1, PadOrigin));

        Assert.False(_rig1.Session.VocalsActive);
        Assert.Equal([(1, false)], _spy1.Mutes);
        Assert.Empty(_spy2.Mutes);
        Assert.Equal([new StemMuteChanged(0, 1, true)], stems.Received);

        _handlers.Handle(new ToggleStem(0, 1, PadOrigin));

        Assert.True(_rig1.Session.VocalsActive);
        Assert.Equal([(1, false), (1, true)], _spy1.Mutes);
    }
}
