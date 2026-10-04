using Sholto.Data;
using Sholto.App.Decks;

namespace Sholto.App;

/// <summary>Executes the transport commands: play, restart, both CUE buttons, tempo range. Depends only
/// on <see cref="IDecks"/>, <see cref="IPlaybackRequests"/> and <see cref="ICueRouting"/>, so it is testable with a fake deck.</summary>
public sealed class TransportCommandHandlers(IDecks decks, IPlaybackRequests playback, ICueRouting cueRouting) :
    ICommandHandler<TogglePlay>,
    ICommandHandler<RestartTrack>,
    ICommandHandler<ToggleHeadphoneCue>,
    ICommandHandler<ToggleMasterCue>,
    ICommandHandler<CycleTempoRange>
{
    private readonly IDecks _decks = decks;
    private readonly IPlaybackRequests _playback = playback;
    private readonly ICueRouting _cueRouting = cueRouting;

    public void Handle(in TogglePlay command) => _playback.OnPlayPressed(command.Deck);

    public void Handle(in RestartTrack command) => _decks.DeckFor(command.Deck).Transport.SeekToFraction(0);

    public void Handle(in ToggleHeadphoneCue command) => _cueRouting.ToggleHeadphoneCue(command.Deck);

    public void Handle(in ToggleMasterCue command) => _cueRouting.ToggleMasterCue();

    public void Handle(in CycleTempoRange command) => _decks.DeckFor(command.Deck).CycleTempoRange();
}
