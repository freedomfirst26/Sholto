using Sholto.Data;
using Sholto.App.Decks;
using Sholto.App.Mixer;

namespace Sholto.App;

/// <summary>Executes the mixer commands: EQ, per-stem levels, filter, channel volume, tempo,
/// crossfader. Depends only on <see cref="IDecks"/> and <see cref="IMixer"/>, so it is testable with a fake deck.</summary>
public sealed class MixerCommandHandlers(IDecks decks, IMixer mixer) :
    ICommandHandler<SetCrossfader>,
    ICommandHandler<SetChannelVolume>,
    ICommandHandler<SetEq>,
    ICommandHandler<SetStemLevel>,
    ICommandHandler<SetFilter>,
    ICommandHandler<SetTempo>
{
    private readonly IDecks _decks = decks;
    private readonly IMixer _mixer = mixer;

    public void Handle(in SetCrossfader command) => _mixer.Crossfader = command.Position;

    public void Handle(in SetChannelVolume command) => _decks.DeckFor(command.Deck).ChannelGain = command.Value;

    public void Handle(in SetEq command) => _decks.DeckFor(command.Deck).Effects.SetEq(command.Band, command.Value);

    public void Handle(in SetStemLevel command)
    {
        var session = _decks.DeckFor(command.Deck);
        switch (command.Stem)
        {
            case 0: session.DrumsLevel = command.Value; break;
            case 1: session.VocalsLevel = command.Value; break;
            default: session.InstrumentalLevel = command.Value; break;
        }
    }

    public void Handle(in SetFilter command) => _decks.DeckFor(command.Deck).Effects.SetFilter(command.Position);

    public void Handle(in SetTempo command) => _decks.DeckFor(command.Deck).SetTempoPosition(command.Position);
}
