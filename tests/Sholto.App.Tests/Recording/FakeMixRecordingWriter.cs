using Sholto.App.Audio.Recording;

namespace Sholto.App.Tests.Recording;

/// <summary>A writer with no thread: the test sets its fault and whether stopping finishes in time.</summary>
internal sealed class FakeMixRecordingWriter : IMixRecordingWriter
{
    public bool Started { get; private set; }

    public int Stops { get; private set; }

    public bool StopFinishes { get; set; } = true;

    public string? FaultReason { get; set; }

    public void Start() => Started = true;

    public bool StopAndWait(TimeSpan timeout)
    {
        Stops++;
        return StopFinishes;
    }
}
