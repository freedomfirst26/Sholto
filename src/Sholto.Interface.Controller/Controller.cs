using Sholto.Data;

namespace Sholto.Interface.Controller;

/// <summary>Software model of the physical DJ controller. Owns all MIDI I/O
/// (MidiManager, the device mapping) as an internal detail — the App talks to
/// this in high-level language only: it subscribes to <see cref="Action"/> for
/// semantic events (e.g. CueToggle) and drives lights through the Set* methods,
/// never touching note numbers or LEDs.
///
/// Input bubbles UP: a press becomes a semantic event for the App. The App owns
/// all state (cue, master cue, pad page); output comes back DOWN as Set* calls
/// from the App's events, so the Controller lights nothing on its own.</summary>
public sealed class Controller : IControlSurface
{
    // Set SHOLTO_MIDI_LOG=1 to print every incoming MIDI message ([MIDI raw] …) —
    // handy for discovering which channel/note a button sends when mapping it.
    private readonly IMidiConnection _midi;

    public ButtonWithLight MasterCue { get; }
    public ButtonWithLight Deck1Cue { get; }
    public ButtonWithLight Deck2Cue { get; }
    public ButtonWithLight Deck1BeatSync { get; }
    public ButtonWithLight Deck2BeatSync { get; }
    /// <summary>The deck's 8 hot-cue pads (index 0-7), lightable. Pads 0/1/2 are
    /// driven from real stem-mute state (see <see cref="SetPadLight"/>); pads
    /// 3-7 are modeled but currently unused.</summary>
    public IReadOnlyList<ButtonWithLight> Deck1Pads { get; }
    public IReadOnlyList<ButtonWithLight> Deck2Pads { get; }
    /// <summary>The deck's 8 PAD FX1-page pads (index 0-7), lightable. Only pad 0
    /// (echo toggle) is driven today — see <see cref="SetEchoLight"/>.</summary>
    public IReadOnlyList<ButtonWithLight> Deck1PadsFx1 { get; }
    public IReadOnlyList<ButtonWithLight> Deck2PadsFx1 { get; }
    /// <summary>HOT CUE / PAD FX1 pad-mode buttons, per deck.</summary>
    public ButtonWithLight Deck1PadModeHotCue { get; }
    public ButtonWithLight Deck2PadModeHotCue { get; }
    public ButtonWithLight Deck1PadModePadFx1 { get; }
    public ButtonWithLight Deck2PadModePadFx1 { get; }
    public Fader Deck1Volume { get; } = new("Deck1Volume");
    public Fader Deck2Volume { get; } = new("Deck2Volume");
    private readonly IReadOnlyList<Component> _components;
    /// <summary>High-level semantic events for the App. Every control passes through unchanged
    /// except the channel faders, which emit only after soft-takeover pickup.</summary>
    public event Action<ControllerEvent>? Action;

    /// <summary>Raised (true) when the controller (re)connects, (false) when it
    /// drops out. Lets the App show a connection indicator. Fires on a background
    /// thread — marshal before touching UI.</summary>
    public event Action<bool>? ConnectionChanged;

    /// <summary>True while a controller is currently connected.</summary>
    public bool IsConnected => _midi.IsConnected;

    // The pad page the HARDWARE is in: the last mode-button press seen on the wire, or what we last
    // forced it to. Unknown (-1) until we have either, and again after a reconnect. This is the
    // device's own state, not the App's; it only decides whether a force is needed. Written on the
    // MIDI thread, read on the app thread, hence Volatile (no locks).
    private const int PageUnknown = -1;
    private readonly int[] _hardwarePage = [PageUnknown, PageUnknown];

    /// <param name="midi">The MIDI manager (device connection + mapping), composed
    /// once at bootstrap and injected — Controller no longer decides its identity
    /// or reads <c>SHOLTO_MIDI_LOG</c> itself; the composition root does both.</param>
    public Controller(IMidiConnection midi)
    {
        _midi = midi;

        MasterCue     = MakeButton("MasterCue",     new ControllerLight(0, LightFunction.MasterCue));
        Deck1Cue      = MakeButton("Deck1Cue",      new ControllerLight(0, LightFunction.Cue));
        Deck2Cue      = MakeButton("Deck2Cue",      new ControllerLight(1, LightFunction.Cue));
        Deck1BeatSync = MakeButton("Deck1BeatSync", new ControllerLight(0, LightFunction.BeatSync));
        Deck2BeatSync = MakeButton("Deck2BeatSync", new ControllerLight(1, LightFunction.BeatSync));
        Deck1Pads     = MakePadButtons(deck: 0, LightFunction.Pad, "Pad");
        Deck2Pads     = MakePadButtons(deck: 1, LightFunction.Pad, "Pad");
        Deck1PadsFx1  = MakePadButtons(deck: 0, LightFunction.PadFx1, "PadFx1Pad");
        Deck2PadsFx1  = MakePadButtons(deck: 1, LightFunction.PadFx1, "PadFx1Pad");
        Deck1PadModeHotCue = MakeButton("Deck1PadModeHotCue", new ControllerLight(0, LightFunction.PadModeHotCue));
        Deck2PadModeHotCue = MakeButton("Deck2PadModeHotCue", new ControllerLight(1, LightFunction.PadModeHotCue));
        Deck1PadModePadFx1 = MakeButton("Deck1PadModePadFx1", new ControllerLight(0, LightFunction.PadModePadFx1));
        Deck2PadModePadFx1 = MakeButton("Deck2PadModePadFx1", new ControllerLight(1, LightFunction.PadModePadFx1));
        _components =
        [
            MasterCue, Deck1Cue, Deck2Cue, Deck1BeatSync, Deck2BeatSync, Deck1Volume, Deck2Volume,
            ..Deck1Pads, ..Deck2Pads, ..Deck1PadsFx1, ..Deck2PadsFx1,
            Deck1PadModeHotCue, Deck2PadModeHotCue, Deck1PadModePadFx1, Deck2PadModePadFx1,
        ];

        // Faders emit a high-level value only after soft-takeover pickup.
        Deck1Volume.ValueChanged += v => Action?.Invoke(new ControllerEvent.ChannelVolumeMoved(0, v));
        Deck2Volume.ValueChanged += v => Action?.Invoke(new ControllerEvent.ChannelVolumeMoved(1, v));
    }

    private ButtonWithLight MakeButton(string name, ControllerLight light) =>
        new(name, on =>
        {
            var bytes = _midi.Mapping?.RenderLight(light, on);
            if (bytes is not null) _midi.Send(bytes);
        });

    private ButtonWithLight[] MakePadButtons(int deck, LightFunction function, string namePrefix)
    {
        var pads = new ButtonWithLight[8];
        for (int i = 0; i < pads.Length; i++)
            pads[i] = MakeButton($"Deck{deck + 1}{namePrefix}{i}", new ControllerLight(deck, function, Pad: i));
        return pads;
    }

    /// <summary>Output command from the App (via the orchestrator): drive a deck's
    /// BEAT SYNC LED. Called when the deck starts/stops playing.</summary>
    public void SetBeatSync(int deck, bool on) =>
        (deck == 0 ? Deck1BeatSync : Deck2BeatSync).SetLit(on);

    /// <summary>Output command from the App (via the orchestrator): drive a deck's
    /// stem-mute pad LED. <paramref name="group"/> is 0=Drums, 1=Vocals,
    /// 2=Instrumental — the same pads Translate(NoteEvent) reads StemToggle from.
    /// Pad LED bytes are UNVERIFIED on hardware (see the device mapping's RenderLight).</summary>
    public void SetPadLight(int deck, int group, bool on) =>
        (deck == 0 ? Deck1Pads : Deck2Pads)[group].SetLit(on);

    /// <summary>Output command from the App (via the orchestrator): drive a deck's
    /// PAD FX1 pad-1 LED (the echo toggle). Pad LED bytes are UNVERIFIED on
    /// hardware (see the device mapping's RenderLight).</summary>
    public void SetEchoLight(int deck, bool on) =>
        (deck == 0 ? Deck1PadsFx1 : Deck2PadsFx1)[0].SetLit(on);

    /// <summary>Output command from the App (via ControllerFeedback): drive a deck's
    /// headphone-CUE LED.</summary>
    public void SetHeadphoneCueLight(int deck, bool on) =>
        (deck == 0 ? Deck1Cue : Deck2Cue).SetLit(on);

    /// <summary>Output command from the App (via ControllerFeedback): drive the
    /// MASTER CUE LED.</summary>
    public void SetMasterCueLight(bool on) => MasterCue.SetLit(on);

    /// <summary>Make a deck's pad page the App's: lights the matching mode button (dark the
    /// other), repaints BOTH pad sets (the hardware only renders pad LEDs for the mode it is in
    /// and ignores the other, so re-sending both is a no-op on the wire for the inactive one),
    /// and, if the hardware is not already in that mode, forces it there with the press
    /// simulation the mapping renders. The hardware holds its pad mode itself, so this is how the
    /// App's page wins: after a reconnect, and after a press the App ignored (Inspect). A force is
    /// skipped when the hardware is already in the page, because a repeated press can step the
    /// device on to another page.</summary>
    public void SetPadPage(int deck, PadPage page)
    {
        if (deck is not (0 or 1)) return;
        (deck == 0 ? Deck1PadModeHotCue : Deck2PadModeHotCue).SetLit(page == PadPage.HotCue);
        (deck == 0 ? Deck1PadModePadFx1 : Deck2PadModePadFx1).SetLit(page == PadPage.PadFx1);
        if (Volatile.Read(ref _hardwarePage[deck]) != (int)page)
        {
            var press = _midi.Mapping?.RenderPadMode(deck, page);
            if (press is not null)
            {
                Volatile.Write(ref _hardwarePage[deck], (int)page);
                _midi.Send(press);
            }
        }
        foreach (var b in deck == 0 ? Deck1Pads : Deck2Pads) b.Reassert();
        foreach (var b in deck == 0 ? Deck1PadsFx1 : Deck2PadsFx1) b.Reassert();
    }

    /// <summary>Connect to the hardware. Returns false if no controller is found
    /// (the App can still run from the UI).</summary>
    public bool Connect()
    {
        _midi.EventReceived += OnMidi;
        _midi.Connected += OnControllerConnected;   // subscribe before the first attempt
        _midi.ConnectionLost += () => ConnectionChanged?.Invoke(false);
        return _midi.Connect();
    }

    /// <summary>The device (re)connected and came up dark. Repaint the LEDs we
    /// model so a controller that dropped and came back reflects real state again.
    /// The connection's startup init leaves the pads in Hot Cue; the hardware page is
    /// forgotten here so the App's page is forced when the adapter replays it.</summary>
    private void OnControllerConnected()
    {
        for (var i = 0; i < _hardwarePage.Length; i++) Volatile.Write(ref _hardwarePage[i], PageUnknown);
        foreach (var c in _components)
            if (c is ButtonWithLight b) b.Reassert();
        ConnectionChanged?.Invoke(true);
    }

    /// <summary>Leave the controller dark: every component reset (all button LEDs off).</summary>
    private void Reset()
    {
        foreach (var c in _components) c.Reset();
    }

    private void OnMidi(ControllerEvent evt)
    {
        switch (evt)
        {
            // The hardware has switched its own pad mode: remember it, and tell the App.
            case ControllerEvent.PadPageSelected pp:
                if (pp.Deck is 0 or 1) Volatile.Write(ref _hardwarePage[pp.Deck], (int)pp.Page);
                Action?.Invoke(evt);
                break;
            // Channel faders: route through soft-takeover; the Fader re-emits a
            // ChannelVolumeMoved (via Action) only once it has picked up.
            case ControllerEvent.ChannelVolumeMoved v:
                (v.Deck == 0 ? Deck1Volume : Deck2Volume).Move((float)v.Value);
                break;
            // Everything else passes straight through to the App as-is.
            default:
                Action?.Invoke(evt);
                break;
        }
    }

    public void Dispose()
    {
        Reset();          // leave the controller dark
        _midi.Dispose();
    }
}
