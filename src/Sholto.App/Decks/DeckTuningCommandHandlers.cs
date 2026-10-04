using Sholto.Data;

namespace Sholto.App.Decks;

/// <summary>Executes the on-screen deck tuning commands: the half/double BPM override, reset to analysis,
/// the BPM and grid steppers, opening and closing the tune editor, and a grid click. Thin: each calls the
/// matching method on the deck's session. Depends only on <see cref="IDecks"/>, so it is testable with a
/// fake deck.</summary>
public sealed class DeckTuningCommandHandlers(IDecks decks) :
    ICommandHandler<ChangeBpmMultiplier>,
    ICommandHandler<ResetDeckToAnalysis>,
    ICommandHandler<AdjustBpm>,
    ICommandHandler<NudgeGridFine>,
    ICommandHandler<ToggleTuneEditor>,
    ICommandHandler<CloseTuneEditor>,
    ICommandHandler<ClickGrid>
{
    private readonly IDecks _decks = decks;

    public void Handle(in ChangeBpmMultiplier command)
    {
        var deck = _decks.DeckFor(command.Deck);
        switch (command.Op)
        {
            case BpmMultiplierOp.Toggle: deck.ToggleBpmOverride(); break;
            case BpmMultiplierOp.Halve: deck.HalveBpm(); break;
            case BpmMultiplierOp.Double: deck.DoubleBpm(); break;
            case BpmMultiplierOp.Reset: deck.ResetBpmMultiplier(); break;
        }
    }

    public void Handle(in ResetDeckToAnalysis command) => _decks.DeckFor(command.Deck).ResetToAnalysis();

    public void Handle(in AdjustBpm command) => _decks.DeckFor(command.Deck).Beatgrid.AdjustBpm(command.Delta);

    public void Handle(in NudgeGridFine command) => _decks.DeckFor(command.Deck).Beatgrid.NudgeGridFine(command.Seconds);

    public void Handle(in ToggleTuneEditor command) => _decks.DeckFor(command.Deck).ToggleEdit();

    public void Handle(in CloseTuneEditor command) => _decks.DeckFor(command.Deck).CloseEdit();

    public void Handle(in ClickGrid command) => _decks.DeckFor(command.Deck).OnGridClick(command.Seconds);
}
