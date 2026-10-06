using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>Deck readings the test sets; a deck never set reads as empty.</summary>
internal sealed class FakeDeckClockSource : IDeckClockSource
{
    private readonly DeckClockReading[] _readings = new DeckClockReading[2];

    public void Set(int deck, DeckClockReading reading) => _readings[deck] = reading;

    public DeckClockReading Read(int deck) => _readings[deck];
}
