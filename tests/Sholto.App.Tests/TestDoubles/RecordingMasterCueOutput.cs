using Sholto.App;
namespace Sholto.App.Tests;

internal sealed class RecordingMasterCueOutput : IMasterCueOutput
{
    public bool Last { get; private set; }
    public int Calls { get; private set; }

    public void SetMasterCue(bool on)
    {
        Last = on;
        Calls++;
    }
}
