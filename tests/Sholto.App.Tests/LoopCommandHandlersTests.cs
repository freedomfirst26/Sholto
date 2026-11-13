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
        Assert.Equal(["exit"], _looping1.Calls);
    }

    [Fact]
    public void An_eight_beat_pad_resizes_a_running_one_beat_loop_from_the_same_loop_in()
    {
        _handlers.Handle(new ToggleBeatLoop(0, 0, PadOrigin, Beats: 1));
        var loopIn = Assert.NotNull(_looping1.ActiveLoop).StartSample;
        _looping1.Calls.Clear();

        _handlers.Handle(new ToggleBeatLoop(0, 0, PadOrigin, Beats: 8));

        var loop = Assert.NotNull(_looping1.ActiveLoop);
        Assert.Equal(loopIn, loop.StartSample);
        Assert.Equal(8 * SamplesPerBeat, loop.LengthSamples);
        Assert.Equal(["double", "double", "double"], _looping1.Calls);
    }

    [Fact]
    public void A_quarter_beat_pad_resizes_a_running_four_beat_loop_from_the_same_loop_in()
    {
        _handlers.Handle(new ToggleBeatLoop(0, 0, PadOrigin, Beats: 4));
        var loopIn = Assert.NotNull(_looping1.ActiveLoop).StartSample;
        _looping1.Calls.Clear();

        _handlers.Handle(new ToggleBeatLoop(0, 0, PadOrigin, Beats: 0.25));

        var loop = Assert.NotNull(_looping1.ActiveLoop);
        Assert.Equal(loopIn, loop.StartSample);
        Assert.Equal(SamplesPerBeat / 4, loop.LengthSamples);
        Assert.Equal(["halve", "halve", "halve", "halve"], _looping1.Calls);
    }

    [Fact]
    public void The_pad_of_the_running_loops_length_exits_it()
    {
        _handlers.Handle(new ToggleBeatLoop(0, 0, PadOrigin, Beats: 2));

        _handlers.Handle(new ToggleBeatLoop(0, 0, PadOrigin, Beats: 2));

        Assert.Null(_looping1.ActiveLoop);
    }

    [Fact]
    public void A_loop_halved_by_the_halve_command_counts_at_its_new_length()
    {
        _handlers.Handle(new ToggleBeatLoop(0, 0, PadOrigin, Beats: 2));
        _handlers.Handle(new HalveLoop(0, PadOrigin));

        _handlers.Handle(new ToggleBeatLoop(0, 0, PadOrigin, Beats: 1));

        Assert.Null(_looping1.ActiveLoop);
    }

    [Fact]
    public void A_loop_doubled_by_the_double_command_counts_at_its_new_length()
    {
        _handlers.Handle(new ToggleBeatLoop(0, 0, PadOrigin, Beats: 2));
        _handlers.Handle(new DoubleLoop(0, PadOrigin));

        _handlers.Handle(new ToggleBeatLoop(0, 0, PadOrigin, Beats: 4));

        Assert.Null(_looping1.ActiveLoop);
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(2)]
    [InlineData(32)]
    public void The_four_beat_button_exits_a_running_loop_of_any_length(double beats)
    {
        _handlers.Handle(new ToggleBeatLoop(0, 0, PadOrigin, Beats: beats));

        _handlers.Handle(new ToggleBeatLoop(0, 4, PadOrigin));

        Assert.Null(_looping1.ActiveLoop);
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
