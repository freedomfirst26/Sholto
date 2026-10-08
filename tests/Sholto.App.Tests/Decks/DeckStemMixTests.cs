using Sholto.App.Decks;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>A deck's three stem groups: mute flags, levels pushed to the audio, and what is published.</summary>
public class DeckStemMixTests
{
    private readonly SpyStemControl _spy = new();
    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly DeckStemMix _mix;
    private readonly List<DeckChange> _changes = [];

    public DeckStemMixTests()
    {
        _mix = new DeckStemMix(0, _spy, _bus);
        _mix.Changed += _changes.Add;
    }

    private RecordingHandler<StemMuteChanged> Watch(out IDisposable subscription)
    {
        var received = new RecordingHandler<StemMuteChanged>();
        subscription = _bus.Subscribe(received);
        return received;
    }

    [Fact]
    public void Publishing_the_current_state_announces_all_three_stems_unmuted()
    {
        _mix.PublishCurrent();

        var seen = Watch(out var sub);
        using var s = sub;

        Assert.Equal(
            [new StemMuteChanged(0, 0, false), new StemMuteChanged(0, 1, false), new StemMuteChanged(0, 2, false)],
            seen.Received);
    }

    [Fact]
    public void Muting_a_stem_raises_its_change_and_publishes_once_and_the_same_value_does_nothing()
    {
        var seen = Watch(out var sub);
        using var s = sub;
        seen.Received.Clear();

        _mix.VocalsActive = false;
        _mix.VocalsActive = false;

        Assert.Equal([DeckChange.VocalsActive], _changes);
        Assert.Equal([new StemMuteChanged(0, 1, true)], seen.Received);
        Assert.Empty(_spy.Mutes);
    }

    [Fact]
    public void Resetting_for_a_new_track_restores_every_stem_and_publishes_only_the_changed_ones()
    {
        _mix.DrumsActive = false;
        _mix.InstrumentalLevel = 0.2;
        var seen = Watch(out var sub);
        using var s = sub;
        seen.Received.Clear();
        _changes.Clear();
        _spy.Levels.Clear();

        _mix.ResetForNewTrack();

        Assert.True(_mix.DrumsActive);
        Assert.True(_mix.VocalsActive);
        Assert.True(_mix.InstrumentalActive);
        Assert.Equal(1.0, _mix.InstrumentalLevel);
        Assert.Equal([new StemMuteChanged(0, 0, false)], seen.Received);
        Assert.Equal([DeckChange.DrumsActive, DeckChange.InstrumentalLevel], _changes);
        Assert.Equal([(2, 1.0)], _spy.Levels);
    }

    [Fact]
    public void A_level_is_pushed_to_the_audio_then_raised_and_an_unchanged_level_does_neither()
    {
        _mix.VocalsLevel = 0.5;
        _mix.VocalsLevel = 0.5;

        Assert.Equal([(1, 0.5)], _spy.Levels);
        Assert.Equal([DeckChange.VocalsLevel], _changes);
        Assert.Equal(0.5, _mix.VocalsLevel);
    }

    [Fact]
    public void Turning_a_stem_level_publishes_it_once_and_the_same_level_does_nothing()
    {
        var seen = new RecordingHandler<StemLevelChanged>();
        using var sub = _bus.Subscribe(seen);
        seen.Received.Clear();

        _mix.VocalsLevel = 0.5;
        _mix.VocalsLevel = 0.5;

        Assert.Equal([new StemLevelChanged(0, 1, 0.5)], seen.Received);
    }

    [Fact]
    public void Resetting_for_a_new_track_publishes_unity_for_a_turned_down_stem()
    {
        _mix.InstrumentalLevel = 0.2;
        var seen = new RecordingHandler<StemLevelChanged>();
        using var sub = _bus.Subscribe(seen);
        seen.Received.Clear();

        _mix.ResetForNewTrack();

        Assert.Equal([new StemLevelChanged(0, 2, 1.0)], seen.Received);
    }

    [Fact]
    public void Publishing_the_current_state_announces_every_stem_level()
    {
        _mix.DrumsLevel = 0.7;
        _mix.PublishCurrent();
        var seen = new RecordingHandler<StemLevelChanged>();

        using var sub = _bus.Subscribe(seen);

        Assert.Equal(
            [new StemLevelChanged(0, 0, 0.7), new StemLevelChanged(0, 1, 1.0), new StemLevelChanged(0, 2, 1.0)],
            seen.Received);
    }
}
