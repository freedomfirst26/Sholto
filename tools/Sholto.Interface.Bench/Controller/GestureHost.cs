using Sholto.App.Performance;
using Sholto.Data;
using Sholto.Interface.Controller;

namespace Sholto.Interface.Bench.Controller;

/// <summary>
/// Feeds a <see cref="ScriptedControlSurface"/> the way a hardware controller would be fed, and
/// pumps the manual frame clock the way the real 16 ms timer is. The downstream stack (controller
/// input: recognizer → translator → <c>ICommandSender</c> → command handlers → the performance
/// buckets) is built by whoever composes the graph, over <see cref="Surface"/> and
/// <see cref="Clock"/>, then handed to <see cref="Start"/>. A gesture that doesn't travel this
/// path proves nothing, which is the whole point of driving Bench as a controller rather than a UI
/// puppeteer.
///
/// <para>The manual clock ticks the performance tick and then the controller input exactly as
/// production's does, so hold/timeout gestures (e.g. browse-hold) go through the real code path —
/// they just need real wall-clock time between calls to <see cref="Pump"/>/<see cref="SendGesture"/>
/// to become due, same as they would against the real 16 ms timer.</para>
/// </summary>
/// <param name="surface">The scripted control surface the stack listens to.</param>
/// <param name="clock">The manual frame clock the stack ticks on.</param>
/// <param name="mapping">Translates raw MIDI notes / CCs to controller events.</param>
public sealed class GestureHost(ScriptedControlSurface surface, ManualFrameClock clock, IControllerMapping mapping)
{
    private readonly ScriptedControlSurface _surface = surface;
    private readonly ManualFrameClock _clock = clock;
    private readonly IControllerMapping _mapping = mapping;

    /// <summary>The surface the controller input stack is built over.</summary>
    public ScriptedControlSurface Surface => _surface;

    /// <summary>The clock the performance tick and the controller input subscribe to.</summary>
    public ManualFrameClock Clock => _clock;

    /// <summary>Connects the surface and starts the performance tick, once the stack is built over
    /// <see cref="Surface"/> and <see cref="Clock"/>.</summary>
    public void Start(PerformanceStack performance)
    {
        _surface.Connect();
        performance.Tick.Start();
    }

    /// <summary>Feed one <see cref="ControllerEvent"/> in as if it came from
    /// hardware, then run one frame's worth of the performance tick —
    /// the same processing the real 16 ms timer does — so jog/scratch/EQ effects
    /// that only take hold inside Tick (see PerformanceTick's coalesced-seek
    /// flush) are deterministically flushed within one scenario step, without
    /// needing the scenario to sleep in real wall-clock time.</summary>
    public void SendGesture(ControllerEvent evt)
    {
        _surface.Emit(evt);
        Pump();
    }

    /// <summary>Feed one event in without running a frame. For the latency benchmark, which
    /// pumps the clock itself once per simulated frame.</summary>
    public void Emit(ControllerEvent evt) => _surface.Emit(evt);

    /// <summary>Run one Tick — flushes coalesced jog/scratch/magnetism, and pumps
    /// any gesture that became due purely through time (e.g. browse-hold, if the
    /// scenario has actually let real time pass since the press).</summary>
    public void Pump()
    {
        // The performance tick then the controller input's tick (the recognizer), as the real clock does.
        _clock.Tick();
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
