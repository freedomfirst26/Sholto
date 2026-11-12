using Sholto.TestSupport.Allocation;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.App.Decks;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>The end-of-track flash and play-phase events of the deck session, read off the frame clock.</summary>
public class DeckSessionPlayPhaseTests
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

    /// <summary>Playing at 0.95 of the track from a known instant; returns that instant.</summary>
    private static DateTime StartEnding(DeckSessionRig rig, ScriptedPorts ports)
    {
        ports.ScriptedLoading.IsPlaying = true;
        ports.ScriptedPlayhead.PlayPosition = 0.5;
        rig.Session.SyncPlayPosition();
        Assert.Equal(PlayPhase.Playing, rig.Session.PlayState);

        ports.ScriptedPlayhead.PlayPosition = 0.95;
        var start = rig.Clock.Now;
        rig.Session.SyncPlayPosition();
        return start;
    }

    [Fact]
    public void The_flash_phase_is_a_function_of_the_frame_clock_400_ms_lit_then_400_ms_dark()
    {
        var ports = Ports(analysed: true);
        var rig = new DeckSessionRig(ports);
        var plays = new RecordingHandler<DeckPlayStateChanged>();
        using var sub = rig.Bus.Subscribe(plays);

        var start = StartEnding(rig, ports);
        Assert.Equal(PlayPhase.Ending, rig.Session.PlayState);
        Assert.True(rig.Session.EndFlashOn);

        rig.Clock.Now = start.AddMilliseconds(399);
        rig.Session.SyncPlayPosition();
        Assert.True(rig.Session.EndFlashOn);

        rig.Clock.Now = start.AddMilliseconds(400);
        rig.Session.SyncPlayPosition();
        Assert.False(rig.Session.EndFlashOn);

        rig.Clock.Now = start.AddMilliseconds(799);
        rig.Session.SyncPlayPosition();
        Assert.False(rig.Session.EndFlashOn);

        rig.Clock.Now = start.AddMilliseconds(800);
        rig.Session.SyncPlayPosition();
        Assert.True(rig.Session.EndFlashOn);

        // The bus carries every phase change, so the controller light blinks exactly as the ring does.
        Assert.Equal(
            [
                new DeckPlayStateChanged(0, PlayPhase.Stopped, false),   // replayed on subscribe
                new DeckPlayStateChanged(0, PlayPhase.Playing, false),
                new DeckPlayStateChanged(0, PlayPhase.Ending, true),
                new DeckPlayStateChanged(0, PlayPhase.Ending, false),
                new DeckPlayStateChanged(0, PlayPhase.Ending, true),
            ],
            plays.Received);
    }

    [Fact]
    public void Every_flash_toggle_raises_EndFlash_and_publishes_once_so_ring_and_light_cannot_drift()
    {
        var ports = Ports(analysed: true);
        var rig = new DeckSessionRig(ports);
        var plays = new RecordingHandler<DeckPlayStateChanged>();
        using var sub = rig.Bus.Subscribe(plays);
        var start = StartEnding(rig, ports);
        var flashes = 0;
        rig.Session.Changed += change => { if (change == DeckChange.EndFlash) flashes++; };
        var publishedBefore = plays.Received.Count;

        for (var ms = 0; ms < 2000; ms += 16)
        {
            rig.Clock.Now = start.AddMilliseconds(ms);
            rig.Session.SyncPlayPosition();
        }

        // 400 ms half-period over 2000 ms: dark at 400, lit at 800, dark at 1200, lit at 1600, and the loop
        // stops before 2000, so four toggles.
        Assert.Equal(4, flashes);
        Assert.Equal(flashes, plays.Received.Count - publishedBefore);
    }

    [Fact]
    public void Pausing_ends_the_flash()
    {
        var ports = Ports(analysed: true);
        var rig = new DeckSessionRig(ports);
        StartEnding(rig, ports);

        ports.ScriptedLoading.IsPlaying = false;
        rig.Session.SyncPlayPosition();

        Assert.Equal(PlayPhase.Stopped, rig.Session.PlayState);
        Assert.False(rig.Session.EndFlashOn);
    }

    [Fact]
    public void The_per_frame_tick_allocates_nothing_after_warm_up()
    {
        var ports = Ports(analysed: true);
        var rig = new DeckSessionRig(ports);
        var start = StartEnding(rig, ports);
        var frame = 0;

        void Run(int frames)
        {
            for (var i = 0; i < frames; i++, frame++)
            {
                rig.Clock.Now = start.AddMilliseconds(frame * 16);
                rig.Session.SyncPlayPosition();
            }
        }

        // A full unmeasured pass so tiered JIT/OSR has finished before measuring.
        Run(2200);
        // Five windows: a sporadic one-off runtime allocation on a random frame lands in at most one; a real
        // per-frame allocation (at least 24 B a frame) hits every window.
        var result = new AllocationWindows(5).Measure(() => Run(400));

        Assert.True(result.AllocatingWindows <= 1, result.ToString());
    }

    [Fact]
    public void A_scratch_in_flight_leaves_the_transport_state_alone_but_still_moves_the_position()
    {
        var ports = Ports(analysed: true);
        var rig = new DeckSessionRig(ports);
        rig.Session.IsScratching = true;
        ports.ScriptedLoading.IsPlaying = true;
        ports.ScriptedPlayhead.PlayPosition = 0.3;

        rig.Session.SyncPlayPosition();

        Assert.False(rig.Session.IsPlaying);
        Assert.Equal(PlayPhase.Stopped, rig.Session.PlayState);
        Assert.Equal(0.3, rig.Session.PlayPosition);

        rig.Session.IsScratching = false;
        rig.Session.SyncPlayPosition();

        Assert.True(rig.Session.IsPlaying);
        Assert.Equal(PlayPhase.Playing, rig.Session.PlayState);
    }

    [Fact]
    public void Starting_play_raises_IsPlaying_then_PlayState_then_publishes_then_moves_the_position()
    {
        var ports = Ports(analysed: true);
        var rig = new DeckSessionRig(ports);
        var log = new List<string>();
        rig.Session.Changed += change =>
        {
            if (change is DeckChange.IsPlaying or DeckChange.PlayState or DeckChange.PlayPosition or DeckChange.EndFlash)
                log.Add(change.ToString());
        };
        using var sub = rig.Bus.Subscribe(new ActionEventHandler<DeckPlayStateChanged>(e => log.Add("pub:" + e.Phase)));
        log.Clear();   // drop the replay on subscribe

        ports.ScriptedLoading.IsPlaying = true;
        ports.ScriptedPlayhead.PlayPosition = 0.5;
        rig.Session.SyncPlayPosition();

        Assert.Equal(["IsPlaying", "PlayState", "pub:Playing", "PlayPosition"], log);

        log.Clear();
        ports.ScriptedPlayhead.PlayPosition = 0.95;
        rig.Session.SyncPlayPosition();

        Assert.Equal(["PlayPosition", "PlayState", "pub:Ending"], log);
    }

    [Fact]
    public void Re_entering_the_end_restarts_the_flash_from_the_new_moment()
    {
        var ports = Ports(analysed: true);
        var rig = new DeckSessionRig(ports);
        var start = StartEnding(rig, ports);

        rig.Clock.Now = start.AddMilliseconds(1000);
        ports.ScriptedLoading.IsPlaying = false;
        rig.Session.SyncPlayPosition();
        Assert.Equal(PlayPhase.Stopped, rig.Session.PlayState);

        var t2 = start.AddMilliseconds(1300);
        rig.Clock.Now = t2;
        ports.ScriptedLoading.IsPlaying = true;
        ports.ScriptedPlayhead.PlayPosition = 0.95;
        rig.Session.SyncPlayPosition();
        Assert.True(rig.Session.EndFlashOn);

        rig.Clock.Now = t2.AddMilliseconds(399);
        rig.Session.SyncPlayPosition();
        Assert.True(rig.Session.EndFlashOn);

        rig.Clock.Now = t2.AddMilliseconds(400);
        rig.Session.SyncPlayPosition();
        Assert.False(rig.Session.EndFlashOn);
    }

    [Fact]
    public void Beginning_a_load_while_ending_publishes_stopped_with_the_flash_off()
    {
        var ports = Ports(analysed: true);
        var rig = new DeckSessionRig(ports);
        var plays = new RecordingHandler<DeckPlayStateChanged>();
        using var sub = rig.Bus.Subscribe(plays);
        StartEnding(rig, ports);
        Assert.Equal(PlayPhase.Ending, rig.Session.PlayState);

        rig.Session.BeginLoad(SomeTrack);

        Assert.Equal(new DeckPlayStateChanged(0, PlayPhase.Stopped, false), plays.Received[^1]);
        Assert.Equal(0.0, rig.Session.PlayPosition);
    }

    [Fact]
    public void Every_sync_raises_PlayPosition_even_when_the_position_has_not_moved()
    {
        var ports = Ports(analysed: true);
        var rig = new DeckSessionRig(ports);
        ports.ScriptedPlayhead.PlayPosition = 0.4;
        var positions = 0;
        rig.Session.Changed += change => { if (change == DeckChange.PlayPosition) positions++; };

        rig.Session.SyncPlayPosition();
        rig.Session.SyncPlayPosition();
        rig.Session.SyncPlayPosition();

        Assert.Equal(3, positions);
    }
}
