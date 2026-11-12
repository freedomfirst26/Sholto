using Sholto.Data;
using Sholto.Interface.Controller.Model;

namespace Sholto.Interface.Controller;

/// <summary>Maps App state events to the controller's LEDs, and keeps the last value of each so the
/// lights can be repainted. Which LED shows what lives here: play state is the BEAT SYNC LED (solid
/// while playing, blinking with the end-of-track flash), the cue buttons light with headphone/master
/// cue, and the pad page lights one of the four pad-mode buttons and puts the hardware into the same
/// pad mode. Pad lights are on/off only (the cue colours are shown on screen). Each page's pads: HOT
/// CUE lights each pad with a saved cue; PAD FX1 pad 1 is the echo (lit while on) and pad 2 the roll
/// (always lit); BEAT JUMP pads are all lit; SAMPLER pads 1-3 are lit while their stem is audible and
/// pads 5-7 while it is the only audible one.</summary>
public sealed class ControllerFeedback(IControlSurface surface, IEventSubscriber subscriber)
    : IControllerFeedback,
      IEventHandler<DeckPlayStateChanged>, IEventHandler<StemMuteChanged>, IEventHandler<EchoChanged>,
      IEventHandler<HeadphoneCueChanged>, IEventHandler<MasterCueChanged>, IEventHandler<PadPageChanged>,
      IEventHandler<InspectModeChanged>, IEventHandler<DeckHotCuesChanged>
{
    private const int Decks = 2;
    private const int PadsPerPage = 8;
    private const int Stems = StemMuteChanged.StemsPerDeck;
    // The pads of the PAD FX1 and SAMPLER pages that are lit by something (0-based).
    private const int EchoPad = 0;
    private const int RollPad = 1;
    private const int SoloPadBase = 4;   // pads 5-7 solo drums / vocals / instrumental

    private readonly IControlSurface _surface = surface;
    private readonly IEventSubscriber _subscriber = subscriber;
    private readonly bool[] _beatSync = new bool[Decks];
    private readonly bool[,] _stemLit = new bool[Decks, StemMuteChanged.StemsPerDeck];
    private readonly bool[] _echo = new bool[Decks];
    private readonly PadLight[,] _hotCueLight = new PadLight[Decks, PadsPerPage];
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
        _subscriptions.Add(_subscriber.Subscribe<DeckHotCuesChanged>(this));
        // Nothing publishes the pads that are always lit, so paint them here (and again on a reconnect,
        // which resubscribes).
        for (var deck = 0; deck < Decks; deck++) PaintStaticPads(deck);
    }

    public void Resubscribe() => Subscribe();

    public void Reapply()
    {
        for (var deck = 0; deck < Decks; deck++)
        {
            _surface.SetBeatSync(deck, _beatSync[deck]);
            PaintHotCuePads(deck);
            PaintEchoPad(deck);
            PaintSamplerPads(deck);
            PaintStaticPads(deck);
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
        PaintSamplerPads(e.Deck);
    }

    public void Handle(in EchoChanged e)
    {
        if (!InRange(e.Deck)) return;
        _echo[e.Deck] = e.On;
        PaintEchoPad(e.Deck);
    }

    public void Handle(in DeckHotCuesChanged e)
    {
        if (!InRange(e.Deck)) return;
        for (var pad = 0; pad < PadsPerPage; pad++) _hotCueLight[e.Deck, pad] = PadLight.Off;
        foreach (var cue in e.Cues)
            if ((uint)cue.Pad < PadsPerPage) _hotCueLight[e.Deck, cue.Pad] = PadLight.On;
        PaintHotCuePads(e.Deck);
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

    private void PaintHotCuePads(int deck)
    {
        for (var pad = 0; pad < PadsPerPage; pad++)
            _surface.SetPadLight(deck, PadPage.HotCue, pad, _hotCueLight[deck, pad]);
    }

    private void PaintEchoPad(int deck) =>
        _surface.SetPadLight(deck, PadPage.PadFx1, EchoPad, _echo[deck] ? PadLight.On : PadLight.Off);

    /// <summary>Pads 1-3 are lit while their stem is audible, pads 5-7 while it is the only audible one,
    /// pads 4 and 8 stay dark.</summary>
    private void PaintSamplerPads(int deck)
    {
        for (var stem = 0; stem < Stems; stem++)
        {
            _surface.SetPadLight(deck, PadPage.Sampler, stem, _stemLit[deck, stem] ? PadLight.On : PadLight.Off);
            _surface.SetPadLight(deck, PadPage.Sampler, SoloPadBase + stem, OnlyAudible(deck, stem) ? PadLight.On : PadLight.Off);
        }
    }

    private bool OnlyAudible(int deck, int stem)
    {
        for (var other = 0; other < Stems; other++)
            if (_stemLit[deck, other] != (other == stem)) return false;
        return true;
    }

    /// <summary>The pads no event drives: the roll pad is always lit, and so is every BEAT JUMP pad.</summary>
    private void PaintStaticPads(int deck)
    {
        _surface.SetPadLight(deck, PadPage.PadFx1, RollPad, PadLight.On);
        for (var pad = 0; pad < PadsPerPage; pad++)
            _surface.SetPadLight(deck, PadPage.BeatJump, pad, PadLight.On);
    }

    private void Unsubscribe()
    {
        foreach (var s in _subscriptions) s.Dispose();
        _subscriptions.Clear();
    }

    private bool InRange(int deck) => deck is >= 0 and < Decks;
}
