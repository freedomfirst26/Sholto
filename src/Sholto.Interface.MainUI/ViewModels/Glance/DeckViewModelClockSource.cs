using System.ComponentModel;
using Avalonia.Media;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>Reads the Glance countdown's playhead facts off the two deck view models (index 0 and 1). The key
/// chip's code and brush are kept here and rebuilt only when a deck says its key changed, so a reading
/// every frame allocates nothing.</summary>
public sealed class DeckViewModelClockSource : IDeckClockSource
{
    private readonly DeckViewModel _deck1;
    private readonly DeckViewModel _deck2;
    private readonly string[] _camelot = ["", ""];
    private readonly IBrush?[] _keyBrush = [null, null];

    public DeckViewModelClockSource(DeckViewModel deck1, DeckViewModel deck2)
    {
        _deck1 = deck1;
        _deck2 = deck2;
        deck1.PropertyChanged += (_, e) => OnDeckChanged(0, deck1, e);
        deck2.PropertyChanged += (_, e) => OnDeckChanged(1, deck2, e);
        CacheKey(0, deck1);
        CacheKey(1, deck2);
    }

    public DeckClockReading Read(int deck)
    {
        var d = deck == 0 ? _deck1 : _deck2;
        var track = d.LoadedTrack;
        return new DeckClockReading(
            d.IsLoaded, d.IsPlaying, track?.Title, track?.Duration.TotalSeconds ?? 0, d.PlaybackSeconds, d.PlaybackSpeed,
            _camelot[deck], _keyBrush[deck], d.EffectiveBpm, d.FirstDownbeatSec, d.BarPeriodSec);
    }

    private void OnDeckChanged(int index, DeckViewModel deck, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DeckViewModel.Camelot) or nameof(DeckViewModel.KeyBrush) or null)
            CacheKey(index, deck);
    }

    private void CacheKey(int index, DeckViewModel deck)
    {
        _camelot[index] = deck.Camelot;
        _keyBrush[index] = deck.KeyBrush;
    }
}
