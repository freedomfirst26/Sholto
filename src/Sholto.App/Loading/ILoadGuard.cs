using Sholto.App.Decks;
using Sholto.App.Library;

namespace Sholto.App.Loading;

/// <summary>Stops a load from replacing a playing deck by accident. The first request for a playing deck is
/// refused and announced (<c>LoadConfirmPending</c>); the same track requested again on the same deck within
/// the window goes through. App-wide, because the controller's LOAD cannot be gated anywhere else.</summary>
public interface ILoadGuard
{
    /// <summary>True when the load may go ahead. False when it must wait for a second request. App thread.</summary>
    bool Permit(IDeckSession deck, Track incoming);
}
