using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.App.Decks;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>The deck session's own logic, with no view model and no Avalonia: the CanPlay gate on
/// TogglePlay, fader pickup (an unmeasured fader plays at unity), the end-of-track flash phase read off
/// the frame clock, and the state events it publishes.</summary>
public class DeckSessionTests
{
    private static readonly Track SomeTrack = new("/music/a.mp3", "A", "Artist", TimeSpan.FromMinutes(3));

    private static BasicAnalysis MakeBasic() => new(
        new WaveformPeaks(Min: [-0.5f], Max: [0.5f], Low: [0.1f], Mid: [0.2f], High: [0.3f], SamplesPerPeak: 1024, SampleRate: 48000),
        Bpm: 128.0,
        BeatTimes: [],
        DownbeatTimes: []);

    /// <summary>Scripted ports; basic analysis is set BEFORE the session subscribes, so no analysis event
    /// fires against it.</summary>
    private static ScriptedPorts Ports(bool analysed)
    {
        var ports = new ScriptedPorts(new TestDeckFactory().Create());
        if (analysed) ports.Loading.Analysis.Set(MakeBasic());
        return ports;
    }

    // ---- CanPlay gate ---------------------------------------------------------------------------

    [Fact]
    public void TogglePlay_is_denied_before_a_track_is_loaded()
    {
        var ports = Ports(analysed: true);
        var rig = new DeckSessionRig(ports);

        rig.Session.TogglePlay();

        Assert.Equal(0, ports.Spy.TogglePlayCalls);
        Assert.False(rig.Session.IsPlaying);
    }

    [Fact]
    public void TogglePlay_is_denied_while_loaded_but_basic_analysis_has_not_landed()
    {
        var ports = Ports(analysed: false);
        var rig = new DeckSessionRig(ports);
        rig.Session.LoadStreaming(SomeTrack, SomeTrack.FilePath);

        Assert.Equal(DeckLoadState.Loaded, rig.Session.LoadState);
        Assert.False(rig.Session.CanPlay);
        rig.Session.TogglePlay();

        Assert.Equal(0, ports.Spy.TogglePlayCalls);
        Assert.False(rig.Session.IsPlaying);
    }

    [Fact]
    public void TogglePlay_starts_the_transport_once_loaded_and_analysed()
    {
        var ports = Ports(analysed: true);
        var rig = new DeckSessionRig(ports);
        rig.Session.LoadStreaming(SomeTrack, SomeTrack.FilePath);

        Assert.True(rig.Session.CanPlay);
        rig.Session.TogglePlay();

        Assert.Equal(1, ports.Spy.TogglePlayCalls);
        Assert.True(rig.Session.IsPlaying);
    }

    [Fact]
    public void A_playing_deck_can_always_be_paused_even_if_it_would_not_pass_the_gate()
    {
        var ports = Ports(analysed: false);
        var rig = new DeckSessionRig(ports);
        ports.ScriptedLoading.IsPlaying = true;   // already playing, e.g. started elsewhere

        rig.Session.TogglePlay();

        Assert.Equal(1, ports.Spy.TogglePlayCalls);
        Assert.False(rig.Session.IsPlaying);
    }


    // ---- Published state ------------------------------------------------------------------------

    [Fact]
    public void A_new_session_publishes_its_whole_starting_picture_for_late_subscribers()
    {
        var rig = new DeckSessionRig(new TestDeckFactory().Create());
        var plays = new RecordingHandler<DeckPlayStateChanged>();
        var stems = new RecordingHandler<StemMuteChanged>();
        var echo = new RecordingHandler<EchoChanged>();
        using var a = rig.Bus.Subscribe(plays);
        using var b = rig.Bus.Subscribe(stems);
        using var c = rig.Bus.Subscribe(echo);

        Assert.Equal([new DeckPlayStateChanged(0, PlayPhase.Stopped, false)], plays.Received);
        Assert.Equal(
            [new StemMuteChanged(0, 0, false), new StemMuteChanged(0, 1, false), new StemMuteChanged(0, 2, false)],
            stems.Received);
        Assert.Equal([new EchoChanged(0, false)], echo.Received);
    }

    [Fact]
    public void Switching_echo_on_and_off_publishes_each_change_once()
    {
        var deck = new TestDeckFactory().Create();
        var rig = new DeckSessionRig(new ScriptedPorts(deck, effects: new RememberingEffects(deck.Effects)));
        var echo = new RecordingHandler<EchoChanged>();
        using var sub = rig.Bus.Subscribe(echo);
        echo.Received.Clear();

        rig.Session.EchoActive = true;
        Assert.True(rig.Session.EchoActive);
        rig.Session.EchoActive = false;

        Assert.Equal([new EchoChanged(0, true), new EchoChanged(0, false)], echo.Received);
    }

    [Fact]
    public void Loading_a_track_switches_echo_off_and_says_so()
    {
        var ports = Ports(analysed: true);
        var rig = new DeckSessionRig(ports);
        rig.Session.EchoActive = true;
        var echo = new RecordingHandler<EchoChanged>();
        using var sub = rig.Bus.Subscribe(echo);
        echo.Received.Clear();

        rig.Session.BeginLoad(SomeTrack);

        Assert.False(rig.Session.EchoActive);
        Assert.Equal(new EchoChanged(0, false), echo.Received[^1]);
    }
}
