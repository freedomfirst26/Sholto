using Sholto.Data;

namespace Sholto.Interface.Controller;

/// <summary>Maps App state events to the controller's LEDs, and keeps the last value of each so the
/// lights can be repainted. Which LED shows what lives here: play state is the BEAT SYNC LED (solid
/// while playing, blinking with the end-of-track flash), a stem pad is lit while its stem is audible,
/// echo is the PAD FX1 pad-1 LED, the cue buttons light with headphone/master cue, and the pad page
/// lights the HOT CUE / PAD FX1 buttons and puts the hardware into the same pad mode.</summary>
public sealed class ControllerFeedback(IControlSurface surface, IEventSubscriber subscriber)
    : IControllerFeedback,
      IEventHandler<DeckPlayStateChanged>, IEventHandler<StemMuteChanged>, IEventHandler<EchoChanged>,
      IEventHandler<HeadphoneCueChanged>, IEventHandler<MasterCueChanged>, IEventHandler<PadPageChanged>,
      IEventHandler<InspectModeChanged>
{
    private const int Decks = 2;

    private readonly IControlSurface _surface = surface;
    private readonly IEventSubscriber _subscriber = subscriber;
    private readonly bool[] _beatSync = new bool[Decks];
    private readonly bool[,] _stemLit = new bool[Decks, StemMuteChanged.StemsPerDeck];
    private readonly bool[] _echo = new bool[Decks];
    private readonly bool[] _headphoneCue = new bool[Decks];
    private readonly PadPage[] _padPage = [PadPage.HotCue, PadPage.HotCue];
    private bool _masterCue;
    private bool _inspecting;
    private readonly List<IDisposable> _subscriptions = [];

    public void Subscribe()
    {
        Unsubscribe();
        _subscriptions.Add(_subscriber.Subscribe<DeckPlayStateChanged>(this));
        _subscriptions.Add(_subscriber.Subscribe<StemMuteChanged>(this));
        _subscriptions.Add(_subscriber.Subscribe<EchoChanged>(this));
        _subscriptions.Add(_subscriber.Subscribe<HeadphoneCueChanged>(this));
        _subscriptions.Add(_subscriber.Subscribe<MasterCueChanged>(this));
        _subscriptions.Add(_subscriber.Subscribe<PadPageChanged>(this));
        _subscriptions.Add(_subscriber.Subscribe<InspectModeChanged>(this));
    }

    public void Resubscribe() => Subscribe();

    public void Reapply()
    {
        for (var deck = 0; deck < Decks; deck++)
        {
            _surface.SetBeatSync(deck, _beatSync[deck]);
            for (var stem = 0; stem < StemMuteChanged.StemsPerDeck; stem++)
                _surface.SetPadLight(deck, stem, _stemLit[deck, stem]);
            _surface.SetEchoLight(deck, _echo[deck]);
            _surface.SetHeadphoneCueLight(deck, _headphoneCue[deck]);
            _surface.SetPadPage(deck, _padPage[deck]);
        }
        _surface.SetMasterCueLight(_masterCue);
    }

    /// <summary>The App's Inspect mode ended: no state changed while it was on (the controller's commands
    /// were only echoed), but the DJ may have pressed a pad-mode button away from the App's page, so
    /// repaint every light from the cache. Turning Inspect on needs nothing.</summary>
    public void Handle(in InspectModeChanged e)
    {
        var wasInspecting = _inspecting;
        _inspecting = e.On;
        if (wasInspecting && !e.On) Reapply();
    }

    public void Handle(in DeckPlayStateChanged e)
    {
        if (!InRange(e.Deck)) return;
        _beatSync[e.Deck] = e.Phase switch
        {
            PlayPhase.Playing => true,
            PlayPhase.Ending => e.EndFlashOn,
            _ => false,
        };
        _surface.SetBeatSync(e.Deck, _beatSync[e.Deck]);
    }

    public void Handle(in StemMuteChanged e)
    {
        if (!InRange(e.Deck) || e.Stem < 0 || e.Stem >= StemMuteChanged.StemsPerDeck) return;
        _stemLit[e.Deck, e.Stem] = !e.Muted;
        _surface.SetPadLight(e.Deck, e.Stem, _stemLit[e.Deck, e.Stem]);
    }

    public void Handle(in EchoChanged e)
    {
        if (!InRange(e.Deck)) return;
        _echo[e.Deck] = e.On;
        _surface.SetEchoLight(e.Deck, e.On);
    }

    public void Handle(in HeadphoneCueChanged e)
    {
        if (!InRange(e.Deck)) return;
        _headphoneCue[e.Deck] = e.On;
        _surface.SetHeadphoneCueLight(e.Deck, e.On);
    }

    public void Handle(in MasterCueChanged e)
    {
        _masterCue = e.On;
        _surface.SetMasterCueLight(e.On);
    }

    public void Handle(in PadPageChanged e)
    {
        if (!InRange(e.Deck)) return;
        _padPage[e.Deck] = e.Page;
        _surface.SetPadPage(e.Deck, e.Page);
    }

    public void Dispose() => Unsubscribe();

    private void Unsubscribe()
    {
        foreach (var s in _subscriptions) s.Dispose();
        _subscriptions.Clear();
    }

    private bool InRange(int deck) => deck is >= 0 and < Decks;
}
