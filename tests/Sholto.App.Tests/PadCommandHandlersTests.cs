using Sholto.App.Decks;
using Sholto.App.Library;
using Sholto.App.Tests.Segments;
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
    private readonly ScriptedPorts _ports1;
    private readonly SeekRecordingTransport _transport1;
    private readonly RecordingDeckHotCues _hotCues = new();
    private readonly PadCommandHandlers _handlers;

    public PadCommandHandlersTests()
    {
        _ports1 = new ScriptedPorts(new TestDeckFactory().Create(), stems: _spy1);
        _transport1 = new SeekRecordingTransport(_ports1);
        _rig1 = new DeckSessionRig(new SeekRecordingPorts(_ports1, _transport1));
        _rig2 = new DeckSessionRig(new ScriptedPorts(new TestDeckFactory().Create(), stems: _spy2));
        var pair = new DeckPair(_rig1.Session, _rig2.Session);
        _handlers = new PadCommandHandlers(pair, new CueRouting(pair, new RecordingMasterCueOutput(), _rig1.Bus), _hotCues,
            new BeatJumpSize(Microsoft.Extensions.Options.Options.Create(new BeatJumpOptions())));
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

    [Fact]
    public void Hot_cue_commands_are_forwarded_to_the_deck_hot_cues()
    {
        _handlers.Handle(new PressHotCue(1, 3, true, PadOrigin));
        _handlers.Handle(new PressHotCue(1, 3, false, PadOrigin));
        _handlers.Handle(new DeleteHotCue(0, 7, PadOrigin));

        Assert.Equal(["press 1 3 True", "press 1 3 False", "delete 0 7"], _hotCues.Calls);
    }

    private void LoadWithBpm(double bpm)
    {
        var track = LibrarySessionRig.Alpha;
        _rig1.Session.LoadTrack(track, track.FilePath, [], bpmMultiplier: 1.0);
        _ports1.Loading.Analysis.Set(new SyntheticTrackBuilder(bpm).AddStandardStructure().Build().Analysis);
    }

    [Fact]
    public void Beat_jump_moves_by_whole_beats_of_the_track_bpm()
    {
        LoadWithBpm(120);

        _handlers.Handle(new BeatJump(0, -4, PadOrigin));
        _handlers.Handle(new BeatJump(0, 8, PadOrigin));

        Assert.Equal([-2.0, 4.0], _transport1.Seeks);
    }

    [Fact]
    public void Every_pad_jumps_its_printed_beats_before_any_size_change()
    {
        LoadWithBpm(120);

        foreach (var beats in new double[] { -1, 1, -2, 2, -4, 4, -8, 8 })
            _handlers.Handle(new BeatJump(0, beats, PadOrigin));

        Assert.Equal([-0.5, 0.5, -1.0, 1.0, -2.0, 2.0, -4.0, 4.0], _transport1.Seeks);
    }

    [Fact]
    public void After_halving_every_pad_on_that_deck_jumps_half_as_far()
    {
        LoadWithBpm(120);

        _handlers.Handle(new HalveBeatJump(0, PadOrigin));
        foreach (var beats in new double[] { -1, 1, -2, 2, -4, 4, -8, 8 })
            _handlers.Handle(new BeatJump(0, beats, PadOrigin));

        Assert.Equal([-0.25, 0.25, -0.5, 0.5, -1.0, 1.0, -2.0, 2.0], _transport1.Seeks);
    }

    [Fact]
    public void After_doubling_every_pad_on_that_deck_jumps_twice_as_far()
    {
        LoadWithBpm(120);

        _handlers.Handle(new DoubleBeatJump(0, PadOrigin));
        foreach (var beats in new double[] { -1, 1, -2, 2, -4, 4, -8, 8 })
            _handlers.Handle(new BeatJump(0, beats, PadOrigin));

        Assert.Equal([-1.0, 1.0, -2.0, 2.0, -4.0, 4.0, -8.0, 8.0], _transport1.Seeks);
    }

    [Fact]
    public void Changing_the_other_decks_size_leaves_this_decks_jumps_alone()
    {
        LoadWithBpm(120);

        _handlers.Handle(new HalveBeatJump(1, PadOrigin));
        _handlers.Handle(new DoubleBeatJump(1, PadOrigin));
        _handlers.Handle(new DoubleBeatJump(1, PadOrigin));
        _handlers.Handle(new BeatJump(0, 4, PadOrigin));

        Assert.Equal([2.0], _transport1.Seeks);
    }

    [Fact]
    public void Beat_jump_does_nothing_on_a_deck_without_a_track()
    {
        _handlers.Handle(new BeatJump(0, 4, PadOrigin));

        Assert.Empty(_transport1.Seeks);
    }

    [Fact]
    public void Beat_jump_does_nothing_when_the_bpm_is_unknown()
    {
        LoadWithBpm(120);
        _ports1.Loading.Analysis.Set(new Sholto.App.Analysis.Analyzers.BasicAnalysis(
            new Sholto.App.Analysis.Analyzers.Waveform.WaveformPeaksFactory().None(), 0, [], []));

        _handlers.Handle(new BeatJump(0, 4, PadOrigin));

        Assert.Empty(_transport1.Seeks);
    }

    [Fact]
    public void Solo_leaves_only_that_stem_audible()
    {
        _handlers.Handle(new SoloStem(0, 1, PadOrigin));

        Assert.False(_rig1.Session.DrumsActive);
        Assert.True(_rig1.Session.VocalsActive);
        Assert.False(_rig1.Session.InstrumentalActive);
        Assert.Contains((0, false), _spy1.Mutes);
        Assert.Contains((2, false), _spy1.Mutes);
    }

    [Fact]
    public void Solo_on_the_already_soloed_stem_restores_all_three()
    {
        _handlers.Handle(new SoloStem(0, 1, PadOrigin));
        _handlers.Handle(new SoloStem(0, 1, PadOrigin));

        Assert.True(_rig1.Session.DrumsActive);
        Assert.True(_rig1.Session.VocalsActive);
        Assert.True(_rig1.Session.InstrumentalActive);
    }

    [Fact]
    public void Solo_on_another_stem_moves_the_solo()
    {
        _handlers.Handle(new SoloStem(0, 1, PadOrigin));
        _handlers.Handle(new SoloStem(0, 2, PadOrigin));

        Assert.False(_rig1.Session.DrumsActive);
        Assert.False(_rig1.Session.VocalsActive);
        Assert.True(_rig1.Session.InstrumentalActive);
    }

    [Fact]
    public void Solo_on_a_deck_without_stems_does_not_throw()
    {
        var bare = new DeckSessionRig(new ScriptedPorts(new TestDeckFactory().Create()));
        var pair = new DeckPair(bare.Session, bare.Session);
        var handlers = new PadCommandHandlers(pair, new CueRouting(pair, new RecordingMasterCueOutput(), bare.Bus), _hotCues,
            new BeatJumpSize(Microsoft.Extensions.Options.Options.Create(new BeatJumpOptions())));

        var ex = Record.Exception(() => handlers.Handle(new SoloStem(0, 0, PadOrigin)));

        Assert.Null(ex);
    }
}
