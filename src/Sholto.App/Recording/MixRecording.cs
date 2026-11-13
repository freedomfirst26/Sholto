using Microsoft.Extensions.Options;
using Sholto.App.Analysis;
using Sholto.App.Audio.Recording;
using Sholto.Data;

namespace Sholto.App.Recording;

/// <summary>See <see cref="IMixRecording"/>. Start picks the path, opens the file and builds the ring here on the
/// app thread, starts the writer thread, then arms the master tap. The frame tick publishes the elapsed time once per
/// whole second of captured audio and notices a writer fault. Stop disarms the tap, lets the writer drain, and closes
/// the file here. Every attempt that ends publishes exactly one <see cref="MixRecordingStopped"/>.</summary>
public sealed class MixRecording : IMixRecording, ICommandHandler<ToggleMixRecording>, IFrameTickHandler
{
    /// <summary>Where the recording sits on the frame clock: with the App core.</summary>
    private const int FrameOrder = 60;
    private const string Unfinalised = "writer did not finish; file may be unfinalised";

    private readonly IMixRecorder _recorder;
    private readonly IStereoSampleRingFactory _rings;
    private readonly IMixRecordingPaths _paths;
    private readonly IWavSinkFactory _sinks;
    private readonly IMixRecordingWriterFactory _writers;
    private readonly IEventPublisher _publisher;
    private readonly IFrameClock _clock;
    private readonly ILog _log;
    private readonly TimeSpan _stopTimeout;
    private IMixRecordingWriter? _writer;
    private string? _path;
    private long _publishedSeconds;

    public MixRecording(IMixRecorder recorder, IStereoSampleRingFactory rings, IMixRecordingPaths paths, IWavSinkFactory sinks,
        IMixRecordingWriterFactory writers, IEventPublisher publisher, IFrameClock clock, ILogFactory logs,
        IOptions<MixRecordingOptions> options)
    {
        _recorder = recorder;
        _rings = rings;
        _paths = paths;
        _sinks = sinks;
        _writers = writers;
        _publisher = publisher;
        _clock = clock;
        _log = logs.Create("MixRecording");
        _stopTimeout = TimeSpan.FromMilliseconds(options.Value.StopTimeoutMs);
        clock.Subscribe(this, FrameOrder);
    }

    public bool IsRecording => _writer is not null;

    public void Handle(in ToggleMixRecording command)
    {
        if (IsRecording) Stop();
        else Start();
    }

    public void Start()
    {
        if (IsRecording) return;
        IWavSink? sink = null;
        IMixRecordingWriter? writer = null;
        string path;
        try
        {
            // The file is named from the wall clock where the DJ is: the clock is UTC, the name is local.
            path = _paths.Next(TimeZoneInfo.ConvertTimeFromUtc(_clock.Now, TimeZoneInfo.Local));
            sink = _sinks.Create(path);
            var ring = _rings.Create();
            writer = _writers.Create(ring, sink);
            writer.Start();
            _recorder.Arm(ring);
        }
        // Any failure to start (an unwritable folder, a full disk, a name taken in the meantime) is reported to
        // the DJ as a stopped recording; nothing is left running.
        catch (Exception e)
        {
            _recorder.Disarm();
            if (writer is not null) writer.StopAndWait(_stopTimeout);
            else sink?.Dispose();
            _log.Error($"recording did not start: {e.GetType().Name}");
            _publisher.Publish(new MixRecordingStopped(null, e.Message));
            return;
        }

        _writer = writer;
        _path = path;
        _publishedSeconds = 0;
        _log.Info("recording started");
        _publisher.Publish(new MixRecordingChanged(true, TimeSpan.Zero, path));
    }

    public void Stop()
    {
        if (IsRecording) End(null);
    }

    public void OnFrame(DateTime now)
    {
        if (_writer is null) return;
        if (_writer.FaultReason is { } fault)
        {
            End(fault);
            return;
        }
        long seconds = CapturedSeconds();
        if (seconds == _publishedSeconds) return;
        _publishedSeconds = seconds;
        _publisher.Publish(new MixRecordingChanged(true, TimeSpan.FromSeconds(seconds), _path));
    }

    private void End(string? reason)
    {
        var writer = _writer!;
        var path = _path;
        _writer = null;
        _path = null;

        _recorder.Disarm();
        bool finished = writer.StopAndWait(_stopTimeout);
        // A write that failed during the final drain, or a close that failed, is a reason too.
        reason ??= writer.FaultReason;
        if (!finished)
        {
            _log.Warn(Unfinalised);
            reason ??= Unfinalised;
        }
        long dropped = _recorder.DroppedFrames;
        if (dropped > 0) _log.Warn($"dropped {dropped} frames: the writer fell behind");
        _log.Info(reason is null ? "recording stopped" : "recording stopped early");

        _publisher.Publish(new MixRecordingChanged(false, TimeSpan.FromSeconds(CapturedSeconds()), path));
        _publisher.Publish(new MixRecordingStopped(path, reason));
    }

    private long CapturedSeconds() => _recorder.CapturedFrames / EngineAudio.SampleRate;
}
