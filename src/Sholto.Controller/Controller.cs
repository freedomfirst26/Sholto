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

    public Button MasterCue { get; }
    public Button Deck1Cue { get; }
    public Button Deck2Cue { get; }
    public Fader Deck1Volume { get; } = new("Deck1Volume");
    public Fader Deck2Volume { get; } = new("Deck2Volume");
    private readonly IReadOnlyList<Component> _components;

    /// <summary>High-level semantic events for the App. Carries CueChanged for the
    /// modeled cue buttons, and passes every other control through unchanged.</summary>
    public event Action<ControllerEvent>? Action;

    public Controller()
    {
        MasterCue = MakeButton("MasterCue", new ControllerLight(0, LightFunction.MasterCue));
        Deck1Cue  = MakeButton("Deck1Cue",  new ControllerLight(0, LightFunction.Cue));
        Deck2Cue  = MakeButton("Deck2Cue",  new ControllerLight(1, LightFunction.Cue));
        _components = [MasterCue, Deck1Cue, Deck2Cue, Deck1Volume, Deck2Volume];

        Deck1Cue.Clicked += _ => OnCueClicked(0, Deck1Cue);
        Deck2Cue.Clicked += _ => OnCueClicked(1, Deck2Cue);
        // MASTER CUE audio is handled by the FLX-4 hardware; we only toggle the LED.
        MasterCue.Clicked += _ => MasterCue.SetLit(!MasterCue.IsLit);

        // Faders emit a high-level value only after soft-takeover pickup.
        Deck1Volume.ValueChanged += v => Action?.Invoke(new ControllerEvent.ChannelVolumeMoved(0, v));
        Deck2Volume.ValueChanged += v => Action?.Invoke(new ControllerEvent.ChannelVolumeMoved(1, v));
    }

    private Button MakeButton(string name, ControllerLight light) =>
        new(name, on =>
        {
            var bytes = _midi.Mapping?.RenderLight(light, on);
            if (bytes is not null) _midi.Send(bytes);
        });

    /// <summary>Connect to the hardware. Returns false if no controller is found
    /// (the App can still run from the UI).</summary>
    public bool Connect()
    {
        bool ok = _midi.Connect();
        _midi.EventReceived += OnMidi;
        return ok;
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

    private void OnCueClicked(int deck, Button button)
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
