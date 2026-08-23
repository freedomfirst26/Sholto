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
    private readonly MidiManager _midi = new() { LogAllMessages = false };

    public ButtonWithLight MasterCue { get; }
    public ButtonWithLight Deck1Cue { get; }
    public ButtonWithLight Deck2Cue { get; }
    public ButtonWithLight Deck1BeatSync { get; }
    public ButtonWithLight Deck2BeatSync { get; }
    public Fader Deck1Volume { get; } = new("Deck1Volume");
    public Fader Deck2Volume { get; } = new("Deck2Volume");
    private readonly IReadOnlyList<Component> _components;

    /// <summary>High-level semantic events for the App. Carries CueChanged for the
    /// modeled cue buttons, and passes every other control through unchanged.</summary>
    public event Action<ControllerEvent>? Action;

    /// <summary>Raised (true) when the controller (re)connects, (false) when it
    /// drops out. Lets the App show a connection indicator. Fires on a background
    /// thread — marshal before touching UI.</summary>
    public event Action<bool>? ConnectionChanged;

    /// <summary>True while a controller is currently connected.</summary>
    public bool IsConnected => _midi.IsConnected;

    public Controller()
    {
        MasterCue     = MakeButton("MasterCue",     new ControllerLight(0, LightFunction.MasterCue));
        Deck1Cue      = MakeButton("Deck1Cue",      new ControllerLight(0, LightFunction.Cue));
        Deck2Cue      = MakeButton("Deck2Cue",      new ControllerLight(1, LightFunction.Cue));
        Deck1BeatSync = MakeButton("Deck1BeatSync", new ControllerLight(0, LightFunction.BeatSync));
        Deck2BeatSync = MakeButton("Deck2BeatSync", new ControllerLight(1, LightFunction.BeatSync));
        _components = [MasterCue, Deck1Cue, Deck2Cue, Deck1BeatSync, Deck2BeatSync, Deck1Volume, Deck2Volume];

        Deck1Cue.Clicked += _ => OnCueClicked(0, Deck1Cue);
        Deck2Cue.Clicked += _ => OnCueClicked(1, Deck2Cue);
        // MASTER CUE audio is handled by the FLX-4 hardware; we only toggle the LED.
        MasterCue.Clicked += _ => MasterCue.SetLit(!MasterCue.IsLit);

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

    /// <summary>Output command from the App (via the orchestrator): drive a deck's
    /// BEAT SYNC LED. Called when the deck starts/stops playing.</summary>
    public void SetBeatSync(int deck, bool on) =>
        (deck == 0 ? Deck1BeatSync : Deck2BeatSync).SetLit(on);

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

    public void Dispose()
    {
        Reset();          // leave the controller dark
        _midi.Dispose();
    }
}
