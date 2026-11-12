using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.App.Decks;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>Stem mute and level handling of the deck session, published and pushed to the audio.</summary>
public class DeckSessionStemMixTests
{
    private static readonly Track SomeTrack = new("/music/a.mp3", "A", "Artist", TimeSpan.FromMinutes(3));

    private static BasicAnalysis MakeBasic() => new(
        new WaveformPeaks(Min: [-0.5f], Max: [0.5f], Low: [0.1f], Mid: [0.2f], High: [0.3f], SamplesPerPeak: 1024, SampleRate: 48000),
        Bpm: 128.0,
        BeatTimes: [],
        DownbeatTimes: []);


    /// fires against it.</summary>
    private static ScriptedPorts Ports(bool analysed)
    {
        var ports = new ScriptedPorts(new TestDeckFactory().Create());
        if (analysed) ports.Loading.Analysis.Set(MakeBasic());
        return ports;
    }

    [Fact]
    public void Muting_a_stem_publishes_it_and_an_unchanged_value_publishes_nothing()
    {
        var rig = new DeckSessionRig(new TestDeckFactory().Create());
        var stems = new RecordingHandler<StemMuteChanged>();
        using var sub = rig.Bus.Subscribe(stems);
        stems.Received.Clear();

        rig.Session.VocalsActive = false;
        rig.Session.VocalsActive = false;

        Assert.Equal([new StemMuteChanged(0, 1, true)], stems.Received);
    }

    [Fact]
    public void Loading_a_track_restores_every_stem_to_active()
    {
        var ports = Ports(analysed: true);
        var rig = new DeckSessionRig(ports);
        rig.Session.DrumsActive = false;
        rig.Session.InstrumentalActive = false;
        var stems = new RecordingHandler<StemMuteChanged>();
        using var sub = rig.Bus.Subscribe(stems);
        stems.Received.Clear();

        rig.Session.BeginLoad(SomeTrack);

        Assert.True(rig.Session.DrumsActive);
        Assert.True(rig.Session.InstrumentalActive);
        Assert.Contains(new StemMuteChanged(0, 0, false), stems.Received);
        Assert.Contains(new StemMuteChanged(0, 2, false), stems.Received);
    }

    [Fact]
    public void Setting_a_stem_level_pushes_it_to_the_audio_and_an_unchanged_level_pushes_nothing()
    {
        var spy = new SpyStemControl();
        var rig = new DeckSessionRig(new ScriptedPorts(new TestDeckFactory().Create(), stems: spy));

        rig.Session.DrumsLevel = 0.3;
        rig.Session.DrumsLevel = 0.3;
        rig.Session.VocalsLevel = 0.7;

        Assert.Equal([(0, 0.3), (1, 0.7)], spy.Levels);
    }

    [Fact]
    public void Muting_a_stem_through_the_session_does_not_touch_the_audio()
    {
        // Pins split ownership: PadCommandHandlers pushes the mute to the audio itself; the session setter
        // only publishes. Flips in T-DS3b.
        var spy = new SpyStemControl();
        var rig = new DeckSessionRig(new ScriptedPorts(new TestDeckFactory().Create(), stems: spy));

        rig.Session.DrumsActive = false;

        Assert.False(rig.Session.DrumsActive);
        Assert.Empty(spy.Mutes);
    }

    [Fact]
    public void Loading_a_track_returns_changed_stem_levels_to_unity_on_the_audio()
    {
        var spy = new SpyStemControl();
        var rig = new DeckSessionRig(new ScriptedPorts(new TestDeckFactory().Create(), stems: spy));
        rig.Session.DrumsLevel = 0.3;
        rig.Session.InstrumentalLevel = 0;
        spy.Levels.Clear();

        rig.Session.BeginLoad(SomeTrack);

        Assert.Equal([(0, 1.0), (2, 1.0)], spy.Levels);
        Assert.Equal(1.0, rig.Session.DrumsLevel);
        Assert.Equal(1.0, rig.Session.VocalsLevel);
        Assert.Equal(1.0, rig.Session.InstrumentalLevel);
    }
}
