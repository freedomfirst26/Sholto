using Sholto.Data;
using Sholto.Interface.Controller;

namespace Sholto.App.Tests;

/// <summary>A control surface that emits whatever the test hands it; its lights are no-ops.</summary>
internal sealed class EmittingControlSurface : IControlSurface
{
    public event Action<ControllerEvent>? Action;
    public event Action<bool>? ConnectionChanged;

    public bool IsConnected => true;

    public void Emit(ControllerEvent evt) => Action?.Invoke(evt);

    public bool Connect()
    {
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
