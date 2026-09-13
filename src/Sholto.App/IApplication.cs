namespace Sholto.App;

/// <summary>The app, as <see cref="Orchestrator"/> needs to see it — pure interface
/// COMPOSITION of the four real roles split out of the old <c>IDeckHost</c>
/// (<see cref="IDecks"/>, <see cref="IPlaybackRequests"/>, <see cref="IMixer"/>,
/// <see cref="ITrackSelection"/>). This must never gain a member of its own: the
/// moment it does, it is <c>IDeckHost</c> again, which this refactor already
/// deleted once. <see cref="ViewModels.MainViewModel"/> implements it directly.</summary>
public interface IApplication : IDecks, IPlaybackRequests, IMixer, ITrackSelection
{
}
