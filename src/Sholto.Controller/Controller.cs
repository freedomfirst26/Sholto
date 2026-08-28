using Sholto.Controller.Mappings;

namespace Sholto.Controller;

/// <summary>Software model of the physical DJ controller. Owns all MIDI I/O
/// (MidiManager, the device mapping) as an internal detail — the App talks to
/// this in high-level language only: it calls <see cref="Reset"/> and subscribes
/// to <see cref="Action"/> for semantic events (e.g. CueChanged), never touching
/// note numbers or LEDs.
///
/// Input bubbles UP: a raw press is routed to the matching <see cref="Button"/>,
/// whose <see cref="Button.Clicked"/> the Controller catches. The Controller then
/// orchestrates output DOWN (lights the button) and emits the semantic event UP
/// to the App. The App is entirely ignorant of the lighting.</summary>
public sealed class Controller : IDisposable
{
    // Set SHOLTO_MIDI_LOG=1 to print every incoming MIDI message ([MIDI raw] …) —
    // handy for discovering which channel/note a button sends when mapping it.
    private readonly MidiManager _midi;

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
    /// <summary>Which pad page each deck's pads are currently on. Defaults to
    /// HotCue — matches the mode the app forces the hardware into at startup
    /// (see AlsaRawMidi.SendStartupInit).</summary>
    private readonly PadPage[] _padPage = { PadPage.HotCue, PadPage.HotCue };

    /// <summary>High-level semantic events for the App. Carries CueChanged for the
    /// modeled cue buttons, and passes every other control through unchanged.</summary>
    public event Action<ControllerEvent>? Action;

    /// <summary>Raised (true) when the controller (re)connects, (false) when it
    /// drops out. Lets the App show a connection indicator. Fires on a background
    /// thread — marshal before touching UI.</summary>
    public event Action<bool>? ConnectionChanged;

    /// <summary>True while a controller is currently connected.</summary>
    public bool IsConnected => _midi.IsConnected;

    /// <param name="flx4Options">Wire numbers for the DDJ-FLX4 mapping. Defaults
    /// to <c>new DdjFlx4Options()</c> (the device's built-in wire numbers) if
    /// not supplied — existing call sites keep working unchanged.</param>
    public Controller(Mappings.DdjFlx4Options? flx4Options = null)
    {
        _midi = new MidiManager(flx4Options)
        {
            LogAllMessages = Environment.GetEnvironmentVariable("SHOLTO_MIDI_LOG") == "1",
        };

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

        Deck1Cue.Clicked += _ => OnCueClicked(0, Deck1Cue);
        Deck2Cue.Clicked += _ => OnCueClicked(1, Deck2Cue);
        MasterCue.Clicked += _ => OnMasterCueClicked();

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
    /// Pad LED bytes are UNVERIFIED on hardware (see DdjFlx4Mapping.RenderLight).</summary>
    public void SetPadLight(int deck, int group, bool on) =>
        (deck == 0 ? Deck1Pads : Deck2Pads)[group].SetLit(on);

    /// <summary>Output command from the App (via the orchestrator): drive a deck's
    /// PAD FX1 pad-1 LED (the echo toggle). Pad LED bytes are UNVERIFIED on
    /// hardware (see DdjFlx4Mapping.RenderLight).</summary>
    public void SetEchoLight(int deck, bool on) =>
        (deck == 0 ? Deck1PadsFx1 : Deck2PadsFx1)[0].SetLit(on);

    /// <summary>Switch a deck's active pad page: lights the matching mode
    /// button (dark the other), and repaints BOTH pad sets unconditionally.
    /// The hardware only renders pad LEDs for whichever mode it's currently
    /// in and silently ignores note-on for the inactive one, so re-sending
    /// both sets is simpler than tracking which set is "live" — the set that
    /// doesn't match the new page is a no-op on the wire.</summary>
    private void SetPadPage(int deck, PadPage page)
    {
        _padPage[deck] = page;
        (deck == 0 ? Deck1PadModeHotCue : Deck2PadModeHotCue).SetLit(page == PadPage.HotCue);
        (deck == 0 ? Deck1PadModePadFx1 : Deck2PadModePadFx1).SetLit(page == PadPage.PadFx1);
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
    /// Pad mode (Hot Cue) is re-sent by the connection itself.</summary>
    private void OnControllerConnected()
    {
        foreach (var c in _components)
            if (c is ButtonWithLight b) b.Reassert();
        ConnectionChanged?.Invoke(true);
    }

    /// <summary>Return the whole controller to a known state: every component
    /// reset (all button LEDs off) and the cleared cue state percolated up to the
    /// App so its cue audio clears too.</summary>
    public void Reset()
    {
        foreach (var c in _components) c.Reset();
        Action?.Invoke(new ControllerEvent.CueChanged(0, false));
        Action?.Invoke(new ControllerEvent.CueChanged(1, false));
    }

    private void OnMidi(ControllerEvent evt)
    {
        switch (evt)
        {
            // Modeled buttons: route the press to the component; the Clicked
            // handler does the lighting + semantic emit.
            case ControllerEvent.CueToggle c:
                (c.Deck == 0 ? Deck1Cue : Deck2Cue).Press();
                break;
            case ControllerEvent.MasterCuePressed:
                MasterCue.Press();
                break;
            case ControllerEvent.PadPageSelected pp:
                SetPadPage(pp.Deck, pp.Page);
                Action?.Invoke(evt);   // forward too — see PadPageSelected's doc comment
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

    private void OnCueClicked(int deck, ButtonWithLight button)
    {
        button.SetLit(!button.IsLit);                                   // orchestrate the light
        Action?.Invoke(new ControllerEvent.CueChanged(deck, button.IsLit)); // tell the App
    }

    private void OnMasterCueClicked()
    {
        MasterCue.SetLit(!MasterCue.IsLit);                             // orchestrate the light
        Action?.Invoke(new ControllerEvent.MasterCueChanged(MasterCue.IsLit)); // tell the App
    }

    public void Dispose()
    {
        Reset();          // leave the controller dark
        _midi.Dispose();
    }
}
