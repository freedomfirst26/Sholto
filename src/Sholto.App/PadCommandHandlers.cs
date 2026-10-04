using Sholto.Data;
using Sholto.App.Decks;

namespace Sholto.App;

/// <summary>Executes the pad commands: the three stem-mute pads, the echo pad, the roll pad and the two
/// pad-mode presses (which tell <see cref="ICueRouting"/> the new page; the LEDs follow its event).
/// Depends only on <see cref="IDecks"/> and <see cref="ICueRouting"/>.</summary>
public sealed class PadCommandHandlers(IDecks decks, ICueRouting cueRouting) :
    ICommandHandler<ToggleStem>,
    ICommandHandler<ToggleEcho>,
    ICommandHandler<HoldRoll>,
    ICommandHandler<SelectPadPage>
{
    private readonly IDecks _decks = decks;
    private readonly ICueRouting _cueRouting = cueRouting;

    public void Handle(in ToggleStem command)
    {
        var session = _decks.DeckFor(command.Deck);
        var nextActive = command.Stem switch
        {
            0 => !session.DrumsActive,
            1 => !session.VocalsActive,
            _ => !session.InstrumentalActive,
        };
        switch (command.Stem)
        {
            case 0: session.DrumsActive = nextActive; break;
            case 1: session.VocalsActive = nextActive; break;
            case 2: session.InstrumentalActive = nextActive; break;
        }
        session.Stems.SetStemGroup(command.Stem, nextActive);
    }

    public void Handle(in ToggleEcho command)
    {
        var session = _decks.DeckFor(command.Deck);
        session.EchoActive = !session.EchoActive;
    }

    /// <summary>Roll is a hold, not a toggle: press engages, release disengages, through the generic
    /// SetParam seam.</summary>
    public void Handle(in HoldRoll command) =>
        _decks.DeckFor(command.Deck).Effects.SetParam("roll", 0, command.Pressed ? 1 : 0);

    public void Handle(in SelectPadPage command) => _cueRouting.SelectPadPage(command.Deck, command.Page);
}
