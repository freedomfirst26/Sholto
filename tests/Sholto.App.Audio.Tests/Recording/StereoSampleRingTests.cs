using Sholto.App.Audio.Recording;

namespace Sholto.App.Audio.Tests.Recording;

public class StereoSampleRingTests
{
    [Fact]
    public void StereoSampleRing_TakesChannels1And2OfAFourChannelBuffer()
    {
        var ring = new StereoSampleRing(8);

        Assert.True(ring.TryWrite([1, 2, 3, 4, 5, 6, 7, 8], 4));

        var read = new float[4];
        Assert.Equal(4, ring.Read(read));
        Assert.Equal([1f, 2f, 5f, 6f], read);
    }

    [Fact]
    public void StereoSampleRing_TwoChannelBufferPassesThrough()
    {
        var ring = new StereoSampleRing(8);

        Assert.True(ring.TryWrite([1, 2, 3, 4, 5, 6], 2));

        Assert.Equal(3, ring.AvailableFrames);
        var read = new float[6];
        Assert.Equal(6, ring.Read(read));
        Assert.Equal([1f, 2f, 3f, 4f, 5f, 6f], read);
        Assert.Equal(0, ring.AvailableFrames);
    }

    [Fact]
    public void StereoSampleRing_FullRing_RejectsWholeBufferAndKeepsOldData()
    {
        var ring = new StereoSampleRing(2);

        Assert.True(ring.TryWrite([1, 2, 3, 4], 2));
        Assert.False(ring.TryWrite([9, 9], 2));

        var read = new float[6];
        Assert.Equal(4, ring.Read(read));
        Assert.Equal([1f, 2f, 3f, 4f], read[..4]);
    }

    [Fact]
    public void StereoSampleRing_WrapsAround()
    {
        var ring = new StereoSampleRing(4);
        var read = new float[6];

        Assert.True(ring.TryWrite([1, 2, 3, 4, 5, 6], 2));
        Assert.Equal(6, ring.Read(read));
        Assert.True(ring.TryWrite([7, 8, 9, 10, 11, 12], 2));
        Assert.Equal(6, ring.Read(read));

        Assert.Equal([7f, 8f, 9f, 10f, 11f, 12f], read);
    }

    [Fact]
    public void StereoSampleRing_ReadStopsAtTheDestinationsWholeFrames()
    {
        var ring = new StereoSampleRing(4);
        ring.TryWrite([1, 2, 3, 4, 5, 6], 2);

        var read = new float[3];
        Assert.Equal(2, ring.Read(read));
        Assert.Equal(2, ring.AvailableFrames);
    }
}
