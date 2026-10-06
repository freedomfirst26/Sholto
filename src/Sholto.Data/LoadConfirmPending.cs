namespace Sholto.Data;

/// <summary>A load onto a playing deck is waiting for confirmation. State: a late subscriber is told whether
/// one is pending.</summary>
/// <param name="Pending">True while a confirmation is awaited.</param>
/// <param name="Deck">The deck the load targets.</param>
/// <param name="IncomingTitle">The track about to load.</param>
/// <param name="PlayingTitle">The track currently playing on the deck.</param>
/// <param name="RemainingSeconds">Time left in the playing track.</param>
public readonly record struct LoadConfirmPending(bool Pending, int Deck, string? IncomingTitle, string? PlayingTitle, double RemainingSeconds) : IStateEvent
{
    public int Slot => 0;
}
