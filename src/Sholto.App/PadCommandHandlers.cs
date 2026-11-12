using Sholto.Data;
using Sholto.App.Decks;

namespace Sholto.App;

/// <summary>Executes the pad commands: the hot-cue pads, beat jump and its size, the stem mute and solo pads, the
/// echo pad, the roll pad and the pad-mode presses (which tell <see cref="ICueRouting"/> the new page; the LEDs
/// follow its event). Depends on <see cref="IDecks"/>, <see cref="ICueRouting"/>, <see cref="IDeckHotCues"/> and
/// <see cref="IBeatJumpSize"/>.</summary>
public sealed class PadCommandHandlers(IDecks decks, ICueRouting cueRouting, IDeckHotCues hotCues, IBeatJumpSize beatJumpSize) :
    ICommandHandler<ToggleStem>,
    ICommandHandler<HoldEcho>,
    ICommandHandler<HoldRoll>,
    ICommandHandler<SelectPadPage>,
    ICommandHandler<PressHotCue>,
    ICommandHandler<DeleteHotCue>,
    ICommandHandler<BeatJump>,
    ICommandHandler<HalveBeatJump>,
    ICommandHandler<DoubleBeatJump>,
    ICommandHandler<SoloStem>
{
    private readonly IDecks _decks = decks;
    private readonly ICueRouting _cueRouting = cueRouting;
    private readonly IDeckHotCues _hotCues = hotCues;
    private readonly IBeatJumpSize _beatJumpSize = beatJumpSize;

    public void Handle(in ToggleStem command)
    {
        var session = _decks.DeckFor(command.Deck);
        var nextActive = command.Stem switch
        {
            0 => !session.DrumsActive,
            1 => !session.VocalsActive,
            _ => !session.InstrumentalActive,
        };
        SetStem(session, command.Stem, nextActive);
    }

    /// <summary>Solo one stem: only it stays audible. Soloing the stem that is already alone brings all three back.</summary>
    public void Handle(in SoloStem command)
    {
        var session = _decks.DeckFor(command.Deck);
        bool alreadySolo = session.DrumsActive == (command.Stem == 0)
            && session.VocalsActive == (command.Stem == 1)
            && session.InstrumentalActive == (command.Stem == 2);
        for (int stem = 0; stem < 3; stem++)
            SetStem(session, stem, alreadySolo || stem == command.Stem);
    }

    /// <summary>Move the playhead by the command's beats times the deck's BEAT JUMP size, in beats of the track's
    /// own BPM (not the tempo-adjusted one, so a tempo change does not change the jump). Nothing happens on an
    /// unloaded deck or an unknown BPM.</summary>
    public void Handle(in BeatJump command)
    {
        var session = _decks.DeckFor(command.Deck);
        double bpm = session.SourceBpm;
        if (!session.IsLoaded || bpm <= 0) return;
        session.Transport.SeekRelative(command.Beats * _beatJumpSize.ScaleFor(command.Deck) * 60.0 / bpm);
    }

    public void Handle(in HalveBeatJump command) => _beatJumpSize.Halve(command.Deck);

    public void Handle(in DoubleBeatJump command) => _beatJumpSize.Double(command.Deck);

    public void Handle(in PressHotCue command) => _hotCues.Press(command.Deck, command.Pad, command.Pressed);

    public void Handle(in DeleteHotCue command) => _hotCues.Delete(command.Deck, command.Pad);

    private void SetStem(IDeckSession session, int stem, bool active)
    {
        switch (stem)
        {
            case 0: session.DrumsActive = active; break;
            case 1: session.VocalsActive = active; break;
            case 2: session.InstrumentalActive = active; break;
        }
        session.Stems.SetStemGroup(stem, active);
    }

    /// <summary>Echo is a hold, like roll: press turns it on, release turns it off. Turning it off stops feeding
    /// the echo and leaves its tail to ring out (the deck's SetEcho(false)).</summary>
    public void Handle(in HoldEcho command) => _decks.DeckFor(command.Deck).EchoActive = command.Pressed;

    /// <summary>Roll is a hold, not a toggle: press engages, release disengages, through the generic
    /// SetParam seam.</summary>
    public void Handle(in HoldRoll command) =>
        _decks.DeckFor(command.Deck).Effects.SetParam("roll", 0, command.Pressed ? 1 : 0);

    public void Handle(in SelectPadPage command) => _cueRouting.SelectPadPage(command.Deck, command.Page);
}
