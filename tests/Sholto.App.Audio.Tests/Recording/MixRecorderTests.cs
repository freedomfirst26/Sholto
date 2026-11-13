using Sholto.App.Audio.Recording;

namespace Sholto.App.Audio.Tests.Recording;

public class MixRecorderTests
{
    [Fact]
    public void MixRecorder_Disarmed_CapturesNothing()
    {
        var recorder = new MixRecorder();
        var ring = new StereoSampleRing(4);
        recorder.Arm(ring);
        recorder.Disarm();

        recorder.Write([1, 2, 3, 4], 2);

        Assert.Equal(0, recorder.CapturedFrames);
        Assert.Equal(0, recorder.DroppedFrames);
        Assert.Equal(0, ring.AvailableFrames);
    }

    [Fact]
    public void MixRecorder_Armed_CountsCapturedAndDropped()
    {
        var recorder = new MixRecorder();
        recorder.Arm(new StereoSampleRing(2));

        recorder.Write([1, 2, 3, 4], 2);
        recorder.Write([5, 6, 7, 8], 2);

        Assert.Equal(2, recorder.CapturedFrames);
        Assert.Equal(2, recorder.DroppedFrames);
    }

    [Fact]
    public void MixRecorder_CountsFramesOfAFourChannelBuffer()
    {
        var recorder = new MixRecorder();
        recorder.Arm(new StereoSampleRing(8));

        recorder.Write([1, 2, 3, 4, 5, 6, 7, 8], 4);

        Assert.Equal(2, recorder.CapturedFrames);
    }

    [Fact]
    public void MixRecorder_ReArm_StartsCountsAtZero()
    {
        var recorder = new MixRecorder();
        recorder.Arm(new StereoSampleRing(2));
        recorder.Write([1, 2, 3, 4], 2);
        recorder.Write([5, 6, 7, 8], 2);

        recorder.Arm(new StereoSampleRing(2));

        Assert.Equal(0, recorder.CapturedFrames);
        Assert.Equal(0, recorder.DroppedFrames);
    }
}
