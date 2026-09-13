using System.Linq;
using Sholto.App;
using Sholto.App.ViewModels;
using Sholto.Bench.Rendering;
using Sholto.Controller;
using Sholto.Controller.Gestures;
using Sholto.Controller.Mappings;

namespace Sholto.Bench.Controller;

/// <summary>
/// Composes the REAL downstream stack a hardware controller's events travel
/// through — <see cref="GestureRecognizer"/> → <see cref="GestureBus"/> →
/// <see cref="GestureBindings"/> → <see cref="Orchestrator"/> — the same wiring
/// <c>App.axaml.cs</c> does (see the block around <c>_bus.Dispatch(gesture)</c>),
/// just fed by a <see cref="ScriptedControlSurface"/> instead of a real
/// <c>Controller</c>. A gesture that doesn't travel this path proves nothing,
/// which is the whole point of driving Bench as a controller rather than a UI
/// puppeteer.
///
/// <para>Deliberately narrower than App's composition: no Faceplate guide table
/// (Bench never mounts one), no LED wiring (nothing to light), no DB factory (no
/// persistence in Bench). Orchestrator's <c>Ticked</c> event and public
/// <c>Tick()</c> are still wired exactly as production does, so hold/timeout
/// gestures (e.g. browse-hold) go through the real code path — they just need
/// real wall-clock time between calls to <see cref="Pump"/>/<see cref="SendGesture"/>
/// to become due, same as they would against the real 16 ms timer.</para>
/// </summary>
public sealed class GestureHost
{
    private readonly ScriptedControlSurface _controlSurface = new();
    private readonly GestureRecognizer _recognizer = new();
    private readonly GestureBus _bus = new();
    private readonly Orchestrator _orchestrator;
    private readonly IControllerMapping _mapping = new DdjFlx4Mapping(SholtoOptions.Default.DdjFlx4.Value);

    /// <param name="keyboard">Real keyboard input source for "key" scenario steps —
    /// normally the same <c>MainWindow</c> the scenario's UI driver mounts, so a
    /// keyboard press and a "gesture"/"midi" step travel through the SAME
    /// Orchestrator instance (see the three-entity refactor in
    /// ~/Projects/sholto.md). </param>
    public GestureHost(MainViewModel vm, IKeyboard keyboard)
    {
        _orchestrator = new Orchestrator(_controlSurface, keyboard, vm, dbFactory: () => null,
            SholtoOptions.Default.Scratch, SholtoOptions.Default.Magnetism,
            _recognizer, NoOpAudioFileDecoder.Instance);

        var bindings = new GestureBindings("bench", _orchestrator.BuildGestureTable());
        _bus.Register(bindings);

        _controlSurface.Action += evt =>
        {
            var gesture = _recognizer.Recognize(evt, DateTime.UtcNow);
            if (gesture is not null) _bus.Dispatch(gesture);
        };
        _orchestrator.Ticked += () =>
        {
            foreach (var due in _recognizer.Tick(DateTime.UtcNow))
                _bus.Dispatch(due);
        };

        _controlSurface.Connect();
        _orchestrator.Start();
    }

    /// <summary>Feed one <see cref="ControllerEvent"/> in as if it came from
    /// hardware, then run one frame's worth of <see cref="Orchestrator.Tick"/> —
    /// the same processing the real 16 ms timer does — so jog/scratch/EQ effects
    /// that only take hold inside Tick (see Orchestrator.Tick's coalesced-seek
    /// comment) are deterministically flushed within one scenario step, without
    /// needing the scenario to sleep in real wall-clock time.</summary>
    public void SendGesture(ControllerEvent evt)
    {
        _controlSurface.Emit(evt);
        Pump();
    }

    /// <summary>Run one Tick — flushes coalesced jog/scratch/magnetism, and pumps
    /// any gesture that became due purely through time (e.g. browse-hold, if the
    /// scenario has actually let real time pass since the press).</summary>
    public void Pump()
    {
        _orchestrator.Tick();
        foreach (var due in _recognizer.Tick(DateTime.UtcNow))
            _bus.Dispatch(due);
    }

    /// <summary>Translate one raw MIDI note through the FLX-4 mapping — one layer
    /// above <see cref="SendGesture"/> — and, if it resolved to an event, feed it
    /// through the same pipeline. Returns the resolved event (or null: an
    /// unmapped note, exactly what real hardware noise looks like) so the caller
    /// can report which <see cref="ControllerEvent"/> came out.</summary>
    public ControllerEvent? SendMidiNote(int channel, int note, int velocity, bool isDown)
    {
        var evt = _mapping.Translate(new NoteEvent(channel, note, velocity, isDown));
        if (evt is not null) SendGesture(evt);
        return evt;
    }

    /// <summary>Translate one raw MIDI CC through the FLX-4 mapping and feed it
    /// through the same pipeline as <see cref="SendMidiNote"/>.</summary>
    public ControllerEvent? SendMidiCc(int channel, int control, int value)
    {
        var evt = _mapping.Translate(new CcEvent(channel, control, value));
        if (evt is not null) SendGesture(evt);
        return evt;
    }
}
