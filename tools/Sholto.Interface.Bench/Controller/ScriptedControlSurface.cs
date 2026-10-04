using Sholto.Data;
using Sholto.Interface.Controller;

namespace Sholto.Interface.Bench.Controller;

/// <summary>
/// <see cref="IControlSurface"/> implementation that emits <see cref="ControllerEvent"/>s
/// from a scenario file instead of from hardware — so a Bench scenario drives the
/// app as a controller, through the same <c>GestureRecognizer</c> →
/// command bus → <c>Orchestrator</c> pipeline the DDJ-FLX4 does, rather than
/// puppeteering the UI directly.
///
/// <para>No physical LEDs exist to drive, so the output half of the port
/// (<see cref="SetBeatSync"/>, <see cref="SetPadLight"/>, <see cref="SetEchoLight"/>,
/// the cue lights and <see cref="SetPadPage"/>) is a no-op.</para>
/// </summary>
public sealed class ScriptedControlSurface : IControlSurface
{
    private bool _connected;

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

    public void SetBeatSync(int deck, bool on) { }
    public void SetPadLight(int deck, int group, bool on) { }
    public void SetEchoLight(int deck, bool on) { }
    public void SetHeadphoneCueLight(int deck, bool on) { }
    public void SetMasterCueLight(bool on) { }
    public void SetPadPage(int deck, PadPage page) { }

    public void Dispose() { }
}
