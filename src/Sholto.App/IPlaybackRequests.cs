using System;

namespace Sholto.App;

/// <summary>Play/pause requests for a deck — not the transport itself: the actual
/// transport (<c>Pause</c>/<c>TogglePlay</c>/<c>Seek*</c>) lives on <c>Deck</c>, reached
/// via <see cref="IDecks"/>. This interface's two members point in opposite directions
/// — <see cref="OnPlayPressed"/> is a request going down (control surface → app),
/// <see cref="BrakePauseRequested"/> is a request coming back up (app → orchestration) —
/// and both are just that: requests, not the mechanism they trigger. One of the four
/// real roles extracted from the old <c>IDeckHost</c> — see the split plan in
/// <c>~/Projects/sholto.md</c>. <see cref="ViewModels.MainViewModel"/> implements this
/// directly.</summary>
public interface IPlaybackRequests
{
    void OnPlayPressed(int deck);

    /// <summary>Raised when a scratch-capable deck's PAUSE is pressed — Orchestrator
    /// turns this into a vinyl-brake coast-to-stop instead of cutting to silence.</summary>
    event Action<int>? BrakePauseRequested;
}
