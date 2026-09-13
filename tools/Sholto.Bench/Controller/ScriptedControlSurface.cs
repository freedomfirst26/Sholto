using Sholto.Controller;

namespace Sholto.Bench.Controller;

/// <summary>
/// <see cref="IControlSurface"/> implementation that emits <see cref="ControllerEvent"/>s
/// from a scenario file instead of from hardware — so a Bench scenario drives the
/// app as a controller, through the same <c>GestureRecognizer</c> →
/// <c>GestureBus</c> → <c>Orchestrator</c> pipeline the DDJ-FLX4 does, rather than
/// puppeteering the UI directly.
///
/// <para>No physical LEDs exist to drive, so the output half of the port
/// (<see cref="Reset"/>, <see cref="SetBeatSync"/>, <see cref="SetPadLight"/>,
/// <see cref="SetEchoLight"/>, <see cref="ReassertPadPages"/>) is a no-op, and the
/// cue-button model is tracked as plain fields rather than the real
/// <c>Controller</c>'s lightable <c>Button</c> components — good enough to satisfy
/// <see cref="SnapshotCueState"/>/<see cref="RestoreCueState"/> callers, which
/// Bench's own gesture host never actually calls (that dance is Inspect-mode UI
/// behaviour, out of scope here).</para>
/// </summary>
public sealed class ScriptedControlSurface : IControlSurface
{
    private bool _connected;
    private bool _cue1, _cue2, _master;

    public event Action<ControllerEvent>? Action;
    public event Action<bool>? ConnectionChanged;

    public bool IsConnected => _connected;

    /// <summary>Feed one event into the pipeline exactly as if it had arrived from
    /// hardware — this is the method Bench's scenario/midi drivers call.</summary>
    public void Emit(ControllerEvent evt) => Action?.Invoke(evt);

    /// <summary>Always "succeeds" — there is no hardware to fail to find.</summary>
    public bool Connect()
    {
        _connected = true;
        ConnectionChanged?.Invoke(true);
        return true;
    }

    public void Reset()
    {
        _cue1 = _cue2 = _master = false;
    }

    public void SetBeatSync(int deck, bool on) { }
    public void SetPadLight(int deck, int group, bool on) { }
    public void SetEchoLight(int deck, bool on) { }
    public void ReassertPadPages() { }

    public CueSnapshot SnapshotCueState() => new(_cue1, _cue2, _master);

    public void RestoreCueState(CueSnapshot snapshot)
    {
        _cue1 = snapshot.Deck1;
        _cue2 = snapshot.Deck2;
        _master = snapshot.Master;
    }

    public void Dispose() { }
}
