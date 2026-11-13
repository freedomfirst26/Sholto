using Microsoft.Extensions.Options;
using Sholto.App.Audio.Recording;
using Sholto.App.Recording;
using Sholto.Data;

namespace Sholto.App.Tests.Recording;

public class MixRecordingTests
{
    private readonly FakeMixRecorder _recorder = new();
    private readonly FakeMixRecordingPaths _paths = new();
    private readonly FakeWavSinkFactory _sinks = new();
    private readonly FakeMixRecordingWriterFactory _writers = new();
    private readonly CapturingPublisher _publisher = new();
    private readonly SettableFrameClock _clock = new();
    private readonly RecordingLogFactory _logs = new();
    private readonly MixRecording _recording;

    public MixRecordingTests() =>
        _recording = new MixRecording(_recorder, new SmallRingFactory(), _paths, _sinks, _writers, _publisher, _clock, _logs,
            Options.Create(new MixRecordingOptions()));

    private void Toggle() => _recording.Handle(new ToggleMixRecording(new Origin("test", "record", "press")));

    [Fact]
    public void Toggling_starts_and_publishes_on_at_zero()
    {
        Toggle();

        Assert.True(_recording.IsRecording);
        Assert.NotNull(_recorder.Armed);
        Assert.True(_writers.Latest.Started);
        Assert.Equal([new MixRecordingChanged(true, TimeSpan.Zero, "take-1.wav")], _publisher.Of<MixRecordingChanged>());
        Assert.Empty(_publisher.Of<MixRecordingStopped>());
    }

    [Fact]
    public void The_file_is_named_from_the_local_time()
    {
        Toggle();

        Assert.Equal(TimeZoneInfo.ConvertTimeFromUtc(_clock.Now, TimeZoneInfo.Local), _paths.LastLocalNow);
    }

    [Fact]
    public void Toggling_again_stops_publishes_off_then_one_normal_stop()
    {
        Toggle();
        _recorder.CapturedFrames = 48000 * 3 + 100;
        Toggle();

        Assert.False(_recording.IsRecording);
        Assert.Null(_recorder.Armed);
        Assert.Equal(1, _writers.Latest.Stops);
        Assert.Equal(new MixRecordingChanged(false, TimeSpan.FromSeconds(3), "take-1.wav"), _publisher.Events[^2]);
        Assert.Equal(new MixRecordingStopped("take-1.wav", null), _publisher.Events[^1]);
        Assert.Single(_publisher.Of<MixRecordingStopped>());
    }

    [Fact]
    public void Elapsed_is_published_once_per_whole_second_of_captured_frames()
    {
        Toggle();

        _recorder.CapturedFrames = 47999;
        _recording.OnFrame(_clock.Now);
        _recorder.CapturedFrames = 48000;
        _recording.OnFrame(_clock.Now);
        _recording.OnFrame(_clock.Now);
        _recorder.CapturedFrames = 48000 * 2 - 1;
        _recording.OnFrame(_clock.Now);
        _recorder.CapturedFrames = 48000 * 2;
        _recording.OnFrame(_clock.Now);

        Assert.Equal(
            [TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)],
            _publisher.Of<MixRecordingChanged>().Select(changed => changed.Elapsed));
        Assert.All(_publisher.Of<MixRecordingChanged>(), changed => Assert.True(changed.On));
    }

    [Fact]
    public void A_second_recording_publishes_elapsed_zero_first()
    {
        Toggle();
        _recorder.CapturedFrames = 48000 * 5;
        _recording.OnFrame(_clock.Now);
        Toggle();
        _publisher.Events.Clear();

        Toggle();
        _recording.OnFrame(_clock.Now);

        Assert.Equal([new MixRecordingChanged(true, TimeSpan.Zero, "take-2.wav")], _publisher.Of<MixRecordingChanged>());
    }

    [Fact]
    public void A_path_failure_publishes_one_stop_with_no_file_and_stays_off()
    {
        _paths.Failure = new UnauthorizedAccessException("Access denied");

        Toggle();

        Assert.False(_recording.IsRecording);
        Assert.Null(_recorder.Armed);
        Assert.Empty(_writers.Created);
        Assert.Equal([new MixRecordingStopped(null, "Access denied")], _publisher.Of<MixRecordingStopped>());
        Assert.Empty(_publisher.Of<MixRecordingChanged>());
    }

    [Fact]
    public void A_writer_fault_seen_on_a_tick_publishes_exactly_one_stop_with_the_reason()
    {
        Toggle();
        _writers.Latest.FaultReason = "WAV size limit reached";

        _recording.OnFrame(_clock.Now);
        _recording.OnFrame(_clock.Now);
        _recording.Stop();

        Assert.False(_recording.IsRecording);
        Assert.Null(_recorder.Armed);
        Assert.Equal([new MixRecordingStopped("take-1.wav", "WAV size limit reached")], _publisher.Of<MixRecordingStopped>());
        Assert.False(_publisher.Of<MixRecordingChanged>()[^1].On);
    }

    [Fact]
    public void A_writer_that_does_not_finish_in_time_stops_with_the_unfinalised_reason()
    {
        Toggle();
        _writers.Latest.StopFinishes = false;

        Toggle();

        var stopped = Assert.Single(_publisher.Of<MixRecordingStopped>());
        Assert.Contains("file may be unfinalised", stopped.Reason);
        Assert.Contains(_logs.Lines, line => line.Message.Contains("file may be unfinalised"));
    }

    [Fact]
    public void Dropped_frames_are_logged_at_stop()
    {
        Toggle();
        _recorder.DroppedFrames = 960;

        Toggle();

        Assert.Contains(_logs.Lines, line => line.Message.Contains("dropped 960 frames"));
    }

    [Fact]
    public void Stop_while_not_recording_publishes_nothing()
    {
        _recording.Stop();

        Assert.Empty(_publisher.Events);
    }
}
