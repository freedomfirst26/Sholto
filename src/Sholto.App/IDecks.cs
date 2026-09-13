using System.Threading.Tasks;
using Sholto.App.ViewModels;

namespace Sholto.App;

/// <summary>The two decks — what <see cref="Orchestrator"/> loads tracks into, plays,
/// and routes per-deck controller gestures to. One of the four real roles extracted
/// from the old <c>IDeckHost</c> (see the split plan in <c>~/Projects/sholto.md</c>,
/// "split IDeckHost into real roles"). <see cref="MainViewModel"/> implements this
/// directly.</summary>
public interface IDecks
{
    DeckViewModel Deck1 { get; }
    DeckViewModel Deck2 { get; }
    DeckViewModel DeckFor(int deck);

    /// <summary>Drop a marker on the given deck at its current playback position and
    /// persist it (keyboard M key, +Shift for deck 2 — routed via
    /// <see cref="Orchestrator"/>'s keyboard handling). Lives here rather than on a
    /// new interface because it is deck-scoped, the same way <see cref="DeckFor"/> is.</summary>
    Task AddMarkerToTargetDeckAsync(int deckIndex);
}
