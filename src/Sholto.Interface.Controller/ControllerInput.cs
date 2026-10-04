using System.Collections.Concurrent;
using Sholto.Data;
using Sholto.Interface.Controller.Gestures;

namespace Sholto.Interface.Controller;

/// <summary>The controller's input half. A surface event may arrive on the MIDI thread; it is queued and
/// one drain is posted onto the app thread (no closure per event). On the app thread the recognizer
/// resolves it to a gesture, which the translator turns into a command and sends. The recognizer's
/// hold timer is ticked on the frame clock at an interface order, after the App core, as the old
/// orchestrator-then-recognizer tick was. It always sends: whether a command runs or is only echoed
/// (the guide is open) is the App's Inspect gate, not this interface's business.</summary>
internal sealed class ControllerInput(
    IControlSurface surface,
    IGestureRecognizer recognizer,
    IGestureCommandTranslator translator,
    IAppThread appThread,
    IFrameClock clock) : IControllerInput, IFrameTickHandler
{
    /// <summary>Interface order on the frame clock (the App core is 0..99).</summary>
    public const int FrameOrder = 100;

    private readonly IControlSurface _surface = surface;
    private readonly IGestureRecognizer _recognizer = recognizer;
    private readonly IGestureCommandTranslator _translator = translator;
    private readonly IAppThread _appThread = appThread;
    private readonly IFrameClock _clock = clock;
    private readonly ConcurrentQueue<ControllerEvent> _pending = new();
    private Action? _drain;

    public void Start()
    {
        _drain = Drain;
        _surface.Action += OnSurfaceEvent;
        _clock.Subscribe(this, FrameOrder);
    }

    public void OnFrame(DateTime now)
    {
        var due = _recognizer.Tick(now);
        for (var i = 0; i < due.Count; i++) Route(due[i]);
    }

    private void OnSurfaceEvent(ControllerEvent evt)
    {
        _pending.Enqueue(evt);
        _appThread.Post(_drain!);
    }

    private void Drain()
    {
        while (_pending.TryDequeue(out var evt))
        {
            var gesture = _recognizer.Recognize(evt, _clock.Now);
            if (gesture is { } g) Route(g);
        }
    }

    private void Route(in Gesture gesture) => _translator.Translate(gesture);
}
