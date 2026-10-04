using Sholto.Data;
using Sholto.App.Decks;

namespace Sholto.App;

/// <summary>Executes the loop and grid commands: beat loop toggle/halve/double, grid nudge, markers and
/// the grid editor. Depends only on <see cref="IDecks"/> and <see cref="IDeckMarkers"/>, so it is testable with a fake deck.</summary>
public sealed class LoopCommandHandlers(IDecks decks, IDeckMarkers markers) :
    ICommandHandler<ToggleBeatLoop>,
    ICommandHandler<HalveLoop>,
    ICommandHandler<DoubleLoop>,
    ICommandHandler<NudgeGrid>,
    ICommandHandler<AddMarker>,
    ICommandHandler<OpenGridEditor>
{
    private readonly IDecks _decks = decks;
    private readonly IDeckMarkers _markers = markers;

    public void Handle(in ToggleBeatLoop command) => _decks.DeckFor(command.Deck).Looping.EnableBeatLoop(command.Bars);

    public void Handle(in HalveLoop command) => _decks.DeckFor(command.Deck).Looping.HalveLoop();

    public void Handle(in DoubleLoop command) => _decks.DeckFor(command.Deck).Looping.DoubleLoop();

    /// <summary>The BEAT arrows are one pair shared by both decks. The interface names a deck when it
    /// can (Shift held); with none (-1) the deck with a loop running gets it, else deck 0.</summary>
    public void Handle(in NudgeGrid command)
    {
        var target = command.Deck;
        if (target < 0)
            target = _decks.DeckFor(0).Looping.ActiveLoop is not null ? 0
                   : _decks.DeckFor(1).Looping.ActiveLoop is not null ? 1
                   : 0;
        _decks.DeckFor(target).Beatgrid.NudgeGrid(command.Beats);
    }

    public void Handle(in AddMarker command) => _ = _markers.AddAsync(command.Deck);

    public void Handle(in OpenGridEditor command) => GridEditTarget()?.OpenEdit();

    /// <summary>Which deck a grid edit applies to: the deck whose grid editor is already open, else the
    /// one with an active loop, else the first loaded deck, else null. Mirrors
    /// <c>MainWindow.GridTarget</c> (that copy stays in the view for the phase/BPM-tune keys).</summary>
    private IDeckSession? GridEditTarget()
    {
        if (_decks.Deck1.EditOpen) return _decks.Deck1;
        if (_decks.Deck2.EditOpen) return _decks.Deck2;
        if (_decks.Deck1.Looping.ActiveLoop is not null) return _decks.Deck1;
        if (_decks.Deck2.Looping.ActiveLoop is not null) return _decks.Deck2;
        if (_decks.Deck1.Loading.IsLoaded) return _decks.Deck1;
        if (_decks.Deck2.Loading.IsLoaded) return _decks.Deck2;
        return null;
    }
}
