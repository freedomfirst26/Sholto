using Sholto.App.Audio.Recording;
using Sholto.TestSupport.Allocation;

namespace Sholto.App.Tests.Recording;

public class MixRecorderAllocationTests
{
    [Fact]
    public void Write_DoesNotAllocate()
    {
        var recorder = new MixRecorder();
        // Small enough to fill: the measured writes both copy and drop.
        recorder.Arm(new StereoSampleRing(4096));
        var buffer = new float[256 * 4];
        recorder.Write(buffer, 4); // warm-up

        var result = new AllocationWindows(5).Measure(() =>
        {
            for (var i = 0; i < 1000; i++) recorder.Write(buffer, 4);
        });

        Assert.True(result.AllocatingWindows <= 1, result.ToString());
        Assert.Equal(0, result.Smallest);
        Assert.True(recorder.DroppedFrames > 0);
    }
}
