using Sholto.Data;
using Sholto.Interface.Controller;

namespace Sholto.Interface.Controller.Tests;

/// <summary>An <see cref="IControlSurface"/> that remembers the last value written to each LED and
/// counts the writes. Fixed-size storage, so recording allocates nothing.</summary>
public sealed class RecordingControlSurface : IControlSurface
{
    public bool[] BeatSync { get; } = new bool[2];
    public bool[,] Pad { get; } = new bool[2, 3];
    public bool[] Echo { get; } = new bool[2];
    public bool[] HeadphoneCue { get; } = new bool[2];
    public bool MasterCue { get; private set; }
    public PadPage[] Page { get; } = [PadPage.HotCue, PadPage.HotCue];
    public int Writes { get; private set; }

    public event Action<ControllerEvent>? Action { add { } remove { } }
    public event Action<bool>? ConnectionChanged { add { } remove { } }
    public bool IsConnected => true;
    public bool Connect() => true;
    public void SetBeatSync(int deck, bool on) { BeatSync[deck] = on; Writes++; }
    public void SetPadLight(int deck, int group, bool on) { Pad[deck, group] = on; Writes++; }
    public void SetEchoLight(int deck, bool on) { Echo[deck] = on; Writes++; }
    public void SetHeadphoneCueLight(int deck, bool on) { HeadphoneCue[deck] = on; Writes++; }
    public void SetMasterCueLight(bool on) { MasterCue = on; Writes++; }
    public void SetPadPage(int deck, PadPage page) { Page[deck] = page; Writes++; }
    public void Dispose() { }
}
