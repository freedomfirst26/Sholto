using System.ComponentModel;
using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>The header strip: the two slots and which deck the list fits to.</summary>
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
    public void The_header_names_the_reference_deck()
    {
        Reference(1, Playing(30));

        Assert.Equal(1, Header.ReferenceDeck);
    }

    [Fact]
    public void TrackListChanged_raises_no_header_notification()
    {
        var changed = new List<string?>();
        ((INotifyPropertyChanged)Header).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _rig.Bus.Publish(new TrackListChanged([new TrackListSource("songs", TrackListSourceKind.Songs, "Songs", 67)], 67));

        Assert.Empty(changed);
    }
}
