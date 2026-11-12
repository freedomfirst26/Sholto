using Sholto.App.Decks;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary><see cref="ToggleBeatLoop"/> through the real deck sessions: the 4 BEAT button's whole bars and
/// the BEAT LOOP pads' 1/4 to 32 beats reach the named deck's loop at their length.</summary>
public class LoopCommandHandlersTests
{
    private const long SamplesPerBeat = RecordingLooping.SamplesPerBar / 4;
    private static readonly Origin PadOrigin = new(InterfaceIds.Bench, "test", "pad");

    private readonly RecordingLooping _looping1 = new();
    private readonly RecordingLooping _looping2 = new();
    private readonly LoopCommandHandlers _handlers;

    public LoopCommandHandlersTests()
    {
        var rig1 = new DeckSessionRig(new LoopingPorts(new ScriptedPorts(new TestDeckFactory().Create()), _looping1));
        var rig2 = new DeckSessionRig(new LoopingPorts(new ScriptedPorts(new TestDeckFactory().Create()), _looping2));
        _handlers = new LoopCommandHandlers(new DeckPair(rig1.Session, rig2.Session), new RecordingDeckMarkers());
    }

    [Fact]
    public void A_quarter_beat_command_loops_a_quarter_of_a_beat_on_its_deck()
    {
        _handlers.Handle(new ToggleBeatLoop(1, 0, PadOrigin, Beats: 0.25));

        Assert.Equal(SamplesPerBeat / 4, Assert.NotNull(_looping2.ActiveLoop).LengthSamples);
        Assert.Null(_looping1.ActiveLoop);
    }

    [Fact]
    public void A_thirty_two_beat_command_loops_eight_bars()
    {
        _handlers.Handle(new ToggleBeatLoop(0, 0, PadOrigin, Beats: 32));

        Assert.Equal(32 * SamplesPerBeat, Assert.NotNull(_looping1.ActiveLoop).LengthSamples);
        Assert.Equal(["enable 8"], _looping1.Calls);
    }

    [Fact]
    public void A_bars_command_with_no_beats_still_loops_whole_bars()
    {
        _handlers.Handle(new ToggleBeatLoop(0, 4, PadOrigin));

        Assert.Equal(["enable 4"], _looping1.Calls);
        Assert.Equal(16 * SamplesPerBeat, Assert.NotNull(_looping1.ActiveLoop).LengthSamples);
    }

    [Fact]
    public void A_second_press_exits_the_short_loop_without_halving_anything()
    {
        _handlers.Handle(new ToggleBeatLoop(0, 0, PadOrigin, Beats: 0.5));
        _looping1.Calls.Clear();

        _handlers.Handle(new ToggleBeatLoop(0, 0, PadOrigin, Beats: 0.5));

        Assert.Null(_looping1.ActiveLoop);
        Assert.Equal(["enable 1"], _looping1.Calls);
    }

    [Theory]
    [InlineData(4, 0, 16)]
    [InlineData(1, 0, 4)]
    [InlineData(4, 0.25, 0.25)]
    [InlineData(0, 32, 32)]
    public void The_length_in_beats_is_the_beats_when_given_else_four_per_bar(int bars, double beats, double expected)
    {
        Assert.Equal(expected, new ToggleBeatLoop(0, bars, PadOrigin, beats).LengthBeats);
    }
}
