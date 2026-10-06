using Avalonia.Media;
using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>The two LOAD TO slots: the six situations of the design (empty, paused, playing, target,
/// reference, caution, armed), the ring, and the platter's spin.</summary>
public class DeckSlotViewModelTests
{
    private static readonly IBrush Key8A = new SolidColorBrush(Colors.Red);

    private readonly GlanceRig _rig = new();

    private IDeckSlotViewModel Slot(int i) => _rig.Header.Slots[i];

    private static DeckClockReading Empty => default;

    private static DeckClockReading Loaded(string title, bool playing, double duration = 360, double position = 80,
        string camelot = "8A", double bpm = 128) =>
        new(true, playing, title, duration, position, 1.0, camelot, Key8A, bpm);

    /// <summary>Open Glance with the decks as given; <paramref name="target"/> is the deck a load goes to.</summary>
    private void Open(DeckClockReading deck1, DeckClockReading deck2, int target)
    {
        _rig.Decks.Set(0, deck1);
        _rig.Decks.Set(1, deck2);
        _rig.SuggestedTarget = target;
        var reference = deck1.IsLoaded && target == 1 ? 0 : deck2.IsLoaded && target == 0 ? 1 : -1;
        _rig.Show(GlanceRig.Track("A"));
        _rig.Answer = q => Task.FromResult(
            GlanceRig.Ranked(q.TargetDeck, [GlanceRig.Track("A")], fitActive: reference >= 0) with { ReferenceDeck = reference });
        _rig.Glance.Open();
    }

    [Fact]
    public void Usual_one_deck_playing_is_the_reference_and_the_empty_deck_is_the_target()
    {
        Open(Loaded("Pressure Systems", playing: true, position: 80), Empty, target: 1);

        var one = Slot(0);
        Assert.True(one.IsLoaded);
        Assert.True(one.IsPlaying);
        Assert.True(one.IsReference);
        Assert.False(one.IsTarget);
        Assert.True(one.ShowStats);
        Assert.Equal("Pressure Systems", one.Title);
        Assert.Equal("8A", one.Camelot);
        Assert.Same(Key8A, one.KeyBrush);
        Assert.Equal("128.0", one.BpmText);
        Assert.Equal("−4:40", one.TimeText);

        var two = Slot(1);
        Assert.False(two.IsLoaded);
        Assert.True(two.IsTarget);
        Assert.False(two.IsReference);
        Assert.Equal("Empty", two.Title);
        Assert.Equal("Loads here", two.EmptyHint);
        Assert.False(two.ShowStats);
        Assert.False(two.IsCaution);
    }

    [Fact]
    public void A_paused_target_reads_paused_and_the_playing_deck_stays_the_reference()
    {
        Open(Loaded("Pressure Systems", playing: true), Loaded("Static Bloom", playing: false, camelot: "9A"), target: 1);

        var two = Slot(1);
        Assert.True(two.IsTarget);
        Assert.False(two.IsPlaying);
        Assert.False(two.IsCaution);
        Assert.True(two.ShowStats);
        Assert.Equal("paused", two.TimeText);
        Assert.True(Slot(0).IsReference);
    }

    [Fact]
    public void Fitting_to_a_paused_deck_makes_it_the_reference_and_the_empty_deck_the_target()
    {
        Open(Empty, Loaded("Static Bloom", playing: false, camelot: "9A"), target: 0);

        Assert.True(Slot(0).IsTarget);
        Assert.False(Slot(0).IsLoaded);
        Assert.Equal("Loads here", Slot(0).EmptyHint);
        Assert.True(Slot(1).IsReference);
        Assert.Equal("paused", Slot(1).TimeText);
    }

    [Fact]
    public void A_playing_target_is_in_caution_with_its_time_left()
    {
        Open(Loaded("Static Bloom", playing: false), Loaded("Afterglow (Club Edit)", playing: true, position: 322, camelot: "4A"), target: 1);

        var two = Slot(1);
        Assert.True(two.IsTarget);
        Assert.True(two.IsCaution);
        Assert.True(two.ShowCaution);
        Assert.False(two.ShowStats);
        Assert.False(two.IsArmed);
        Assert.Equal("Playing · −0:38", two.CautionText);
        Assert.False(Slot(0).IsCaution);
    }

    [Fact]
    public void The_first_replace_press_arms_the_playing_target_and_expiry_disarms_it()
    {
        Open(Loaded("Static Bloom", playing: false), Loaded("Afterglow (Club Edit)", playing: true, position: 322), target: 1);

        _rig.Bus.Publish(new LoadConfirmPending(true, 1, "In", "Afterglow (Club Edit)", 38));

        var two = Slot(1);
        Assert.True(two.IsArmed);
        Assert.True(two.ShowArmed);
        Assert.False(two.IsCaution);
        Assert.False(two.ShowStats);
        Assert.Equal("⇧2 again to replace", two.ArmedText);
        Assert.False(Slot(0).IsArmed);

        _rig.Bus.Publish(new LoadConfirmPending(false, 1, null, null, 0));

        Assert.False(two.IsArmed);
        Assert.True(two.IsCaution);
    }

    [Fact]
    public void Armed_follows_the_pending_deck_even_when_it_was_not_the_target()
    {
        Open(Loaded("Static Bloom", playing: true), Loaded("Afterglow", playing: true), target: 0);
        Assert.True(Slot(0).IsCaution);

        _rig.Bus.Publish(new LoadConfirmPending(true, 1, "In", "Afterglow", 38));

        Assert.True(Slot(1).IsArmed);
        Assert.True(Slot(1).IsTarget);
        Assert.False(Slot(0).IsTarget);
    }

    [Fact]
    public void Both_empty_has_no_reference_and_hints_how_to_load()
    {
        Open(Empty, Empty, target: 0);

        Assert.All(_rig.Header.Slots, s =>
        {
            Assert.False(s.IsLoaded);
            Assert.False(s.IsReference);
            Assert.Equal("Empty", s.Title);
            Assert.Equal(0, s.RemainingFraction);
        });
        Assert.Equal("Loads here", Slot(0).EmptyHint);
        Assert.Equal("to load", Slot(1).EmptyHint);
    }

    [Fact]
    public void An_empty_slot_reads_deck_N_empty_with_a_key_cap_to_load_and_on_the_target_loads_here()
    {
        Open(Empty, Empty, target: 0);

        Assert.Equal("DECK 1 · empty", Slot(0).EmptyTitle);
        Assert.Equal("Loads here", Slot(0).EmptyHint);
        Assert.Equal("DECK 2 · empty", Slot(1).EmptyTitle);
        Assert.Equal("⇧2", Slot(1).KeyCapText);
        Assert.Equal("to load", Slot(1).EmptyHint);

        _rig.Glance.FlipTarget();
        _rig.Tick();

        Assert.Equal("to load", Slot(0).EmptyHint);
        Assert.Equal("⇧1", Slot(0).KeyCapText);
        Assert.Equal("Loads here", Slot(1).EmptyHint);
    }

    [Fact]
    public void The_sweep_runs_only_on_the_empty_target_with_motion_allowed()
    {
        Open(Empty, Empty, target: 0);
        Assert.True(Slot(0).IsSweeping);
        Assert.False(Slot(1).IsSweeping);

        _rig.Glance.FlipTarget();
        _rig.Tick();
        Assert.False(Slot(0).IsSweeping);
        Assert.True(Slot(1).IsSweeping);
    }

    [Fact]
    public void The_sweep_stops_when_the_target_is_loaded()
    {
        Open(Empty, Loaded("A", playing: true), target: 0);
        Assert.True(Slot(0).IsSweeping);

        _rig.Decks.Set(0, Loaded("B", playing: false));
        _rig.Tick();

        Assert.False(Slot(0).IsSweeping);
    }

    [Fact]
    public void The_sweep_never_runs_under_reduced_motion()
    {
        var rig = new GlanceRig(reducedMotion: true);
        rig.SuggestedTarget = 0;
        rig.Show(GlanceRig.Track("A"));
        rig.Glance.Open();

        Assert.True(rig.Header.Slots[0].IsTarget);
        Assert.False(rig.Header.Slots[0].IsLoaded);
        Assert.False(rig.Header.Slots[0].IsSweeping);
    }

    [Fact]
    public void A_steady_empty_target_frame_allocates_nothing_and_repaints_nothing()
    {
        var decks = new FakeDeckClockSource();
        var slot = new DeckSlotFactory(decks, new FixedMotionPreference(false)).Create(0);
        var notifications = 0;
        var repaints = 0;
        slot.PropertyChanged += (_, _) => notifications++;
        slot.Changed += () => repaints++;
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        void Frame()
        {
            now = now.AddSeconds(0.016);
            slot.Update(isTarget: true, isReference: false, replacePending: false, now);
        }

        for (var i = 0; i < 20; i++) Frame();
        notifications = 0;
        repaints = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 60; i++) Frame();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated <= 60 * 64L, $"allocated {allocated} bytes over 60 frames");
        Assert.Equal(0, notifications);
        Assert.Equal(0, repaints);
    }

    [Fact]
    public void Flipping_the_target_moves_the_role_between_slots()
    {
        Open(Loaded("A", playing: false), Loaded("B", playing: false), target: 1);

        _rig.Glance.FlipTarget();

        Assert.True(Slot(0).IsTarget);
        Assert.False(Slot(1).IsTarget);
    }

    [Fact]
    public void The_ring_is_the_share_of_the_track_still_to_play()
    {
        Open(Loaded("A", playing: true, duration: 400, position: 100), Empty, target: 1);
        Assert.Equal(0.75, Slot(0).RemainingFraction, 3);

        _rig.Decks.Set(0, Loaded("A", playing: true, duration: 400, position: 300));
        _rig.Tick();
        Assert.Equal(0.25, Slot(0).RemainingFraction, 3);

        _rig.Decks.Set(0, Loaded("A", playing: true, duration: 400, position: 400));
        _rig.Tick();
        Assert.Equal(0, Slot(0).RemainingFraction);
    }

    [Fact]
    public void The_time_text_counts_down_at_the_decks_speed()
    {
        Open(new DeckClockReading(true, true, "A", 180, 30, 1.25, "8A", Key8A, 128), Empty, target: 1);

        Assert.Equal("−2:00", Slot(0).TimeText);
    }

    [Fact]
    public void The_platter_follows_the_deck_through_the_bar_when_there_is_a_grid()
    {
        // 2 s bars from 0.5 s: at 3.0 s the playhead is 1.25 bars in, a quarter of a turn.
        Open(Reading(position: 3.0), Empty, target: 1);

        Assert.Equal(0.25, Slot(0).SpinTurns, 2);
    }

    [Fact]
    public void Without_a_grid_the_platter_turns_once_per_bar_at_the_decks_tempo()
    {
        // 120 BPM is a 2 s bar: half a second is a quarter of a turn.
        Open(Reading(position: 10, barPeriod: 0, bpm: 120), Empty, target: 1);
        var before = Slot(0).SpinTurns;

        for (var i = 0; i < 5; i++) { _rig.Clock.Advance(0.1); _rig.Header.OnFrame(_rig.Clock.Now); }

        Assert.Equal(before + 0.25, Slot(0).SpinTurns, 2);
    }

    [Fact]
    public void The_platter_stays_still_while_the_deck_is_paused()
    {
        Open(Reading(position: 3.0, playing: false), Empty, target: 1);
        var before = Slot(0).SpinTurns;

        _rig.Decks.Set(0, Reading(position: 3.9, playing: false));
        for (var i = 0; i < 10; i++) _rig.Tick();

        Assert.Equal(before, Slot(0).SpinTurns);
    }

    [Fact]
    public void The_platter_does_not_turn_under_reduced_motion()
    {
        var rig = new GlanceRig(reducedMotion: true);
        rig.Decks.Set(0, Reading(position: 3.0));
        rig.Show(GlanceRig.Track("A"));
        rig.Glance.Open();
        for (var i = 0; i < 20; i++)
        {
            rig.Decks.Set(0, Reading(position: 3.0 + i * 0.1));
            rig.Tick();
        }

        Assert.Equal(0, rig.Header.Slots[0].SpinTurns);
        Assert.True(rig.Header.Slots[0].IsPlaying);
    }

    [Fact]
    public void A_steady_playing_frame_allocates_nothing()
    {
        // Frames inside one whole second and one ring degree: only the spin moves, on every frame.
        var decks = new FakeDeckClockSource();
        var slot = new DeckSlotFactory(decks, new FixedMotionPreference(false)).Create(0);
        var notifications = 0;
        var repaints = 0;
        slot.PropertyChanged += (_, _) => notifications++;
        slot.Changed += () => repaints++;
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        void Frame(int i)
        {
            decks.Set(0, Reading(position: 3.02 + i * 0.007));
            now = now.AddSeconds(0.016);
            slot.Update(isTarget: false, isReference: true, replacePending: false, now);
        }

        for (var i = 0; i < 20; i++) Frame(i);
        notifications = 0;
        repaints = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 20; i < 60; i++) Frame(i);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(repaints >= 35, $"the platter barely moved ({repaints} repaints)");
        Assert.Equal(0, notifications);
        Assert.Equal(0, allocated);
    }

    private static DeckClockReading Reading(double position, bool playing = true, double barPeriod = 2.0, double bpm = 128) =>
        new(true, playing, "A", 360, position, 1.0, "8A", Key8A, bpm, FirstDownbeatSeconds: 0.5, BarPeriodSeconds: barPeriod);
}
