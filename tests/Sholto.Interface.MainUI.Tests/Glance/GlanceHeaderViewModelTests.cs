using System.ComponentModel;
using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>The header strip: which deck the list fits to, its key and BPM, and the countdown.</summary>
public class GlanceHeaderViewModelTests
{
    private readonly GlanceRig _rig = new();
    private GlanceHeaderViewModel Header => _rig.Header;

    private void Reference(int deck, DeckClockReading reading)
    {
        _rig.Decks.Set(deck, reading);
        _rig.Show(GlanceRig.Track("A"));
        _rig.Answer = q => Task.FromResult(GlanceRig.Ranked(q.TargetDeck, [GlanceRig.Track("A")]) with { ReferenceDeck = deck });
        _rig.Glance.Open();
    }

    private static DeckClockReading Playing(double position, double speed = 1.0) =>
        new(true, true, "Born Slippy", 180, position, speed);

    [Fact]
    public void The_header_reads_the_reference_key_bpm_and_title()
    {
        Reference(1, Playing(30));

        Assert.Equal("8A", Header.KeyText);
        Assert.Equal("130.0", Header.BpmText);
        Assert.Equal("Born Slippy", Header.Title);
        Assert.True(Header.IsPlaying);
    }

    [Fact]
    public void The_countdown_is_the_time_left_at_the_decks_speed()
    {
        Reference(1, Playing(30));
        Assert.Equal("−2:30", Header.CountdownText);

        _rig.Decks.Set(1, Playing(30, 1.25));
        _rig.Tick();

        Assert.Equal("−2:00", Header.CountdownText);
    }

    [Fact]
    public void A_tick_inside_the_same_second_raises_no_countdown_notification()
    {
        Reference(1, Playing(30));
        var changed = new List<string?>();
        ((INotifyPropertyChanged)Header).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _rig.Decks.Set(1, Playing(29.6));
        _rig.Tick();
        Assert.DoesNotContain(nameof(Header.CountdownText), changed);

        _rig.Decks.Set(1, Playing(31.2));
        _rig.Tick();
        Assert.Contains(nameof(Header.CountdownText), changed);
    }

    [Fact]
    public void Under_45_seconds_left_is_low()
    {
        Reference(1, Playing(100));
        Assert.False(Header.IsLow);

        _rig.Decks.Set(1, Playing(140));
        _rig.Tick();

        Assert.True(Header.IsLow);
    }

    [Fact]
    public void A_paused_reference_shows_no_countdown()
    {
        Reference(1, new DeckClockReading(true, false, "Born Slippy", 180, 30, 1.0));

        Assert.Equal("", Header.CountdownText);
    }

    [Fact]
    public void The_active_library_filter_is_named()
    {
        _rig.Bus.Publish(new LibraryFilterChanged("Peak time"));

        Assert.Equal("Peak time", Header.FilterLabel);
    }
}
