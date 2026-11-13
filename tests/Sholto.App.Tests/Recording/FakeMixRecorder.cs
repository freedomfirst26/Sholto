using Sholto.App.Audio.Recording;

namespace Sholto.App.Tests.Recording;

/// <summary>An <see cref="IMixRecorder"/> whose counts the test sets; it remembers whether it is armed.</summary>
internal sealed class FakeMixRecorder : IMixRecorder
{
    public IStereoSampleWriter? Armed { get; private set; }

    public long CapturedFrames { get; set; }

    public long DroppedFrames { get; set; }

    public void Arm(IStereoSampleWriter ring)
    {
        CapturedFrames = 0;
        DroppedFrames = 0;
        Armed = ring;
    }

    public void Disarm() => Armed = null;
}
