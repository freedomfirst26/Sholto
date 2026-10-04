using Sholto.Data;
using Sholto.App.Decks;

namespace Sholto.App;

/// <summary>Owns headphone cue per deck, master cue and the pad page per deck. Changes arrive only
/// through the gesture handlers; each applies its audio effect (the deck's cue flag, the engine's
/// master-cue monitor) and publishes the new state for interfaces to light.</summary>
public sealed class CueRouting(IDecks decks, IMasterCueOutput masterCueOutput, IEventPublisher publisher) : ICueRouting
{
    private const int DeckCount = 2;

    private readonly IDecks _decks = decks;
    private readonly IMasterCueOutput _masterCueOutput = masterCueOutput;
    private readonly IEventPublisher _publisher = publisher;
    private readonly bool[] _headphoneCue = new bool[DeckCount];
    private readonly PadPage[] _padPage = [PadPage.HotCue, PadPage.HotCue];
    private bool _masterCue;

    public void Start()
    {
        for (var deck = 0; deck < DeckCount; deck++)
        {
            _publisher.Publish(new HeadphoneCueChanged(deck, _headphoneCue[deck]));
            _publisher.Publish(new PadPageChanged(deck, _padPage[deck]));
        }
        _publisher.Publish(new MasterCueChanged(_masterCue));
    }

    public void ToggleHeadphoneCue(int deck)
    {
        if (deck is < 0 or >= DeckCount) return;
        var on = !_headphoneCue[deck];
        _headphoneCue[deck] = on;
        _decks.DeckFor(deck).CueActive = on;
        _publisher.Publish(new HeadphoneCueChanged(deck, on));
    }

    public void ToggleMasterCue()
    {
        _masterCue = !_masterCue;
        _masterCueOutput.SetMasterCue(_masterCue);
        _publisher.Publish(new MasterCueChanged(_masterCue));
    }

    public void SelectPadPage(int deck, PadPage page)
    {
        if (deck is < 0 or >= DeckCount) return;
        _padPage[deck] = page;
        _publisher.Publish(new PadPageChanged(deck, page));
    }
}
