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

    // ---- Fader pickup ---------------------------------------------------------------------------

    [Fact]
    public void An_unmeasured_fader_plays_at_unity_but_reports_no_level()
    {
        var rig = new DeckSessionRig(new TestDeckFactory().Create());

        Assert.Null(rig.Session.ChannelGain);
        Assert.False(rig.Session.GainKnown);
        Assert.Equal(1f, rig.Ports.Mixer.Volume);       // audible: unity x crossfade 1
        Assert.Equal(0.0, rig.Session.EffectiveGain);   // the UI draws no level until measured
        Assert.False(rig.Session.IsMuted);              // unknown is not muted
    }

    [Fact]
    public void The_first_fader_move_adopts_the_real_position_and_it_combines_with_the_crossfade()
    {
        var rig = new DeckSessionRig(new TestDeckFactory().Create());

        rig.Session.ChannelGain = 0.25;
        Assert.True(rig.Session.GainKnown);
        Assert.Equal(0.25f, rig.Ports.Mixer.Volume);

        rig.Session.SetCrossfadeGain(0.5f);
        Assert.Equal(0.125f, rig.Ports.Mixer.Volume);
        Assert.Equal(0.125, rig.Session.EffectiveGain, 6);
    }

    [Fact]
    public void A_measured_fader_at_zero_is_muted()
    {
        var rig = new DeckSessionRig(new TestDeckFactory().Create());

        rig.Session.ChannelGain = 0;

        Assert.True(rig.Session.IsMuted);
        Assert.Equal(0f, rig.Ports.Mixer.Volume);
    }

    [Fact]
    public void A_channel_gain_above_one_is_clamped_to_one()
    {
        var rig = new DeckSessionRig(new TestDeckFactory().Create());

        rig.Session.ChannelGain = 1.7;

        Assert.Equal(1.0, rig.Session.ChannelGain);
        Assert.Equal(1f, rig.Ports.Mixer.Volume);
    }

    [Fact]
    public void Setting_the_same_channel_gain_raises_nothing()
    {
        var rig = new DeckSessionRig(new TestDeckFactory().Create());
        var changes = new List<DeckChange>();
        rig.Session.Changed += changes.Add;

        rig.Session.ChannelGain = 0.25;
        rig.Session.ChannelGain = 0.25;

        Assert.Single(changes, DeckChange.ChannelGain);
    }

    // ---- End-of-track flash from the frame clock -------------------------------------------------

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

        for (var i = 0; i < 200; i++)
        {
            rig.Clock.Now = start.AddMilliseconds(i * 16);
            rig.Session.SyncPlayPosition();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 200; i < 2200; i++)
        {
            rig.Clock.Now = start.AddMilliseconds(i * 16);
            rig.Session.SyncPlayPosition();
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
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

    // ---- Tempo ----------------------------------------------------------------------------------

    [Fact]
    public void Matching_the_effective_bpm_flags_a_magnet_adjustment_until_the_user_moves_the_fader()
    {
        var ports = Ports(analysed: true);
        var rig = new DeckSessionRig(ports);
        rig.Session.SetTempoRange(0.16);

        var changed = rig.Session.MatchEffectiveBpm(130.0);

        Assert.True(changed);
        Assert.True(rig.Session.WasMagnetAdjusted);
        Assert.Equal(130.0, rig.Session.EffectiveBpm, 2);

        rig.Session.SetTempoPosition(0.5);

        Assert.False(rig.Session.WasMagnetAdjusted);
    }

    [Fact]
    public void Matching_the_effective_bpm_does_nothing_without_analysis()
    {
        var rig = new DeckSessionRig(Ports(analysed: false));

        Assert.False(rig.Session.MatchEffectiveBpm(130.0));
        Assert.False(rig.Session.WasMagnetAdjusted);
    }

    [Fact]
    public void The_tempo_range_cycles_6_10_16_wide_and_back()
    {
        var rig = new DeckSessionRig(new TestDeckFactory().Create());
        rig.Session.SetTempoRange(0.06);

        var seen = new List<double>();
        for (var i = 0; i < 4; i++)
        {
            rig.Session.CycleTempoRange();
            seen.Add(rig.Ports.Tempo.TempoRange);
        }

        Assert.Equal([0.10, 0.16, 1.00, 0.06], seen);
    }

    // ---- BPM multiplier -------------------------------------------------------------------------

    [Fact]
    public void Halving_the_bpm_asks_the_owner_to_persist_it_but_a_multiplier_restored_by_a_load_does_not()
    {
        var ports = Ports(analysed: true);
        var rig = new DeckSessionRig(ports);
        var chosen = new List<(string Path, double Multiplier)>();
        rig.Session.BpmMultiplierChosen += (path, multiplier) => chosen.Add((path, multiplier));

        rig.Session.LoadTrack(SomeTrack, SomeTrack.FilePath, [], bpmMultiplier: 2.0);
        Assert.Equal(2.0, rig.Session.BpmMultiplier);
        Assert.Empty(chosen);

        rig.Session.HalveBpm();

        Assert.Equal(1.0, rig.Session.BpmMultiplier);
        Assert.Equal([(SomeTrack.FilePath, 1.0)], chosen);
    }

    [Fact]
    public void Beginning_a_load_clears_a_magnet_adjustment()
    {
        var rig = new DeckSessionRig(Ports(analysed: true));
        rig.Session.SetTempoRange(0.16);
        Assert.True(rig.Session.MatchEffectiveBpm(130.0));
        Assert.True(rig.Session.WasMagnetAdjusted);

        rig.Session.BeginLoad(SomeTrack);

        Assert.False(rig.Session.WasMagnetAdjusted);
    }

    [Fact]
    public void Unloading_clears_a_magnet_adjustment()
    {
        var rig = new DeckSessionRig(Ports(analysed: true));
        rig.Session.SetTempoRange(0.16);
        Assert.True(rig.Session.MatchEffectiveBpm(130.0));

        rig.Session.Unload();

        Assert.False(rig.Session.WasMagnetAdjusted);
    }

    [Fact]
    public void Beginning_or_streaming_a_load_restores_the_multiplier_to_the_audio_without_asking_to_persist_it()
    {
        var rig = new DeckSessionRig(Ports(analysed: true));
        var chosen = new List<(string Path, double Multiplier)>();
        rig.Session.BpmMultiplierChosen += (path, multiplier) => chosen.Add((path, multiplier));

        rig.Session.BeginLoad(SomeTrack, 2.0);
        Assert.Equal(2.0, rig.Session.BpmMultiplier);
        Assert.Equal(2.0, rig.Ports.Tempo.BpmMultiplier);

        rig.Session.LoadStreaming(SomeTrack, SomeTrack.FilePath, 0.5);
        Assert.Equal(0.5, rig.Session.BpmMultiplier);
        Assert.Equal(0.5, rig.Ports.Tempo.BpmMultiplier);

        Assert.Empty(chosen);
    }

    [Fact]
    public void Resetting_an_unchanged_multiplier_still_asks_the_owner_to_persist_it_but_raises_no_change()
    {
        // Pins current behaviour: the persist request is unconditional, the change event is not.
        var rig = new DeckSessionRig(Ports(analysed: true));
        rig.Session.LoadTrack(SomeTrack, SomeTrack.FilePath, [], bpmMultiplier: 1.0);
        var chosen = new List<(string Path, double Multiplier)>();
        rig.Session.BpmMultiplierChosen += (path, multiplier) => chosen.Add((path, multiplier));
        var changes = new List<DeckChange>();
        rig.Session.Changed += changes.Add;

        rig.Session.ResetBpmMultiplier();

        Assert.Equal([(SomeTrack.FilePath, 1.0)], chosen);
        Assert.DoesNotContain(DeckChange.BpmMultiplier, changes);
    }

    [Fact]
    public void Halving_with_no_track_loaded_changes_the_multiplier_but_asks_nothing()
    {
        var rig = new DeckSessionRig(Ports(analysed: true));
        var chosen = new List<(string Path, double Multiplier)>();
        rig.Session.BpmMultiplierChosen += (path, multiplier) => chosen.Add((path, multiplier));

        rig.Session.HalveBpm();

        Assert.Equal(0.5, rig.Session.BpmMultiplier);
        Assert.Empty(chosen);
    }

    [Fact]
    public void Matching_a_bpm_already_within_a_hundredth_changes_nothing()
    {
        var rig = new DeckSessionRig(Ports(analysed: true));
        rig.Session.SetTempoRange(0.16);
        var positionBefore = rig.Ports.Tempo.TempoPosition;

        Assert.False(rig.Session.MatchEffectiveBpm(128.005));

        Assert.False(rig.Session.WasMagnetAdjusted);
        Assert.Equal(positionBefore, rig.Ports.Tempo.TempoPosition);
    }

    [Fact]
    public void Cycling_from_a_range_between_stops_goes_to_ten_percent()
    {
        var rig = new DeckSessionRig(new TestDeckFactory().Create());
        rig.Session.SetTempoRange(0.08);

        rig.Session.CycleTempoRange();

        Assert.Equal(0.10, rig.Ports.Tempo.TempoRange);
    }
}
