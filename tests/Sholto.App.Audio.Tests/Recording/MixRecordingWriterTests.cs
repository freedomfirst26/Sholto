using Sholto.App.Audio.Recording;

namespace Sholto.App.Audio.Tests.Recording;

public class MixRecordingWriterTests
{
    /// <summary>Long enough that a healthy writer never hits it; the join returns as soon as the thread exits.</summary>
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(30);

    private static MixRecordingWriter Writer(IStereoSampleReader reader, IWavSink sink, long flushIntervalFrames = 1_000_000) =>
        new(reader, sink, TimeSpan.FromMilliseconds(1), flushIntervalFrames);

    private static float[] Frames(int count)
    {
        var samples = new float[count * 2];
        for (int i = 0; i < samples.Length; i++) samples[i] = i;
        return samples;
    }

    [Fact]
    public void DrainsRingIntoSink()
    {
        var ring = new StereoSampleRing(1000);
        var samples = Frames(500);
        ring.TryWrite(samples, 2);
        var sink = new FakeWavSink();
        var writer = Writer(ring, sink);

        writer.Start();
        Assert.True(writer.StopAndWait(Generous));

        Assert.Equal(samples, sink.Samples);
        Assert.Null(writer.FaultReason);
    }

    [Fact]
    public void StopAndWait_DrainsRemainderAndDisposesSink()
    {
        var ring = new StereoSampleRing(100);
        var sink = new FakeWavSink();
        var writer = Writer(ring, sink);
        writer.Start();
        // Written after the thread started: the stop still drains it before the file closes.
        ring.TryWrite(Frames(60), 2);

        Assert.True(writer.StopAndWait(Generous));

        Assert.Equal(Frames(60), sink.Samples);
        Assert.Equal(1, sink.Disposals);
    }

    [Fact]
    public void Flushes_the_header_after_each_flush_interval_of_frames()
    {
        var ring = new StereoSampleRing(100);
        ring.TryWrite(Frames(100), 2);
        var sink = new FakeWavSink();
        // The thread reads a whole buffer at a time (here 48 frames, 1 ms), so 100 frames cross 40 twice.
        var writer = Writer(ring, sink, flushIntervalFrames: 40);

        writer.Start();
        writer.StopAndWait(Generous);

        // Reads of 48, 48 and 4 frames: the first two each cross 40.
        Assert.Equal(2, sink.Flushes);
    }

    [Fact]
    public void SinkThrowsTooLarge_FaultReasonIsWavSizeLimit()
    {
        var ring = new StereoSampleRing(10);
        ring.TryWrite(Frames(4), 2);
        var sink = new FakeWavSink { ThrowOnWrite = new ArgumentException("WAV file too large") };
        var writer = Writer(ring, sink);

        writer.Start();
        Assert.True(writer.StopAndWait(Generous));

        Assert.Equal("WAV size limit reached", writer.FaultReason);
        Assert.Equal(1, sink.Disposals);
    }

    [Fact]
    public void SinkThrowsIo_FaultReasonIsMessage()
    {
        var ring = new StereoSampleRing(10);
        ring.TryWrite(Frames(4), 2);
        var sink = new FakeWavSink { ThrowOnWrite = new IOException("No space left on device") };
        var writer = Writer(ring, sink);

        writer.Start();
        Assert.True(writer.StopAndWait(Generous));

        Assert.Equal("No space left on device", writer.FaultReason);
        Assert.Equal(1, sink.Disposals);
    }

    [Fact]
    public void A_close_that_fails_on_the_stopping_thread_is_the_fault_reason()
    {
        var ring = new StereoSampleRing(10);
        ring.TryWrite(Frames(4), 2);
        var sink = new FakeWavSink { ThrowOnDispose = new IOException("No space left on device") };
        var writer = Writer(ring, sink);

        writer.Start();
        Assert.True(writer.StopAndWait(Generous));

        Assert.Equal("No space left on device", writer.FaultReason);
        Assert.Equal(Frames(4), sink.Samples);
    }

    [Fact]
    public void Stopping_a_writer_that_never_started_closes_the_file()
    {
        var sink = new FakeWavSink();

        Assert.True(Writer(new StereoSampleRing(10), sink).StopAndWait(Generous));

        Assert.Equal(1, sink.Disposals);
    }
}
